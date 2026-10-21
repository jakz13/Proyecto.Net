using MediatR;

namespace UltimaMilla.Application.Commands.Seguimiento.SolicitarReprogramacion;

/// <summary>El destinatario pide otra franja horaria desde el seguimiento público (sin autenticarse).</summary>
public record SolicitarReprogramacionCommand(
    string? CodigoSeguimiento,
    Guid FranjaHorariaId,
    string? OrigenIp = null
) : IRequest<ResultadoReprogramacionDto>;

/// <summary>Resultado de evaluar la solicitud; un rechazo es un resultado normal y trae su motivo.</summary>
public record ResultadoReprogramacionDto(
    bool Aceptada,
    string Estado,
    string? Motivo,
    string? FranjaAsignada);
