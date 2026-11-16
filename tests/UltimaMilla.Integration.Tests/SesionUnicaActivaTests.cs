using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Api.Auth;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Pruebas unitarias y de resiliencia para el middleware de sesión única activa
/// y el registro compartido de conexiones SignalR sin dependencia de base de datos.
/// </summary>
public class SesionUnicaActivaTests
{
    private sealed class FakeCacheMemoria : IDistributedCacheService
    {
        public readonly Dictionary<string, object?> Datos = new(StringComparer.Ordinal);
        public bool DebeFallar { get; init; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (DebeFallar)
                throw new InvalidOperationException("Fallo simulado de Redis");

            return Task.FromResult(Datos.TryGetValue(key, out var val) && val is T typed ? typed : default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (DebeFallar)
                throw new InvalidOperationException("Fallo simulado de Redis");

            Datos[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Datos.Remove(key);
            return Task.CompletedTask;
        }
    }

    private static DefaultHttpContext CrearHttpContextAutenticado(
        Guid usuarioId,
        string sid,
        string ruta,
        IDistributedCacheService cache,
        string rol = "Repartidor")
    {
        var services = new ServiceCollection()
            .AddSingleton<IDistributedCacheService>(cache)
            .BuildServiceProvider();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new Claim("sub", usuarioId.ToString()),
            new Claim("sid", sid),
            new Claim(ClaimTypes.Role, rol)
        };
        var identidad = new ClaimsIdentity(claims, "Bearer");

        var contexto = new DefaultHttpContext
        {
            RequestServices = services
        };
        contexto.Request.Path = ruta;
        contexto.User = new ClaimsPrincipal(identidad);
        contexto.Response.Body = new MemoryStream();
        return contexto;
    }

    [Fact]
    public async Task Middleware_CuandoRedisTieneOtroSid_Responde401ConCodigoSesionDesplazadaYNoContinua()
    {
        var usuarioId = Guid.NewGuid();
        var cache = new FakeCacheMemoria();
        cache.Datos[ClavesCache.SesionActivaUsuario(usuarioId)] = "sid_dispositivo_nuevo";

        var siguienteInvocado = false;
        var middleware = new SesionUnicaActivaMiddleware(
            _ =>
            {
                siguienteInvocado = true;
                return Task.CompletedTask;
            },
            NullLogger<SesionUnicaActivaMiddleware>.Instance);

        var contexto = CrearHttpContextAutenticado(usuarioId, "sid_dispositivo_viejo", "/api/mobile/v1/ping", cache);

        await middleware.InvokeAsync(contexto);

        Assert.False(siguienteInvocado);
        Assert.Equal((int)HttpStatusCode.Unauthorized, contexto.Response.StatusCode);

        contexto.Response.Body.Position = 0;
        using var doc = await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.Equal(SesionUnicaActivaMiddleware.CodigoSesionDesplazada, doc.RootElement.GetProperty("codigo").GetString());
        Assert.Equal(
            "Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo.",
            doc.RootElement.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("Repartidor")]
    [InlineData("Administrador")]
    [InlineData("Despachador")]
    [InlineData("Operario")]
    [InlineData("Comercio")]
    [InlineData("AdministradorPlataforma")]
    public async Task Middleware_ParaTodosLosPerfiles_DesplazaSiRedisTieneOtroSid(string rol)
    {
        // La sesión única rige para todos los perfiles: la sesión anterior de cualquier cuenta recibe 401 SESION_DESPLAZADA
        var usuarioId = Guid.NewGuid();
        var cache = new FakeCacheMemoria();
        cache.Datos[ClavesCache.SesionActivaUsuario(usuarioId)] = "sid_de_otra_sesion";

        var siguienteInvocado = false;
        var middleware = new SesionUnicaActivaMiddleware(
            _ => { siguienteInvocado = true; return Task.CompletedTask; },
            NullLogger<SesionUnicaActivaMiddleware>.Instance);

        var contexto = CrearHttpContextAutenticado(usuarioId, "sid_de_esta_sesion", "/api/v1/flota/vehiculos", cache, rol);

        await middleware.InvokeAsync(contexto);

        Assert.False(siguienteInvocado);
        Assert.Equal((int)HttpStatusCode.Unauthorized, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task RegistroConexionesHub_ParaUnAdministrador_RegistraYValidaLaSesion()
    {
        var usuarioId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", usuarioId.ToString()), new Claim("sid", "sid_A"), new Claim(ClaimTypes.Role, "Administrador")
        }, "Bearer"));

        var (id, sid) = RegistroConexionesHub.ExtraerIdentidadYSid(principal);
        var cache = new FakeCacheMemoria();

        Assert.Equal(usuarioId, id);
        Assert.Equal("sid_A", sid);
        Assert.True(await RegistroConexionesHub.ValidarYRegistrarSesionAsync(cache, id, sid));
        Assert.Equal("sid_A", await cache.GetAsync<string>(ClavesCache.SesionActivaUsuario(usuarioId)));
        Assert.False(await RegistroConexionesHub.ValidarYRegistrarSesionAsync(cache, id, "sid_B"));
    }

