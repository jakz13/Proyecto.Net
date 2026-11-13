using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Qué puede consultar cada perfil del operador según la letra (sección 3): el administrador configura la operación; el despachador
/// planifica, monitorea y resuelve incidencias; el operario trabaja en el depósito. Ninguno entra a lo que le corresponde a otro.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class PermisosPorPerfilApiTests
{
    private const string Administrador = "Administrador";
    private const string Despachador = "Despachador";
    private const string Operario = "Operario";

    private static readonly (string Ruta, string[] Perfiles)[] Lecturas =
    {
        // Administración
        ("/api/usuarios", new[] { Administrador }),
        ("/api/perfiles", new[] { Administrador }),
        ("/api/cuadros-tarifarios", new[] { Administrador }),
        ("/api/liquidaciones", new[] { Administrador }),
        ("/api/operacion/webhooks/historial", new[] { Administrador }),
        ("/api/v1/mensajeria/cola-fallidos", new[] { Administrador }),

        // Supervisión: reportes y los catálogos con los que se planifica
        ("/api/comercios", new[] { Administrador, Despachador }),
        ("/api/v1/flota/repartidores", new[] { Administrador, Despachador }),
        ("/api/v1/flota/vehiculos", new[] { Administrador, Despachador }),
        ("/api/reportes", new[] { Administrador, Despachador }),

        // Planificación y monitoreo: solo el despachador
        ("/api/tablero/resumen", new[] { Despachador }),
        ("/api/operacion/tablero", new[] { Despachador }),
        ("/api/planificacion/envios-pendientes", new[] { Despachador }),
        ("/api/backoffice/incidencias", new[] { Despachador }),

        // Control del despacho: despachador y operario
        ("/api/planificacion/hojas-de-ruta", new[] { Despachador, Operario }),
        ("/api/retiros", new[] { Despachador, Operario }),
        ("/api/deposito/hojas-de-ruta-pendientes-rendicion", new[] { Despachador, Operario }),

        // Depósito: solo el operario
        ("/api/deposito/envios-admitidos", new[] { Operario }),
        ("/api/deposito/envios-en-deposito", new[] { Operario }),
    };

    private readonly ApiFactory _factory;

    public PermisosPorPerfilApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ComoAsync(string perfil)
    {
        if (perfil == Despachador)
            return await UsuariosDePrueba.DespachadorAsync(_factory);
        if (perfil == Operario)
            return await UsuariosDePrueba.OperarioAsync(_factory);

        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@gmail.com", password = "Admin.2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    [FactPostgres]
    public async Task CadaPerfilConsultaSoloLoQueLeCorresponde()
    {
        var errores = new List<string>();

        foreach (var perfil in new[] { Administrador, Despachador, Operario })
        {
            var cliente = await ComoAsync(perfil);
            foreach (var (ruta, permitidos) in Lecturas)
            {
                var respuesta = await cliente.GetAsync(ruta);
                var denegado = respuesta.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;

                if (permitidos.Contains(perfil) && denegado)
                    errores.Add($"{perfil} debería poder consultar {ruta} y recibió {(int)respuesta.StatusCode}");
                if (!permitidos.Contains(perfil) && !denegado)
                    errores.Add($"{perfil} NO debería poder consultar {ruta} y recibió {(int)respuesta.StatusCode}");
            }
        }

        Assert.True(errores.Count == 0, string.Join("\n", errores));
    }

    [FactPostgres]
    public async Task ElAdministradorNoPlanificaNiTrabajaEnElDeposito()
    {
        var admin = await ComoAsync(Administrador);

        // Escrituras que la letra le asigna al despachador o al operario
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/planificacion/hojas-de-ruta", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/planificacion/hojas-de-ruta/{Guid.NewGuid()}/despacho", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/deposito/envios/{Guid.NewGuid()}/recepcion", new { codigosBultosEscaneados = new[] { "X" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/backoffice/incidencias", new { tipo = "AveriaVehiculo", descripcion = "x" })).StatusCode);
    }

    [FactPostgres]
    public async Task ElExtravioYLaRendicionPreviaLosHacenDespachadorYOperario_ElAdministradorNo()
    {
        // Despacho/Verificar y la pantalla de Envíos las usan ambos perfiles: con un id inexistente la respuesta es 404 (pasó la autorización), nunca 403
        foreach (var perfil in new[] { Despachador, Operario })
        {
            var cliente = await ComoAsync(perfil);
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{Guid.NewGuid()}/extravio", new { motivo = "No apareció" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{Guid.NewGuid()}/rendicion", new { codigosBultosDevueltos = Array.Empty<string>() })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/deposito/hojas-de-ruta/{Guid.NewGuid()}/rendicion")).StatusCode);
        }

        var admin = await ComoAsync(Administrador);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/deposito/envios/{Guid.NewGuid()}/extravio", new { motivo = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/deposito/hojas-de-ruta/{Guid.NewGuid()}/rendicion", new { codigosBultosDevueltos = Array.Empty<string>() })).StatusCode);
    }
}
