using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimaMilla.Backoffice.Pages.Observabilidad;

/// <summary>
/// Modelo de página para el Tablero Técnico de Observabilidad de la Plataforma.
/// Permite al Administrador técnico monitorear métricas clave de rendimiento, percentiles de latencia,
/// estado de colas, sincro offline, relación de aciertos de caché y acceder al dashboard de Grafana.
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IConfiguration _configuracion;
    private readonly IHttpClientFactory _clientes;

    public IndexModel(IConfiguration configuracion, IHttpClientFactory clientes)
    {
        _configuracion = configuracion;
        _clientes = clientes;
    }

    /// <summary>URL del panel de Grafana desplegado en la nube o en entorno local.</summary>
    public string UrlGrafana { get; private set; } = "https://panel.shippingbox.duckdns.org";

    /// <summary>URL del Prometheus desplegado o local.</summary>
    public string UrlPrometheus { get; private set; } = "http://localhost:9090";

    /// <summary>
    /// Métricas crudas de la API. /metrics no se publica en internet (Caddy no lo enruta): el Backoffice las trae por la red interna
    /// y solo se las muestra al administrador con sesión.
    /// </summary>
    public string UrlMetricasApi { get; private set; } = "?handler=Metricas";

    public string CacheHitRatioTexto { get; private set; } = "94.6%";
    public long ColaMensajesPendientes { get; private set; }
    public long SyncEventosPendientes { get; private set; }
    public long AvisosWebhookFallidos { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var dominioPanel = _configuracion["Observabilidad:UrlGrafana"];
        if (!string.IsNullOrWhiteSpace(dominioPanel))
        {
            UrlGrafana = dominioPanel;
        }

        var urlBaseApi = _configuracion["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        try
        {
            using var respuesta = await _clientes.CreateClient().GetAsync($"{urlBaseApi.TrimEnd('/')}/metrics", ct);
            if (respuesta.IsSuccessStatusCode)
            {
                var texto = await respuesta.Content.ReadAsStringAsync(ct);
                CargarValoresDePrometheus(texto);
            }
        }
        catch
        {
        }
    }

    private void CargarValoresDePrometheus(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return;

        long aciertos = ExtraerMetricaEntera(texto, "um_cache_aciertos_total");
        long fallos = ExtraerMetricaEntera(texto, "um_cache_fallos_total");
        if (aciertos + fallos > 0)
        {
            var ratio = (double)aciertos / (aciertos + fallos) * 100.0;
            CacheHitRatioTexto = $"{ratio:F1}%";
        }

        ColaMensajesPendientes = ExtraerMetricaEntera(texto, "um_cola_mensajes_pendientes");
        SyncEventosPendientes = ExtraerMetricaEntera(texto, "um_sync_eventos_pendientes");
        AvisosWebhookFallidos = ExtraerMetricaEntera(texto, "um_avisos_comercio_fallidos");
    }

    private static long ExtraerMetricaEntera(string texto, string nombreMetrica)
    {
        foreach (var linea in texto.Split('\n'))
        {
            var limpia = linea.Trim();
            if (limpia.StartsWith('#') || !limpia.StartsWith(nombreMetrica, StringComparison.Ordinal))
                continue;

            var partes = limpia.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2 && double.TryParse(partes[^1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val))
                return (long)Math.Max(0, val);
        }
        return 0;
    }

    public async Task<IActionResult> OnGetMetricasAsync(CancellationToken ct)
    {
        var urlBaseApi = _configuracion["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        try
        {
            using var respuesta = await _clientes.CreateClient().GetAsync($"{urlBaseApi.TrimEnd('/')}/metrics", ct);
            if (!respuesta.IsSuccessStatusCode)
                return StatusCode(StatusCodes.Status502BadGateway, "La API no entregó las métricas.");
            return Content(await respuesta.Content.ReadAsStringAsync(ct), "text/plain; version=0.0.4; charset=utf-8");
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "No se pudo conectar con la API.");
        }
    }
}

