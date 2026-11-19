using System.Net;
using System.Text;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Infrastructure.Http;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// La marca "interno" del motivo de no entrega viaja completa entre el Backoffice y la API. Antes el cliente web ni la leía ni la enviaba:
/// el motivo se creaba siempre como no interno y el panel de reasignación en ruta (que exige uno interno activo) lo rechazaba
/// con "no es un motivo interno activo", aunque estuviera cargado y activo.
/// </summary>
public class MotivosInternosDelBackofficeTests
{
    private sealed class Manejador : HttpMessageHandler
    {
        public string? CuerpoEnviado { get; private set; }
        public string Respuesta { get; init; } = "[]";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancelacion)
        {
            if (solicitud.Content is not null)
                CuerpoEnviado = await solicitud.Content.ReadAsStringAsync(cancelacion);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") };
        }
    }

    private static HttpMotivosNoEntregaApi Api(Manejador m) => new(new HttpClient(m) { BaseAddress = new Uri("http://api.local") });

    [Fact]
    public async Task El_listado_conserva_la_marca_de_motivo_interno()
    {
        var api = Api(new Manejador { Respuesta = "[{\"id\":\"" + Guid.NewGuid() + "\",\"codigo\":\"RETORNO_SIN_INTENTO\",\"descripcion\":\"Retorno\",\"activo\":true,\"esInterno\":true}]" });

        var motivos = await api.ListarMotivosAsync();

        Assert.True(Assert.Single(motivos).EsInterno);
    }

    [Fact]
    public async Task El_alta_envia_la_marca_de_motivo_interno()
    {
        var manejador = new Manejador { Respuesta = "{\"id\":\"" + Guid.NewGuid() + "\",\"codigo\":\"AVERIA\",\"descripcion\":\"Avería\",\"activo\":true,\"esInterno\":true}" };

        await Api(manejador).CrearMotivoAsync(new CrearMotivoNoEntregaDto { Codigo = "AVERIA", Descripcion = "Avería del vehículo", EsInterno = true });

        Assert.Contains("\"esInterno\":true", manejador.CuerpoEnviado, StringComparison.OrdinalIgnoreCase);
    }
}
