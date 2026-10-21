using System.Net;

namespace UltimaMilla.Frontend.Tests.Helpers;

/// <summary>
/// Manejador HTTP simulado para pruebas unitarias de integración de clientes HTTP sin requerir red.
/// </summary>
public class ManejadorMensajesHttpMock : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _manejador;

    public ManejadorMensajesHttpMock(Func<HttpRequestMessage, Task<HttpResponseMessage>> manejador)
    {
        _manejador = manejador ?? throw new ArgumentNullException(nameof(manejador));
    }

    public static ManejadorMensajesHttpMock ConRespuesta(HttpStatusCode estado, string contenidoJson = "{}")
    {
        return new ManejadorMensajesHttpMock(_ => Task.FromResult(new HttpResponseMessage(estado)
        {
            Content = new StringContent(contenidoJson, System.Text.Encoding.UTF8, "application/json")
        }));
    }

    public static ManejadorMensajesHttpMock ConExcepcion(Exception excepcion)
    {
        return new ManejadorMensajesHttpMock(_ => Task.FromException<HttpResponseMessage>(excepcion));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _manejador(request);
    }
}
