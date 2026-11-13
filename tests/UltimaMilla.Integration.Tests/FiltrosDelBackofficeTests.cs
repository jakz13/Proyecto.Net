using System.Net;
using System.Text;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Infrastructure.Http;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// El listado de hojas de ruta del Backoffice llega con el estado "todos" por defecto: ese valor no es un estado y no debe viajar a la API
/// (antes se enviaba <c>estado=todos</c>, la API no encontraba ninguna ruta y la pantalla de Hojas de Ruta aparecía siempre vacía).
/// </summary>
public class FiltrosDelBackofficeTests
{
    private sealed class ManejadorQueRegistra : HttpMessageHandler
    {
        public List<string> Direcciones { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            Direcciones.Add(solicitud.RequestUri!.PathAndQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
        }
    }

    [Theory]
    [InlineData("todos")]
    [InlineData("Todos")]
    [InlineData("TODOS")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ListarHojasDeRuta_ConElFiltroTodos_NoEnviaEstadoALaApi(string? estado)
    {
        var manejador = new ManejadorQueRegistra();
        var api = new HttpRutasApi(new HttpClient(manejador) { BaseAddress = new Uri("http://api.local") });

        await api.ListarHojasDeRutaAsync(new FiltroRutasDto { Estado = estado });

        Assert.DoesNotContain(manejador.Direcciones, d => d.Contains("estado=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListarHojasDeRuta_ConUnEstadoConcreto_LoEnviaALaApi()
    {
        var manejador = new ManejadorQueRegistra();
        var api = new HttpRutasApi(new HttpClient(manejador) { BaseAddress = new Uri("http://api.local") });

        await api.ListarHojasDeRutaAsync(new FiltroRutasDto { Estado = "Liberada" });

        Assert.Contains(manejador.Direcciones, d => d.Contains("estado=Liberada"));
    }

    [Theory]
    [InlineData(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto.EnDeposito, "EnDeposito")]
    [InlineData(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto.Admitido, "Admitido")]
    [InlineData(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto.EntregadoParcialmente, "EntregadoParcialmente")]
    public async Task ListarEnviosDelComercio_EnviaElNombreDelEstadoYNoSuNumero(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto estado, string nombreEsperado)
    {
        var manejador = new ManejadorQueRegistra();
        var api = new UltimaMilla.Frontend.Infrastructure.Http.HttpEnviosApi(new HttpClient(manejador) { BaseAddress = new Uri("http://api.local") });

        try
        {
            await api.ListarEnviosAsync(new UltimaMilla.Frontend.Contracts.Modelos.Envios.FiltroEnviosDto { SoloEnviosPropiosComercio = true, Estado = estado });
        }
        catch (Exception)
        {
            // la respuesta de la maqueta no es el JSON paginado: solo interesa la dirección que se pidió
        }

        Assert.Contains(manejador.Direcciones, d => d.Contains($"estado={nombreEsperado}"));
    }
}
