using MediatR;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;

/// <summary>Consulta anónima del estado de un envío por su código de seguimiento (UC-33).</summary>
public record ConsultarSeguimientoPublicoQuery(string CodigoSeguimiento) : IRequest<SeguimientoPublicoDto?>;

/// <summary>
/// Vista pública del envío. No incluye nombres de terceros, direcciones, documentos ni identificadores internos.
/// </summary>
public record SeguimientoPublicoDto(
    string CodigoSeguimiento,
    EstadoEnvio EstadoActual,
    string? VentanaHorariaEstimada,
    DateTime FechaUltimaActualizacion,
    bool PermiteReprogramacion,
    IReadOnlyList<HitoSeguimientoDto> HistorialEventos);

public record HitoSeguimientoDto(DateTime Fecha, EstadoEnvio Estado, string DescripcionPublica);
