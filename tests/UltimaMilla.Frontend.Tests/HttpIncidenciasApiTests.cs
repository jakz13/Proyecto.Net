using System.Net;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Frontend.Tests.Helpers;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias de HttpIncidenciasApi para la consulta de conflictos de sincronización offline.
/// </summary>
public class HttpIncidenciasApiTests
{
    private const string UrlBase = "http://localhost:5000";

    [Fact]
    public async Task ListarConflictosSincronizacion_ConRespuestaExitosa_DeserializaTodosLosCampos()
    {
        const string json = """
        [
            {
                "id": "11111111-1111-1111-1111-111111111111",
                "timestamp": "2026-11-15T18:30:00Z",
                "entidadAfectada": "Envio",
                "entidadId": "22222222-2222-2222-2222-222222222222",
                "codigoSeguimiento": "ENV-2026-00108",
                "estadoDispositivo": "Entregado",
                "estadoServidor": "Cancelado",
                "politicaAplicada": "PrevaleceEntregaFisicaConEvidencia",
                "resolucion": "Prevalece entrega física con evidencia."
            }
        ]
        """;

        Uri? rutaCapturada = null;
        var handler = new ManejadorMensajesHttpMock(req =>
        {
            rutaCapturada = req.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        });

        var api = new HttpIncidenciasApi(new HttpClient(handler) { BaseAddress = new Uri(UrlBase) });

        var resultado = await api.ListarConflictosSincronizacionAsync(500);

        Assert.Equal("/api/backoffice/incidencias/conflictos?maximo=200", rutaCapturada?.PathAndQuery);
        var item = Assert.Single(resultado);
        Assert.Equal("ENV-2026-00108", item.CodigoSeguimiento);
        Assert.Equal("PrevaleceEntregaFisicaConEvidencia", item.PoliticaAplicada);
        Assert.Equal("Cancelado", item.EstadoServidor);
        Assert.Equal("Entregado", item.EstadoDispositivo);
    }

    [Fact]
    public async Task ListarConflictosSincronizacion_CuandoEndpointNoExiste_RetornaListaVacia()
    {
        var handler = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.NotFound, "");
        var api = new HttpIncidenciasApi(new HttpClient(handler) { BaseAddress = new Uri(UrlBase) });

        var resultado = await api.ListarConflictosSincronizacionAsync();

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task ListarConflictosSincronizacion_SinAutorizacion_LanzaApiExcepcion()
    {
        var handler = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.Forbidden, "{\"error\":\"Sin permiso\"}");
        var api = new HttpIncidenciasApi(new HttpClient(handler) { BaseAddress = new Uri(UrlBase) });

        var ex = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ListarConflictosSincronizacionAsync());
        Assert.Equal(HttpStatusCode.Forbidden, ex.CodigoEstado);
    }
}
