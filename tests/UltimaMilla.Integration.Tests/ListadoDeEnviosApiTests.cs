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
