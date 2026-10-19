using UltimaMilla.Frontend.Contracts.Modelos.Reportes;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de API para la consulta y exportación de reportes de gestión operativa y comercial (UC-13, RF-30).
/// </summary>
public interface IReportesApi
{
    /// <summary>
    /// Genera la información agregada y desglosada del reporte según los filtros de tipo y período seleccionados.
    /// </summary>
    Task<ResultadoReporteDto> GenerarReporteAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default);

    /// <summary>
    /// Exporta los datos consolidados del reporte en formato CSV descargable.
    /// </summary>
    Task<byte[]> ExportarReporteCsvAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default);
}
