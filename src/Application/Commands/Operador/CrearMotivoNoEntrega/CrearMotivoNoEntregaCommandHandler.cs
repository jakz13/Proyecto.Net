using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.CrearMotivoNoEntrega;

public class CrearMotivoNoEntregaCommandHandler : IRequestHandler<CrearMotivoNoEntregaCommand, MotivoNoEntregaDto>
{
    private readonly IMotivoNoEntregaRepository _motivos;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public CrearMotivoNoEntregaCommandHandler(IMotivoNoEntregaRepository motivos, ICurrentTenant currentTenant, IUnitOfWork unitOfWork)
    {
        _motivos = motivos;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<MotivoNoEntregaDto> Handle(CrearMotivoNoEntregaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var operadorId = _currentTenant.OperadorId;
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), operadorId, request.Codigo ?? string.Empty, request.Descripcion ?? string.Empty, request.EsInterno);

        if (await _motivos.ExisteCodigoAsync(operadorId, motivo.Codigo, cancellationToken))
            throw new DomainValidationException($"Ya existe un motivo con el código '{motivo.Codigo}'.");

        await _motivos.AddAsync(motivo, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return OperadorMapper.ToDto(motivo);
    }
}