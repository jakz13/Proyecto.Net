using System.Net.Http.Headers;
using System.Text;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Messaging;

/// <summary>
/// Llamada HTTP saliente firmada hacia el webhook de un comercio. No sigue redirecciones, acota el tiempo de espera
/// (configurado en el <see cref="HttpClient"/>) y nunca devuelve el cuerpo de la respuesta del tercero: solo el código.
/// </summary>
public sealed class HttpWebhookSender : IWebhookSender
{
    private const int LargoMaximoDetalle = 500;

    private readonly HttpClient _http;

    public HttpWebhookSender(HttpClient http)
    {
        _http = http;
    }

    public async Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(urlWebhook, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return new ResultadoWebhook(false, null, "La URL del webhook no es una dirección http o https válida.");

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };
        solicitud.Headers.TryAddWithoutValidation(FirmaWebhook.CabeceraFirma, firma);
        solicitud.Headers.TryAddWithoutValidation(FirmaWebhook.CabeceraIdAviso, avisoId.ToString());

        try
        {
            using var respuesta = await _http.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var codigo = (int)respuesta.StatusCode;
            return new ResultadoWebhook(respuesta.IsSuccessStatusCode, codigo, $"HTTP {codigo} {respuesta.ReasonPhrase}".Trim());
        }
        catch (HttpRequestException ex)
        {
            return new ResultadoWebhook(false, ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : null, Acotar($"Error de red: {ex.Message}"));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ResultadoWebhook(false, null, "Tiempo de espera agotado esperando la respuesta del comercio.");
        }
    }

    private static string Acotar(string texto) => texto.Length <= LargoMaximoDetalle ? texto : texto[..LargoMaximoDetalle];
}
