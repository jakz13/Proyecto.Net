using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Planificacion.ReasignarEnvio;

public class ReasignarEnvioCommandHandler : IRequestHandler<ReasignarEnvioCommand, ReasignacionResultDto>
{
    private const string Retirar = "Retirar";
    private const string ReasignarARuta = "ReasignarARuta";
    private const string Reprogramar = "Reprogramar";

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public ReasignarEnvioCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<ReasignacionResultDto> Handle(ReasignarEnvioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        var accion = NormalizarAccion(request.Accion);

        var despachadorId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al despachador.");

        var origen = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");
        if (origen.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        if (envio.HojaDeRutaId != origen.Id)
            throw new DomainValidationException("El envío no pertenece a la hoja de ruta indicada.");

        var motivo = string.IsNullOrWhiteSpace(request.Observaciones) ? "Reasignación solicitada por el despachador." : request.Observaciones.Trim();
        var mensajes = new List<OutboxMessage>();
        var enLaCalle = origen.Estado is "Despachada" or "EnTransito";
        Guid? destinoId = null;
        string resumen;

        if (enLaCalle)
        {
            // Ruta ya despachada: el envío se registra como no entregado con un motivo interno y vuelve a depósito en la rendición
            if (accion != Retirar)
                throw new DomainValidationException("El envío ya está en la calle: se retira como no entregado y se reasigna o reprograma después de la rendición.");

            var motivoInterno = await ObtenerMotivoInternoAsync(request.MotivoInternoCodigo, cancellationToken);
            origen.RetirarEnvio(envio.Id);
            envio.RegistrarRetiroEnCalle(motivoInterno, despachadorId, request.Observaciones);
            resumen = "Envío retirado de la ruta en la calle: queda no entregado y volverá a depósito en la rendición del repartidor.";
        }
        else
        {
            // Ruta aún no despachada: el envío vuelve directamente a depósito.
            // Primero se valida el destino o la franja para no dejar el envío a medio mover ante un dato inválido.
            var destino = accion == ReasignarARuta ? await ValidarDestinoAsync(request, origen, envio, cancellationToken) : null;
            var franja = accion == Reprogramar ? await ValidarFranjaAsync(request, cancellationToken) : null;

            origen.RetirarEnvio(envio.Id);
            envio.RetirarDeRuta(motivo, despachadorId);
            resumen = "Envío retirado de la ruta y devuelto a depósito.";

            if (destino is not null)
            {
                destino.AgregarParada(envio.Id, destino.SiguienteOrden, envio.DireccionEntregaId!.Value, envio.FranjaHorariaComprometidaId);
                destino.RegistrarAsignacion(envio.Id, despachadorId);
                envio.AsignarRuta(destino.Id, despachadorId);
                await _rutaRepository.UpdateAsync(destino, cancellationToken);
                destinoId = destino.Id;
                resumen = "Envío reasignado a otra hoja de ruta.";
            }
            else if (franja is not null)
            {
                envio.ReprogramarFranja(franja.Id, despachadorId);
                resumen = "Envío retirado de la ruta y reprogramado para otra franja horaria.";
            }
        }

        // Se informa para que se notifique al repartidor afectado
        mensajes.Add(new OutboxMessage(Guid.NewGuid(), nameof(Envio), envio.Id, "EnvioRetiradoDeRutaIntegrationEvent", JsonSerializer.Serialize(new
        {
            EnvioId = envio.Id,
            HojaDeRutaId = origen.Id,
            RepartidorId = origen.RepartidorId,
            Accion = accion,
            Timestamp = _clock.UtcNow
        })));

        await _rutaRepository.UpdateAsync(origen, cancellationToken);
        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _outboxStorage.SaveRangeAsync(mensajes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (enLaCalle && _tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(origen.OperadorId, origen.Id, origen.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida la reasignación ya persistida
            }
        }

        return new ReasignacionResultDto(
            envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString(), accion, origen.Id, destinoId, enLaCalle, resumen);
    }

    private static string NormalizarAccion(string? accion)
    {
        var valor = string.IsNullOrWhiteSpace(accion) ? Retirar : accion.Trim();
        return new[] { Retirar, ReasignarARuta, Reprogramar }
            .FirstOrDefault(a => a.Equals(valor, StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainValidationException($"La acción '{accion}' no es válida. Valores admitidos: {Retirar}, {ReasignarARuta}, {Reprogramar}.");
    }

    private async Task<MotivoNoEntrega> ObtenerMotivoInternoAsync(string? codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainValidationException("Para retirar un envío que está en la calle indique un motivo interno del catálogo.");

        var motivo = await _envioRepository.GetMotivoNoEntregaAsync(_currentTenant.OperadorId, null, codigo, cancellationToken)
            ?? throw new DomainValidationException($"El motivo '{codigo}' no existe en el catálogo del operador.");

        if (!motivo.Activo || !motivo.EsInterno)
            throw new DomainValidationException($"El motivo '{motivo.Codigo}' no es un motivo interno activo.");

        return motivo;
    }

    /// <summary>Valida que la hoja de ruta de destino admita al envío, sin modificar nada.</summary>
    private async Task<HojaDeRuta> ValidarDestinoAsync(
        ReasignarEnvioCommand request, HojaDeRuta origen, Envio envio, CancellationToken cancellationToken)
    {
        if (!request.HojaDeRutaDestinoId.HasValue || request.HojaDeRutaDestinoId.Value == Guid.Empty)
            throw new DomainValidationException("Indique la hoja de ruta de destino.");

        if (request.HojaDeRutaDestinoId.Value == origen.Id)
            throw new DomainValidationException("La hoja de ruta de destino debe ser distinta de la de origen.");

        var destino = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaDestinoId.Value, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta de destino '{request.HojaDeRutaDestinoId}' no encontrada.");
        if (destino.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta de destino no corresponde al operador actual.");

        if (destino.Estado != "Liberada")
            throw new DomainValidationException($"Solo se puede reasignar a una hoja de ruta liberada (estado de la ruta de destino: '{destino.Estado}').");

        if (!envio.EstaClasificado || !envio.DireccionEntregaId.HasValue)
            throw new DomainValidationException("El envío debe estar clasificado y tener dirección de entrega para asignarse a una ruta.");

        // Las mismas restricciones del armado: paradas máximas, capacidad del vehículo y franja
        var regla = await _envioRepository.GetReglaOperativaVigenteAsync(_currentTenant.OperadorId, cancellationToken)
            ?? throw new DomainValidationException("El operador no tiene reglas operativas vigentes.");
        var vehiculo = destino.VehiculoId.HasValue
            ? (await _vehiculoRepository.GetByIdsAsync(new[] { destino.VehiculoId.Value }, cancellationToken)).FirstOrDefault()
            : null;
        var enviosDestino = (await _envioRepository.GetByHojaDeRutaIdAsync(destino.Id, cancellationToken))
            .Where(e => destino.Paradas.Any(p => p.EnvioId == e.Id && p.Estado != "Cancelada"))
            .ToList();

        if (destino.Paradas.Count(p => p.Estado != "Cancelada") >= regla.MaxParadasPorRuta)
            throw new DomainValidationException($"La hoja de ruta de destino ya tiene el máximo de paradas ({regla.MaxParadasPorRuta}).");

        if (vehiculo is not null)
        {
            if (enviosDestino.Sum(e => e.PesoTotal) + envio.PesoTotal > vehiculo.CapacidadPeso)
                throw new DomainValidationException($"Se superaría la capacidad de peso del vehículo de destino ({vehiculo.CapacidadPeso}).");

            if (enviosDestino.Sum(e => e.VolumenTotal) + envio.VolumenTotal > vehiculo.CapacidadVolumen)
                throw new DomainValidationException($"Se superaría la capacidad de volumen del vehículo de destino ({vehiculo.CapacidadVolumen}).");
        }

        if (envio.FranjaHorariaComprometidaId.HasValue)
        {
            var franja = (await _franjaHorariaRepository.GetByIdsAsync(new[] { envio.FranjaHorariaComprometidaId.Value }, cancellationToken)).FirstOrDefault();
            if (franja is null || !franja.Activa || franja.DiaSemana != destino.Fecha.DayOfWeek)
                throw new DomainValidationException("La franja horaria comprometida del envío no es compatible con la hoja de ruta de destino.");
        }

        return destino;
    }

    private async Task<FranjaHoraria> ValidarFranjaAsync(ReasignarEnvioCommand request, CancellationToken cancellationToken)
    {
        if (!request.FranjaHorariaId.HasValue || request.FranjaHorariaId.Value == Guid.Empty)
            throw new DomainValidationException("Indique la nueva franja horaria.");

        var franja = (await _franjaHorariaRepository.GetByIdsAsync(new[] { request.FranjaHorariaId.Value }, cancellationToken)).FirstOrDefault();
        if (franja is null || !franja.Activa)
            throw new DomainValidationException("La franja horaria indicada no existe o no está activa.");

        return franja;
    }
}
