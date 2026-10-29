using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.ActualizarMotivoNoEntrega;

public class ActualizarMotivoNoEntregaCommandHandler : IRequestHandler<ActualizarMotivoNoEntregaCommand, MotivoNoEntregaDto>
{
    private readonly IMotivoNoEntregaRepository _motivos;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public ActualizarMotivoNoEntregaCommandHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant currentTenant, IUnitOfWork unitOfWork)
    {
        _motivos = motivos;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<MotivoNoEntregaDto> Handle(ActualizarMotivoNoEntregaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MotivoId == Guid.Empty)
            throw new DomainValidationException("El identificador del motivo es inválido.");

        var motivo = await _motivos.GetByIdAsync(request.MotivoId, cancellationToken)
            ?? throw new NotFoundException($"Motivo '{request.MotivoId}' no encontrado.");
        if (motivo.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el motivo no corresponde al operador actual.");

        motivo.ModificarDescripcion(request.Descripcion ?? string.Empty);
        if (request.Activo.HasValue)
        {
            if (request.Activo.Value)
                motivo.Activar();
            else
                motivo.Desactivar();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperadorMapper.ToDto(motivo);
    }
}
