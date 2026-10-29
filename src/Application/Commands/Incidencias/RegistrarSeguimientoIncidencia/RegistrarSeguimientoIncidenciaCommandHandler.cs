using MediatR;
using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Incidencias.RegistrarSeguimientoIncidencia;

public class RegistrarSeguimientoIncidenciaCommandHandler : IRequestHandler<RegistrarSeguimientoIncidenciaCommand, IncidenciaResultDto>
{
    private readonly IIncidenciaRepository _incidenciaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;

    public RegistrarSeguimientoIncidenciaCommandHandler(
        IIncidenciaRepository incidenciaRepository,
        ICurrentTenant currentTenant,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOutboxStorage outboxStorage)
    {
        _incidenciaRepository = incidenciaRepository;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _outboxStorage = outboxStorage;
    }

    public async Task<IncidenciaResultDto> Handle(RegistrarSeguimientoIncidenciaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IncidenciaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la incidencia es inválido.");

        if (!request.TomarACargo && string.IsNullOrWhiteSpace(request.Comentario))
            throw new DomainValidationException("Indique un comentario o tome la incidencia a su cargo.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        var incidencia = await _incidenciaRepository.GetByIdAsync(request.IncidenciaId, cancellationToken)
            ?? throw new NotFoundException($"Incidencia '{request.IncidenciaId}' no encontrada.");
        if (incidencia.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la incidencia no corresponde al operador actual.");

        if (request.TomarACargo)
            incidencia.AsignarResponsable(usuarioId, _clock.UtcNow);

        if (!string.IsNullOrWhiteSpace(request.Comentario))
            incidencia.AgregarNota(usuarioId, request.Comentario, _clock.UtcNow);

        await _outboxStorage.SaveAsync(EventosIntegracion.DeIncidencia(EventosIntegracion.Tipos.IncidenciaConSeguimiento, incidencia), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return IncidenciaMapper.ToResult(incidencia);
    }
}
