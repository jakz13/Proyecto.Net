using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;

public record CrearSuscripcionAvisoRequest(
    Guid? ComercioOperadorId,
    string TipoEvento,
    string UrlWebhook,
    string ClaveFirma);

public record CrearSuscripcionAvisoCommand(
    Guid ComercioOperadorId,
    string TipoEvento,
    string UrlWebhook,
    string ClaveFirma) : IRequest<SuscripcionAvisoDto>;

public record ListarSuscripcionesAvisoQuery(
    Guid? ComercioOperadorId = null) : IRequest<IReadOnlyList<SuscripcionAvisoDto>>;

public record DesactivarSuscripcionAvisoCommand(
    Guid SuscripcionId,
    Guid? ComercioOperadorId = null) : IRequest<SuscripcionAvisoDto>;

public record SuscripcionAvisoDto(
    Guid Id,
    Guid ComercioOperadorId,
    string TipoEvento,
    string UrlWebhook,
    bool Activa);

public record ListarHistorialAvisosQuery(
    Guid? ComercioOperadorId = null,
    string? Estado = null,
    int Limite = 50) : IRequest<IReadOnlyList<AvisoComercioHistorialDto>>;

public record ReenviarAvisoManualCommand(
    Guid AvisoId) : IRequest<AvisoComercioHistorialDto>;

public record AvisoComercioHistorialDto(
    Guid Id,
    Guid SuscripcionAvisoId,
    Guid EnvioId,
    string Estado,
    int Intentos,
    DateTime? ProximoReintento,
    string? UltimaRespuestaHttp,
    DateTime Timestamp);

