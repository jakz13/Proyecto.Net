using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.ActualizarMotivoNoEntrega;

/// <summary>UC-05: modifica la descripción y opcionalmente el estado activo de un motivo existente.</summary>
public record ActualizarMotivoNoEntregaCommand(Guid MotivoId, string? Descripcion, bool? Activo = null) : IRequest<MotivoNoEntregaDto>;
