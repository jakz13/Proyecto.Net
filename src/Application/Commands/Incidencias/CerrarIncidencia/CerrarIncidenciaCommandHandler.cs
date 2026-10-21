using MediatR;
using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Incidencias.CerrarIncidencia;

public class CerrarIncidenciaCommandHandler : IRequestHandler<CerrarIncidenciaCommand, IncidenciaResultDto>
{
    private readonly IIncidenciaRepository _incidenciaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CerrarIncidenciaCommandHandler(
        IIncidenciaRepository incidenciaRepository,
        ICurrentTenant currentTenant,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _incidenciaRepository = incidenciaRepository;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IncidenciaResultDto> Handle(CerrarIncidenciaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IncidenciaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la incidencia es inválido.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        var incidencia = await _incidenciaRepository.GetByIdAsync(request.IncidenciaId, cancellationToken)
            ?? throw new NotFoundException($"Incidencia '{request.IncidenciaId}' no encontrada.");
        if (incidencia.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la incidencia no corresponde al operador actual.");

        incidencia.Cerrar(usuarioId, request.Resolucion ?? string.Empty, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return IncidenciaMapper.ToResult(incidencia);
    }
}
