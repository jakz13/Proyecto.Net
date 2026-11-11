using UltimaMilla.Frontend.Contracts.Modelos.Retiros;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Retiro en domicilio (cambio de requerimientos 2.2): el comercio lo solicita desde su portal y el operador lo planifica y lo sigue
/// desde el backoffice.
/// </summary>
public interface IRetirosApi
{
    // ----- Portal del comercio -----
    Task<ResultadoSolicitudRetiroDto> SolicitarAsync(SolicitarRetiroDto solicitud, CancellationToken cancelacion = default);
    Task<PaginaRetirosDto> ListarDelComercioAsync(string? estado = null, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto?> ObtenerDelComercioAsync(Guid id, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> CancelarDelComercioAsync(Guid id, string motivo, CancellationToken cancelacion = default);

    // ----- Backoffice del operador -----
    Task<ResumenRetirosOperadorDto> ListarDelOperadorAsync(string? estado = null, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto?> ObtenerDelOperadorAsync(Guid id, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> RecibirEnDepositoAsync(Guid id, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> EntregarAlComercioAsync(Guid id, string? receptor, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> ReprogramarAsync(Guid id, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> SacarDeRutaAsync(Guid id, CancellationToken cancelacion = default);
    Task<RetiroDetalleDto> CancelarDelOperadorAsync(Guid id, string motivo, CancellationToken cancelacion = default);
}
