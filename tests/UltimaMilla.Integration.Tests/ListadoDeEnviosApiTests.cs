using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

[Collection(ColeccionApi.Nombre)]
public class ListadoDeEnviosApiTests
{
    private readonly ApiFactory _factory;

    public ListadoDeEnviosApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task ElListadoDeEnvios_IncluyeElNombreDelComercioRemitente()
    {
        // Antes siempre venía vacío y el Backoffice mostraba "Comercio General" en todos los envíos
        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);
        var envios = await despachador.GetFromJsonAsync<JsonElement>("/api/envios");

        Assert.True(envios.GetArrayLength() > 0, "La base de pruebas no tiene envíos.");
        foreach (var envio in envios.EnumerateArray())
        {
            var nombre = envio.GetProperty("comercioNombre");
            Assert.True(nombre.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nombre.GetString()),
                $"El envío {envio.GetProperty("codigoSeguimiento")} no trae el nombre de su comercio.");
        }
    }
}

[Collection(ColeccionApi.Nombre)]
public class IncidenciaConCodigoDeEnvioApiTests
{
    private readonly ApiFactory _factory;

    public IncidenciaConCodigoDeEnvioApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [FactPostgres]
    public async Task RegistrarIncidenciaConElCodigoDeSeguimiento_LaVinculaConElEnvio()
    {
        // La pantalla de Envíos solo conoce el código; antes la incidencia quedaba sin envío asociado
        var comercio = _factory.CreateClient();
        var alta = await comercio.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Incidencia " + Guid.NewGuid().ToString("N")[..6], calle = "Av. Rivera", numero = "2500", ciudad = "Montevideo",
            bultos = new[] { new { peso = 1, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(System.Net.HttpStatusCode.Created, alta.StatusCode);
        var envio = await alta.Content.ReadFromJsonAsync<JsonElement>();

        var despachador = await UsuariosDePrueba.DespachadorAsync(_factory);
        var api = new UltimaMilla.Frontend.Infrastructure.Http.HttpIncidenciasApi(despachador);
        var creada = await api.RegistrarIncidenciaAsync(new UltimaMilla.Frontend.Contracts.Modelos.Incidencias.RegistrarIncidenciaDto
        {
            Tipo = "Otro", Severidad = "Media", Descripcion = "Prueba de vínculo con el envío",
            CodigoSeguimientoEnvio = envio.GetProperty("codigoSeguimiento").GetString()
        }, "prueba");

        Assert.Equal(envio.GetProperty("id").GetGuid(), creada.EnvioAfectadoId);
    }
}
