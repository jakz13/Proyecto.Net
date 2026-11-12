using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;

public class RendirHojaDeRutaCommandHandler : IRequestHandler<RendirHojaDeRutaCommand, RendicionResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IRendicionRepository _rendicionRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;
    private readonly IRetiroRepository? _retiroRepository;

    public RendirHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRendicionRepository rendicionRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null,
        IRetiroRepository? retiroRepository = null)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _rendicionRepository = rendicionRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
        _retiroRepository = retiroRepository;
    }

    public async Task<RendicionResultDto> Handle(RendirHojaDeRutaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al operario de depósito.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        if (ruta.Estado == "Rendida")
        {
            var previa = await _rendicionRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);
            return new RendicionResultDto(
                ruta.Id, ruta.Estado,
                previa?.BultosEntregados ?? 0, previa?.BultosDevueltos ?? 0, previa?.BultosPendientes ?? 0,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                true, "La hoja de ruta ya fue rendida (operación idempotente).");
        }

        if (ruta.Estado is not ("Despachada" or "EnTransito" or "Finalizada"))
            throw new DomainValidationException($"Solo se puede rendir una hoja de ruta despachada (estado actual: '{ruta.Estado}').");

        var presentados = (request.CodigosBultosDevueltos ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet();

        var envios = (await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToList();

        var viajaron = envios.SelectMany(e => ViajaronEnElVehiculo(e)).Select(b => b.Identificacion).ToHashSet();
        var sobrantes = presentados.Where(c => !viajaron.Contains(c)).OrderBy(c => c).ToList();

        var retornanSinIntento = envios
            .Where(e => e.EstadoActual == EstadoEnvio.EnTransito && TodosPresentados(e, presentados))
            .ToList();
        var motivo = retornanSinIntento.Count > 0
            ? await ObtenerMotivoInternoAsync(request.MotivoInternoCodigo, cancellationToken)
            : null;

        int entregados = 0, devueltos = 0, pendientes = 0;
        var reintegrados = new List<string>();
        var enDevolucion = new List<string>();
        var enviosPendientes = new List<string>();
        var mensajes = new List<OutboxMessage>();

        foreach (var envio in envios)
        {
            var bultos = ViajaronEnElVehiculo(envio).ToList();
            var presentadosDelEnvio = bultos.Count(b => presentados.Contains(b.Identificacion));

            switch (envio.EstadoActual)
            {
                case EstadoEnvio.Entregado:
                    entregados += bultos.Count;
                    break;

                case EstadoEnvio.EntregadoParcialmente:
                    var bultosEntregadosParcial = bultos.Where(b => b.EstaEntregado).ToList();
                    var bultosNoEntregadosParcial = bultos.Where(b => !b.EstaEntregado).ToList();
                    var presentadosParcial = bultosNoEntregadosParcial.Count(b => presentados.Contains(b.Identificacion));

                    entregados += bultosEntregadosParcial.Count;
                    devueltos += presentadosParcial;
                    pendientes += bultosNoEntregadosParcial.Count - presentadosParcial;

                    if (bultosNoEntregadosParcial.Count > 0 && presentadosParcial == bultosNoEntregadosParcial.Count)
                    {
                        envio.ReintegrarADepositoPorRendicion(usuarioId);
                        reintegrados.Add(envio.CodigoSeguimientoPublico);
                        mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioReintegradoADepositoIntegrationEvent", new
                        {
                            EnvioId = envio.Id,
                            HojaDeRutaId = ruta.Id,
                            Estado = envio.EstadoActual.ToString(),
                            Timestamp = _clock.UtcNow
                        }));
                    }
                    else if (presentadosParcial < bultosNoEntregadosParcial.Count)
                    {
                        enviosPendientes.Add(envio.CodigoSeguimientoPublico);
                    }
                    break;

                case EstadoEnvio.EnDevolucion:
                    devueltos += presentadosDelEnvio;
                    pendientes += bultos.Count - presentadosDelEnvio;
                    enDevolucion.Add(envio.CodigoSeguimientoPublico);
                    if (presentadosDelEnvio < bultos.Count)
                        enviosPendientes.Add(envio.CodigoSeguimientoPublico);
                    break;

                case EstadoEnvio.Reprogramado:
                case EstadoEnvio.NoEntregado:
                case EstadoEnvio.EnTransito:
                    devueltos += presentadosDelEnvio;
                    pendientes += bultos.Count - presentadosDelEnvio;

                    if (bultos.Count > 0 && presentadosDelEnvio == bultos.Count)
                    {
                        if (envio.EstadoActual == EstadoEnvio.EnTransito)
                            envio.RegistrarRetornoSinIntento(motivo!, usuarioId, request.Observaciones);
                        else
                            envio.ReintegrarADepositoPorRendicion(usuarioId);

                        reintegrados.Add(envio.CodigoSeguimientoPublico);
                        mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioReintegradoADepositoIntegrationEvent", new
                        {
                            EnvioId = envio.Id,
                            HojaDeRutaId = ruta.Id,
                            Estado = EstadoEnvio.EnDeposito.ToString(),
                            Timestamp = _clock.UtcNow
                        }));
                    }
                    else
                    {
                        enviosPendientes.Add(envio.CodigoSeguimientoPublico);
                    }
                    break;
            }
        }

        // Hoja de ruta mixta: los bultos retirados en domicilio llegan al depósito con el repartidor, y un retiro
        // que nunca se visitó vuelve a quedar pendiente de asignación. Se resuelve antes de cerrar la ruta, que marca las paradas no visitadas.
        var retirosRecibidos = await ProcesarRetirosAlRendirAsync(ruta, usuarioId, cancellationToken);

        ruta.Rendir();

        var rendicion = new RendicionDeposito(
            Guid.NewGuid(), ruta.Id, ruta.RepartidorId!.Value, entregados, devueltos, pendientes, request.Observaciones);
        await _rendicionRepository.AddAsync(rendicion, cancellationToken);

        mensajes.Add(CrearMensaje(nameof(Domain.Entities.Planificacion.HojaDeRuta), ruta.Id, "RutaRendidaIntegrationEvent", new
        {
            HojaDeRutaId = ruta.Id,
            RepartidorId = ruta.RepartidorId,
            BultosEntregados = entregados,
            BultosDevueltos = devueltos,
            BultosPendientes = pendientes,
            Timestamp = _clock.UtcNow
        }));

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _envioRepository.UpdateRangeAsync(envios, cancellationToken);
        await _outboxStorage.SaveRangeAsync(mensajes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida la rendición ya persistida
            }
        }

        var sufijoRetiros = retirosRecibidos > 0 ? $" Se recibieron {retirosRecibidos} retiro(s) en depósito." : string.Empty;

        return new RendicionResultDto(
            ruta.Id, ruta.Estado, entregados, devueltos, pendientes,
            reintegrados, enDevolucion, enviosPendientes, sobrantes, false,
            (pendientes == 0 && sobrantes.Count == 0
                ? "Rendición registrada sin diferencias. El repartidor puede despachar otra hoja de ruta."
                : $"Rendición registrada con diferencias: {pendientes} bulto(s) pendiente(s) y {sobrantes.Count} código(s) sobrante(s).") + sufijoRetiros);
    }

    /// <summary>Devuelve la cantidad de retiros que quedaron recibidos en depósito.</summary>
    private async Task<int> ProcesarRetirosAlRendirAsync(HojaDeRuta ruta, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (_retiroRepository is null)
            return 0;

        var idsRetiros = ruta.Paradas.Where(p => p.Estado != "Cancelada").IdsDeRetiros().Distinct().ToList();
        if (idsRetiros.Count == 0)
            return 0;

        var recibidos = 0;
        var retiros = (await _retiroRepository.GetByIdsAsync(idsRetiros, cancellationToken))
            .Where(r => r.OperadorId == _currentTenant.OperadorId);

        foreach (var retiro in retiros)
        {
            switch (retiro.EstadoActual)
            {
                case EstadoRetiro.Retirado:
                    retiro.RecibirEnDeposito(usuarioId);
                    recibidos++;
                    break;

                case EstadoRetiro.AsignadoARuta:
                    retiro.LiberarDeRuta("La hoja de ruta se rindió sin visitar el domicilio de retiro.", usuarioId);
                    break;
            }

            await _retiroRepository.UpdateAsync(retiro, cancellationToken);
        }

        return recibidos;
    }

    /// <summary>Bultos que salieron en el vehículo con esta ruta (entregados, en tránsito, pendientes o en retorno).</summary>
    private static IEnumerable<Bulto> ViajaronEnElVehiculo(Envio envio) =>
        envio.Bultos.Where(b => b.EstadoBulto is
            MaquinaEstadosBulto.CargadoEnVehiculo
            or MaquinaEstadosBulto.EnRetornoADeposito
            or MaquinaEstadosBulto.Entregado
            or MaquinaEstadosBulto.NoEntregado
            or MaquinaEstadosBulto.PendienteReenvio);

    private static bool TodosPresentados(Envio envio, HashSet<string> presentados)
    {
        var bultos = ViajaronEnElVehiculo(envio).ToList();
        return bultos.Count > 0 && bultos.All(b => presentados.Contains(b.Identificacion));
    }

    private async Task<MotivoNoEntrega> ObtenerMotivoInternoAsync(string? codigo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainValidationException("Hay envíos que volvieron sin intento de entrega: indique un motivo interno del catálogo.");

        var motivo = await _envioRepository.GetMotivoNoEntregaAsync(_currentTenant.OperadorId, null, codigo, cancellationToken)
            ?? throw new DomainValidationException($"El motivo '{codigo}' no existe en el catálogo del operador.");

        if (!motivo.Activo || !motivo.EsInterno)
            throw new DomainValidationException($"El motivo '{motivo.Codigo}' no es un motivo interno activo.");

        return motivo;
    }

    private static OutboxMessage CrearMensaje(string agregado, Guid agregadoId, string tipoEvento, object payload) =>
        new(Guid.NewGuid(), agregado, agregadoId, tipoEvento, JsonSerializer.Serialize(payload));
}
