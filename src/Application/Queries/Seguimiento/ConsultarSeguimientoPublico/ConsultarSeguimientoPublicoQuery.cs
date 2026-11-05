using MediatR;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;

/// <summary>Consulta anónima del estado de un envío por su código de seguimiento (UC-33, RF-35).</summary>
public record ConsultarSeguimientoPublicoQuery(string CodigoSeguimiento) : IRequest<SeguimientoPublicoDto?>;

/// <summary>
/// Vista pública del envío. Muestra el avance de entrega a nivel de bultos (RF-35) sin exponer nombres de terceros,
/// direcciones, documentos ni identificadores internos de bultos.
/// </summary>
public record SeguimientoPublicoDto(
    string CodigoSeguimiento,
    EstadoEnvio EstadoActual,
    string? VentanaHorariaEstimada,
    DateTime FechaUltimaActualizacion,
    bool PermiteReprogramacion,
    IReadOnlyList<HitoSeguimientoDto> HistorialEventos,
    int TotalBultos = 0,
    int BultosEntregados = 0,
    int BultosPendientes = 0,
    string? AvanceEntregaBultos = null);

public record HitoSeguimientoDto(DateTime Fecha, EstadoEnvio Estado, string DescripcionPublica);