    [Fact]
    public async Task Middleware_CuandoRedisNoTieneClaveParaElUsuario_RegistraSidActualYPermiteContinuar()
    {
        var usuarioId = Guid.NewGuid();
        var cache = new FakeCacheMemoria();

        var siguienteInvocado = false;
        var middleware = new SesionUnicaActivaMiddleware(
            ctx =>
            {
                siguienteInvocado = true;
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                return Task.CompletedTask;
            },
            NullLogger<SesionUnicaActivaMiddleware>.Instance);

        var contexto = CrearHttpContextAutenticado(usuarioId, "sid_primario_01", "/api/mobile/v1/rutas/activas", cache);

        await middleware.InvokeAsync(contexto);

        Assert.True(siguienteInvocado);
        Assert.Equal((int)HttpStatusCode.OK, contexto.Response.StatusCode);
        Assert.Equal("sid_primario_01", await cache.GetAsync<string>(ClavesCache.SesionActivaUsuario(usuarioId)));
    }

    [Fact]
    public async Task Middleware_CuandoRedisFalla_DegradaControladamenteYPermiteContinuar()
    {
        var usuarioId = Guid.NewGuid();
        var cache = new FakeCacheMemoria { DebeFallar = true };

        var siguienteInvocado = false;
        var middleware = new SesionUnicaActivaMiddleware(
            _ =>
            {
                siguienteInvocado = true;
                return Task.CompletedTask;
            },
            NullLogger<SesionUnicaActivaMiddleware>.Instance);

        var contexto = CrearHttpContextAutenticado(usuarioId, "sid_cualquiera", "/api/mobile/v1/ping", cache);

        await middleware.InvokeAsync(contexto);

        Assert.True(siguienteInvocado);
    }

    [Fact]
    public async Task RegistroConexionesHub_SoloDevuelveConexionesConSidDistintoYValidaContraRedis()
    {
        var usuarioId = Guid.NewGuid();
        var cache = new FakeCacheMemoria();
        var connVieja = $"conn_v_{Guid.NewGuid():N}";
        var connNueva = $"conn_n_{Guid.NewGuid():N}";

        RegistroConexionesHub.Registrar(RegistroConexionesHub.HubOperacion, connVieja, usuarioId, "sid_A");
        RegistroConexionesHub.Registrar(RegistroConexionesHub.HubOperacion, connNueva, usuarioId, "sid_B");

        try
        {
            var desplazadas = RegistroConexionesHub.ObtenerConexionesDesplazadas(
                RegistroConexionesHub.HubOperacion,
                usuarioId,
                "sid_B");

            Assert.Single(desplazadas);
            Assert.Contains(connVieja, desplazadas);
            Assert.DoesNotContain(connNueva, desplazadas);

            // Auto-registro cuando caché está vacía y rechazo al validar otro sid
            var validoVacio = await RegistroConexionesHub.ValidarYRegistrarSesionAsync(cache, usuarioId, "sid_B");
            var rechazado = await RegistroConexionesHub.ValidarYRegistrarSesionAsync(cache, usuarioId, "sid_A");

            Assert.True(validoVacio);
            Assert.False(rechazado);
        }
        finally
        {
            RegistroConexionesHub.Remover(RegistroConexionesHub.HubOperacion, connVieja);
            RegistroConexionesHub.Remover(RegistroConexionesHub.HubOperacion, connNueva);
        }
    }
}

