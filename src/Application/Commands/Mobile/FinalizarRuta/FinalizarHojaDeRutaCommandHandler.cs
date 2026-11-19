using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.FinalizarRuta;

/// <summary>
/// Finaliza la hoja de ruta desde la app: queda "Finalizada" a la espera de que el operario la rinda en el depósito.
/// Lo que el repartidor declara devolver viaja en el evento de integración; el estado de los envíos no cambia hasta la rendición.
/// </summary>
public class FinalizarHojaDeRutaCommandHandler : IRequestHandler<FinalizarHojaDeRutaCommand, FinalizacionResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public FinalizarHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null)
    {
        _rutaRepository = rutaRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<FinalizacionResultDto> Handle(FinalizarHojaDeRutaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var declarados = (request.CodigosBultosDevueltos ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        // Idempotente: reenviar la finalización (reintento de la cola offline) no cambia nada
        if (ruta.Estado is "Finalizada" or "Rendida")
        {
            var mensajeYa = ruta.Estado == "Rendida"
                ? "La hoja de ruta ya fue rendida en el depósito."
                : "La hoja de ruta ya estaba finalizada y espera su rendición en el depósito.";
            return new FinalizacionResultDto(ruta.Id, ruta.Estado, declarados.Count, true, mensajeYa);
        }

        var paradasVigentes = ruta.Paradas.Where(p => p.Estado != "Cancelada").ToList();
        var soloRetiros = paradasVigentes.Count > 0 && paradasVigentes.All(p => p.EsRetiro);

        var puedeFinalizar = ruta.Estado is "Despachada" or "EnTransito"
            || (soloRetiros && ruta.Estado is "Liberada" or "EnCarga");
        if (!puedeFinalizar)
            throw new DomainValidationException($"Solo se puede finalizar una hoja de ruta despachada o en tránsito (estado actual: '{ruta.Estado}').");

        ruta.Finalizar();

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(EventosIntegracion.DeRuta(EventosIntegracion.Tipos.RutaFinalizada, ruta, new
        {
            BultosDeclaradosADevolver = declarados,
            request.Observaciones,
            Timestamp = _clock.UtcNow
        }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte la finalización ya persistida
            }
        }

        return new FinalizacionResultDto(
            ruta.Id,
            ruta.Estado,
            declarados.Count,
            false,
            "Hoja de ruta finalizada. Presente los bultos que vuelven en el depósito para su rendición.");
    }
}
