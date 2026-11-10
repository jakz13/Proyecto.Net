using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace UltimaMilla.Backoffice.Pages.Observabilidad;

/// <summary>
/// Modelo de página para el Tablero Técnico de Observabilidad de la Plataforma (UC-44, §6.12).
/// Permite al Administrador técnico monitorear métricas clave de rendimiento, percentiles de latencia,
/// estado de colas, sincro offline, relación de aciertos de caché y acceder al dashboard de Grafana.
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IConfiguration _configuracion;

    public IndexModel(IConfiguration configuracion)
    {
        _configuracion = configuracion;
    }

    /// <summary>URL del panel de Grafana desplegado en la nube o en entorno local.</summary>
    public string UrlGrafana { get; private set; } = "https://panel.shippingbox.duckdns.org";

    /// <summary>URL del Prometheus desplegado o local.</summary>
    public string UrlPrometheus { get; private set; } = "http://localhost:9090";

    /// <summary>URL del endpoint de métricas OpenTelemetry de la API.</summary>
    public string UrlMetricasApi { get; private set; } = "http://localhost:5000/metrics";

    public void OnGet()
    {
        var dominioPanel = _configuracion["Observabilidad:UrlGrafana"];
        if (!string.IsNullOrWhiteSpace(dominioPanel))
        {
            UrlGrafana = dominioPanel;
        }

        var urlBaseApi = _configuracion["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        UrlMetricasApi = $"{urlBaseApi.TrimEnd('/')}/metrics";
    }
}

