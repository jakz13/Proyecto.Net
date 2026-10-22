using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.CambiarEstadoMotivoNoEntrega;

/// <summary>UC-05: activa o desactiva un motivo (nunca se borra físicamente). Es idempotente.</summary>
public record CambiarEstadoMotivoNoEntregaCommand(Guid MotivoId, bool Activo) : IRequest<MotivoNoEntregaDto>;