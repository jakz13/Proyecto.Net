using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.DescartarAviso;

public class DescartarAvisoCommandHandler : IRequestHandler<DescartarAvisoCommand>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DescartarAvisoCommandHandler(IAvisoComercioRepository avisoRepository, IUnitOfWork unitOfWork)
    {
        _avisoRepository = avisoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DescartarAvisoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AvisoId == Guid.Empty)
            throw new DomainValidationException("El identificador del aviso no puede ser vacío.");

        // GetAvisoByIdAsync está acotado al operador: un aviso de otro inquilino se ve como inexistente
        var aviso = await _avisoRepository.GetAvisoByIdAsync(request.AvisoId, cancellationToken)
            ?? throw new NotFoundException($"El aviso '{request.AvisoId}' no fue encontrado.");

        aviso.Descartar(request.Motivo);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
