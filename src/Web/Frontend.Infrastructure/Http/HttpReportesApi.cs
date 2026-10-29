using System.Globalization;
using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>Cliente HTTP de los reportes de gestión (UC-13, RF-30).</summary>
public class HttpReportesApi : IReportesApi
{
    private readonly HttpClient _http;

    public HttpReportesApi(HttpClient http)
    {
        _http = http;
    }

    public async Task<ResultadoReporteDto> GenerarReporteAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var respuesta = await _http.GetAsync(ConstruirRuta(filtro), cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(cuerpo) ? $"Error HTTP {(int)respuesta.StatusCode}" : cuerpo);
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ResultadoReporteDto>(cancellationToken: cancelacion)
            ?? throw new ApiExcepcion("La API devolvió un reporte vacío.");

        // La pantalla conserva el preajuste elegido (la API solo conoce el rango de fechas)
        return resultado with { Filtro = resultado.Filtro with { Preajuste = filtro.Preajuste } };
    }

    public async Task<byte[]> ExportarReporteCsvAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default)
    {
        var reporte = await GenerarReporteAsync(filtro, cancelacion);
        return ReporteCsv.Generar(reporte);
    }

    private static string ConstruirRuta(FiltroReporteDto filtro)
    {
        var parametros = new List<string>
        {
            $"tipo={Uri.EscapeDataString(filtro.TipoReporte)}",
            $"desde={filtro.FechaDesde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            $"hasta={filtro.FechaHasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
        };

        if (filtro.ComercioId.HasValue)
            parametros.Add($"comercioId={filtro.ComercioId}");
        if (filtro.RepartidorId.HasValue)
            parametros.Add($"repartidorId={filtro.RepartidorId}");
        if (!string.IsNullOrWhiteSpace(filtro.Zona))
            parametros.Add($"zona={Uri.EscapeDataString(filtro.Zona)}");

        return "api/reportes?" + string.Join('&', parametros);
    }
}
