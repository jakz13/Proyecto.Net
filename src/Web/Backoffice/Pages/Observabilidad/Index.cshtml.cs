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

    public void OnGet()
    {
        var dominioPanel = _configuracion["Observabilidad:UrlGrafana"];
        if (!string.IsNullOrWhiteSpace(dominioPanel))
        {
            UrlGrafana = dominioPanel;
        }

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

