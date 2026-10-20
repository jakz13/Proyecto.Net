using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Incidencias;

namespace UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;

public class AbrirIncidenciaCommandHandler : IRequestHandler<AbrirIncidenciaCommand, IncidenciaResultDto>
{
    private readonly IIncidenciaRepository _incidenciaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AbrirIncidenciaCommandHandler(
        IIncidenciaRepository incidenciaRepository,
        IEnvioRepository envioRepository,
        IRutaRepository rutaRepository,
        ICurrentTenant currentTenant,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _incidenciaRepository = incidenciaRepository;
        _envioRepository = envioRepository;
        _rutaRepository = rutaRepository;
        _currentTenant = currentTenant;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<IncidenciaResultDto> Handle(AbrirIncidenciaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        // El envío y la ruta afectados deben pertenecer al operador actual
        if (request.EnvioId.HasValue)
        {
            var envio = await _envioRepository.GetByIdAsync(request.EnvioId.Value, cancellationToken)
                ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");
            if (envio.OperadorId != _currentTenant.OperadorId)
                throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");
        }

        if (request.HojaDeRutaId.HasValue)
        {
            var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId.Value, cancellationToken)
                ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");
            if (ruta.OperadorId != _currentTenant.OperadorId)
                throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");
        }

        var incidencia = new Incidencia(
            Guid.NewGuid(),
            _currentTenant.OperadorId,
            request.EnvioId,
            request.Tipo,
            request.Descripcion,
            request.Severidad ?? "Media",
            request.HojaDeRutaId,
            usuarioId,
            _clock.UtcNow);

        await _incidenciaRepository.AddAsync(incidencia, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return IncidenciaMapper.ToResult(incidencia);
    }
}
