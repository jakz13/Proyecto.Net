using MediatR;

namespace UltimaMilla.Application.Queries.Comercio.ConsultarHistorialAvisos;

/// <summary>
/// Consulta el historial de avisos enviados hacia los sistemas del comercio con soporte de filtros (UC-30, RF-28).
/// </summary>
public record ConsultarHistorialAvisosQuery(
    string? Estado = null,
    Guid? EnvioId = null,
    string? CodigoSeguimiento = null) : IRequest<IReadOnlyList<AvisoHistorialDetalleDto>>;

public record AvisoHistorialDetalleDto(
    Guid Id,
    Guid SuscripcionAvisoId,
    Guid EnvioId,
    string CodigoSeguimiento,
    string TipoEvento,
    string UrlWebhook,
    string Payload,
    string FirmaEnviada,
    string Estado,
    int Intentos,
    string? UltimaRespuestaHttp,
    int CodigoRespuestaHttp,
    DateTime Timestamp,
    DateTime? ProximoReintento);

