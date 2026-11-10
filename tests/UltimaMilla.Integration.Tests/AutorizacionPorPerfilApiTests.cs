using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Autorización de la API por perfil (letra 6.3). Autenticarse no alcanza: un usuario de comercio no puede llamar directamente a los endpoints
/// del operador, y el personal del operador no usa los del comercio. Se prueba con sesiones reales (JWT) de cada perfil, porque el host de
/// pruebas acepta una identidad de demostración con todos los perfiles cuando no se envía token.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AutorizacionPorPerfilApiTests
{
    private readonly ApiFactory _factory;

    public AutorizacionPorPerfilApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Lecturas que solo corresponden al personal del operador: un usuario de comercio recibe 403 en todas.</summary>
    public static readonly string[] LecturasDelOperador =
    {
        "/api/usuarios", "/api/perfiles", "/api/comercios", "/api/reglas-operativas/vigente", "/api/cuadros-tarifarios",
        "/api/zonas-cobertura", "/api/v1/flota/repartidores", "/api/v1/flota/vehiculos", "/api/reportes", "/api/liquidaciones",
        "/api/tablero/resumen", "/api/operacion/tablero", "/api/deposito/envios-admitidos", "/api/deposito/envios-en-deposito",
        "/api/planificacion/hojas-de-ruta", "/api/planificacion/envios-pendientes", "/api/backoffice/incidencias",
        "/api/operacion/webhooks/historial", "/api/v1/mensajeria/cola-fallidos", "/api/operacion/cache/metricas",
        "/api/v1/motivos-no-entrega", "/api/retiros"
    };

    [FactPostgres]
    public async Task UnUsuarioDeComercio_NoPuedeLeerNingunEndpointDeAdministracionDelOperador()
    {
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");

        foreach (var ruta in LecturasDelOperador)
        {
            var respuesta = await comercio.GetAsync(ruta);
            Assert.True(respuesta.StatusCode == HttpStatusCode.Forbidden,
                $"GET {ruta} debería negarse al comercio (403) y respondió {(int)respuesta.StatusCode}");
        }
    }

    [FactPostgres]
    public async Task UnUsuarioDeComercio_NoPuedeModificarNadaDeLaAdministracionDelOperador()
    {
        var comercio = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");

        var escrituras = new (HttpMethod Metodo, string Ruta, object Cuerpo)[]
        {
            (HttpMethod.Post, "/api/usuarios", new { }),
            (HttpMethod.Post, "/api/comercios", new { }),
            (HttpMethod.Post, "/api/zonas-cobertura", new { }),
            (HttpMethod.Post, "/api/cuadros-tarifarios", new { }),
            (HttpMethod.Post, "/api/reglas-operativas", new { }),
            (HttpMethod.Post, "/api/v1/flota/vehiculos", new { }),
            (HttpMethod.Post, "/api/planificacion/hojas-de-ruta", new { }),
            (HttpMethod.Post, "/api/operacion/procesos/cierre-diario", new { }),
            (HttpMethod.Post, "/api/liquidaciones/generar", new { })
        };

        foreach (var (metodo, ruta, cuerpo) in escrituras)
        {
            var respuesta = await comercio.SendAsync(new HttpRequestMessage(metodo, ruta) { Content = JsonContent.Create(cuerpo) });
            Assert.True(respuesta.StatusCode == HttpStatusCode.Forbidden,
                $"{metodo} {ruta} debería negarse al comercio (403) y respondió {(int)respuesta.StatusCode}");
        }
    }

    [FactPostgres]
    public async Task ElPersonalDelOperador_NoUsaLosEndpointsDelComercio()
    {
        foreach (var (email, password) in new[] { ("admin@gmail.com", "Admin.2026!"), ("deposito@gmail.com", "Deposito.2026!"), ("repartidor@gmail.com", "Repartidor.2026!") })
        {
            var cliente = await ClienteComoAsync(email, password);
            foreach (var ruta in new[] { "/api/comercio/envios", "/api/comercio/devoluciones", "/api/comercio/avisos", "/api/comercio/cuenta-corriente", "/api/comercio/retiros" })
            {
                var respuesta = await cliente.GetAsync(ruta);
                Assert.True(respuesta.StatusCode == HttpStatusCode.Forbidden,
                    $"{email}: GET {ruta} debería negarse (403) y respondió {(int)respuesta.StatusCode}");
            }

            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/comercio/clave-api", new { })).StatusCode);
        }
    }

    [FactPostgres]
    public async Task ElRepartidor_SoloAccedeALoSuyoYNoALaAdministracion()
    {
        var repartidor = await ClienteComoAsync("repartidor@gmail.com", "Repartidor.2026!");

        // Su propia operación sí
        Assert.NotEqual(HttpStatusCode.Forbidden, (await repartidor.GetAsync("/api/mobile/v1/rutas")).StatusCode);

        // La administración y la planificación no
        foreach (var ruta in new[] { "/api/usuarios", "/api/comercios", "/api/reportes", "/api/planificacion/hojas-de-ruta", "/api/deposito/envios-admitidos" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(ruta)).StatusCode);
        }
    }

    [FactPostgres]
    public async Task ElOperarioDeDeposito_OperaElDepositoPeroNoPlanificaNiAdministra()
    {
        var deposito = await ClienteComoAsync("deposito@gmail.com", "Deposito.2026!");

        Assert.Equal(HttpStatusCode.OK, (await deposito.GetAsync("/api/deposito/envios-admitidos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await deposito.GetAsync("/api/planificacion/hojas-de-ruta")).StatusCode);

        // Armar rutas, administrar usuarios o ver reportes no es de su perfil
        Assert.Equal(HttpStatusCode.Forbidden,
            (await deposito.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new { repartidorId = Guid.NewGuid(), vehiculoId = Guid.NewGuid(), fecha = "2030-01-01", enviosIds = new[] { Guid.NewGuid() } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await deposito.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await deposito.GetAsync("/api/reportes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await deposito.PostAsJsonAsync("/api/usuarios", new { })).StatusCode);
    }

    [FactPostgres]
    public async Task ElAdministrador_PuedeAdministrarYConsultar()
    {
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");

        foreach (var ruta in LecturasDelOperador.Where(r => r != "/api/operacion/cache/metricas"))
        {
            var respuesta = await admin.GetAsync(ruta);
            Assert.True(respuesta.StatusCode != HttpStatusCode.Forbidden && respuesta.StatusCode != HttpStatusCode.Unauthorized,
                $"GET {ruta} debería permitirse al administrador y respondió {(int)respuesta.StatusCode}");
        }
    }

    [FactPostgres]
    public async Task ElSeguimientoPublicoYElLoginNoRequierenSesion()
    {
        var anonimo = _factory.CreateClient();

        // Un código inexistente responde 404 sin pedir sesión; no 401 ni 403
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/seguimiento/TRK-NOEXISTE00")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/seguimiento/RET-NOEXISTE00")).StatusCode);

        var login = await anonimo.PostAsJsonAsync("/api/v1/auth/login", new { email = "comercio@gmail.com", password = "Comercio.2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [FactPostgres]
    public async Task DosComerciosDelMismoOperador_NoVenLosEnviosNiLosRetirosDelOtro()
    {
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        var comercioA = await ClienteComoAsync("comercio@gmail.com", "Comercio.2026!");

        // Se da de alta un segundo comercio del mismo operador, con su propio usuario de comercio
        var sufijo = Sufijo();
        var emailB = $"comercio.b.{sufijo}@ultimamilla.com.uy";
        var rutB = string.Concat(Enumerable.Range(0, 12).Select(i => (Math.Abs(sufijo.GetHashCode()) + i * 7) % 10));

        var comercioCreado = await admin.PostAsJsonAsync("/api/comercios", new
        {
            razonSocial = "Tienda B " + sufijo, nombreFantasia = "Tienda B", rut = rutB, email = emailB,
            telefono = "099555111", direccion = "Av. Rivera 100"
        });
        Assert.Equal(HttpStatusCode.Created, comercioCreado.StatusCode);
        var comercioBId = (await comercioCreado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var usuarioCreado = await admin.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Usuario", apellido = "Tienda B", email = emailB, password = "ComercioB.2026!",
            perfilId = DatabaseSeeder.DefaultPerfilComercioId, comercioId = comercioBId
        });
        Assert.True(usuarioCreado.StatusCode == HttpStatusCode.Created, await usuarioCreado.Content.ReadAsStringAsync());

        // Un envío y un retiro del comercio A
        var envio = await comercioA.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente A", calle = "Av. Italia", numero = "1", ciudad = "Montevideo",
            bultos = new[] { new { peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, envio.StatusCode);
        var envioId = (await envio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var retiro = await comercioA.PostAsJsonAsync("/api/comercio/retiros", new
        {
            nombreContacto = "Ana", calle = "Av. Italia", numero = "2", ciudad = "Montevideo", motivo = "Cambio",
            bultos = new[] { new { descripcion = "Camisa", peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, retiro.StatusCode);
        var retiroId = (await retiro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // El comercio B no los ve, ni por detalle ni por listado, ni puede operar sobre ellos
        var comercioB = await ClienteComoAsync(emailB, "ComercioB.2026!");
        Assert.Equal(HttpStatusCode.NotFound, (await comercioB.GetAsync($"/api/envios/{envioId}/detalle")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await comercioB.GetAsync($"/api/comercio/retiros/{retiroId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await comercioB.PostAsJsonAsync($"/api/comercio/retiros/{retiroId}/cancelar", new { motivo = "x" })).StatusCode);

        var listado = await comercioB.GetFromJsonAsync<JsonElement>("/api/comercio/retiros");
        Assert.DoesNotContain(listado.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == retiroId);
    }
}
