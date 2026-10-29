using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Deposito.MarcarEnvioExtraviado;

public class MarcarEnvioExtraviadoCommandHandler : IRequestHandler<MarcarEnvioExtraviadoCommand, MarcarEnvioExtraviadoResultDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public MarcarEnvioExtraviadoCommandHandler(
        IEnvioRepository envioRepository,
        IRutaRepository rutaRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        ITableroRealtimeNotifier? tableroNotifier = null)
    {
        _envioRepository = envioRepository;
        _rutaRepository = rutaRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<MarcarEnvioExtraviadoResultDto> Handle(MarcarEnvioExtraviadoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Anti-IDOR: el envío debe pertenecer al operador del usuario autenticado
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        // Si el envío está en una ruta, se valida y se retira de ella antes de cambiar su estado, para no dejar nada a medias
        var retiradoDeRuta = false;
        Guid? rutaRetiradaId = null;
        string? estadoRutaRetirada = null;
        if (envio.HojaDeRutaId is { } rutaId)
        {
            var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(rutaId, cancellationToken);
            var paradaAbierta = ruta?.Paradas.Any(p => p.EnvioId == envio.Id && p.Estado is not ("Cancelada" or "Entregada" or "Fallida")) == true;
            if (ruta is not null && paradaAbierta)
            {
                ruta.RetirarEnvio(envio.Id);
                await _rutaRepository.UpdateAsync(ruta, cancellationToken);
                retiradoDeRuta = true;
                rutaRetiradaId = ruta.Id;
                estadoRutaRetirada = ruta.Estado;
            }
        }

        envio.MarcarExtraviado(request.Motivo, usuarioId);

        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _outboxStorage.SaveAsync(
            EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioExtraviado, envio, new { Motivo = request.Motivo.Trim(), RetiradoDeRuta = retiradoDeRuta }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (retiradoDeRuta && rutaRetiradaId is { } rutaAfectadaId && _tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(envio.OperadorId, rutaAfectadaId, estadoRutaRetirada ?? "Despachada", cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida el extravío ya persistido
            }
        }

        return new MarcarEnvioExtraviadoResultDto(envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), retiradoDeRuta);
    }
}
