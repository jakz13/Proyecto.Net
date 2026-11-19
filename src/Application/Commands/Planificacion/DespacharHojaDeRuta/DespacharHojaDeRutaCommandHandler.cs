using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Planificacion.DespacharHojaDeRuta;

public class DespacharHojaDeRutaCommandHandler : IRequestHandler<DespacharHojaDeRutaCommand, DespachoResultDto>
{
    private const string MotivoNoCargado = "Los bultos del envío no fueron cargados en el vehículo.";
    private const string MotivoSinClasificar = "El envío no fue clasificado por el depósito: no puede salir a reparto.";

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;

    public DespacharHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<DespachoResultDto> Handle(DespacharHojaDeRutaCommand request, CancellationToken cancellationToken)
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

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);

        if (ruta.Estado is "Despachada" or "EnTransito" or "Finalizada")
        {
            return new DespachoResultDto(
                ruta.Id, ruta.Estado, ruta.FechaDespacho,
                envios.Count(e => e.EstadoActual == EstadoEnvio.EnTransito),
                Array.Empty<EnvioRetiradoDto>(), true,
                "La hoja de ruta ya se encontraba despachada (operación idempotente).");
        }

        // Una hoja de ruta con solo retiros no tiene bultos que cargar en el depósito: puede salir sin escaneo de carga
        var paradasVigentes = ruta.Paradas.Where(p => p.Estado != "Cancelada").ToList();
        var soloRetiros = paradasVigentes.Count > 0 && paradasVigentes.All(p => p.EsRetiro);
        var tieneRetiros = paradasVigentes.Any(p => p.EsRetiro);

        if (request.ConfirmarCargaEnMuelle && ruta.Estado is "Liberada" or "EnCarga")
        {
            var enviosExcluidos = request.EnviosNoCargadosIds?.ToHashSet() ?? new HashSet<Guid>();
            var bultosCargadosEnMuelle = envios
                .Where(e => e.EstadoActual == EstadoEnvio.AsignadoARuta && !enviosExcluidos.Contains(e.Id))
                .SelectMany(e => e.Bultos.Where(b => b.EstaDisponibleParaRuta))
                .Select(b => b.Id)
                .ToList();

            if (bultosCargadosEnMuelle.Count > 0 || (ruta.Estado == "EnCarga" && enviosExcluidos.Count > 0))
            {
                ruta.RegistrarCargaEscaneada(bultosCargadosEnMuelle);
            }
        }

        if (ruta.Estado != "EnCarga" && !(ruta.Estado == "Liberada" && soloRetiros))
            throw new DomainValidationException($"La hoja de ruta está en estado '{ruta.Estado}': la carga del vehículo debe estar escaneada y sincronizada antes de despachar.");

        if (await _rutaRepository.ExisteRutaSinRendirAsync(ruta.RepartidorId!.Value, ruta.Id, cancellationToken))
            throw new DomainValidationException("El repartidor tiene otra hoja de ruta despachada sin rendir.");

        // Viajan todos los envíos que tengan al menos un bulto cargado; los bultos no cargados quedan como FaltanteEnCarga.
        // Solo si ningún bulto del envío subió al vehículo, el envío se retira de la ruta y vuelve a depósito.
        var aDespachar = new List<(Envio Envio, List<Bulto> Faltantes)>();
        var aRetirar = new List<Envio>();
        var motivosRetiro = new Dictionary<Guid, string>();
        foreach (var envio in envios.Where(e => e.EstadoActual == EstadoEnvio.AsignadoARuta))
        {
            // Barrera de seguridad: un envío que el depósito no clasificó no sale a la calle, vuelve a depósito
            if (!envio.EstaClasificado)
            {
                aRetirar.Add(envio);
                motivosRetiro[envio.Id] = MotivoSinClasificar;
                continue;
            }

            var esperados = envio.Bultos.Where(b => b.EstaDisponibleParaRuta).ToList();
            var cargadosDelEnvio = esperados.Where(b => ruta.BultoFueCargado(b.Id)).ToList();
            var noCargadosDelEnvio = esperados.Where(b => !ruta.BultoFueCargado(b.Id)).ToList();

            if (cargadosDelEnvio.Count > 0)
                aDespachar.Add((envio, noCargadosDelEnvio));
            else
                aRetirar.Add(envio);
        }

        // Sin entregas cargadas, la ruta solo puede salir si tiene retiros que hacer
        if (aDespachar.Count == 0 && !tieneRetiros)
            throw new DomainValidationException("Ningún envío de la hoja de ruta tiene bultos cargados: no se puede despachar.");

        var mensajes = new List<OutboxMessage>();
        foreach (var envio in aRetirar)
        {
            var motivoRetiro = motivosRetiro.GetValueOrDefault(envio.Id, MotivoNoCargado);
            envio.DesasignarDeRutaPorFaltante(motivoRetiro, usuarioId);
            ruta.DesasignarEnvioFaltante(envio.Id);

            mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioDesasignadoPorFaltanteIntegrationEvent", new
            {
                EnvioId = envio.Id,
                HojaDeRutaId = ruta.Id,
                Motivo = motivosRetiro.ContainsKey(envio.Id) ? "SinClasificar" : "FaltanteEnCarga",
                Timestamp = _clock.UtcNow
            }));
        }

        foreach (var (envio, bultosFaltantes) in aDespachar)
        {
            if (bultosFaltantes.Count > 0)
                envio.DespacharEnRutaParcial(ruta.Id, bultosFaltantes.Select(b => b.Id), usuarioId, request.Observaciones);
            else
                envio.DespacharEnRuta(ruta.Id, usuarioId, request.Observaciones);

            mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioEnTransitoIntegrationEvent", new
            {
                EnvioId = envio.Id,
                HojaDeRutaId = ruta.Id,
                RepartidorId = ruta.RepartidorId,
                VehiculoId = ruta.VehiculoId,
                Estado = EstadoEnvio.EnTransito.ToString(),
                BultosCargados = envio.Bultos.Where(b => b.EstadoBulto == MaquinaEstadosBulto.CargadoEnVehiculo).Select(b => b.Identificacion).ToList(),
                BultosFaltantesEnCarga = bultosFaltantes.Select(b => b.Identificacion).ToList(),
                Timestamp = _clock.UtcNow
            }));
        }

        ruta.Despachar();

        mensajes.Add(CrearMensaje(nameof(Domain.Entities.Planificacion.HojaDeRuta), ruta.Id, "RutaDespachadaIntegrationEvent", new
        {
            HojaDeRutaId = ruta.Id,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            TotalEnviosDespachados = aDespachar.Count,
            TotalEnviosRetirados = aRetirar.Count,
            FechaDespacho = ruta.FechaDespacho ?? _clock.UtcNow
        }));

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _envioRepository.UpdateRangeAsync(aDespachar.Select(x => x.Envio).Concat(aRetirar), cancellationToken);
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
                // El fallo de notificación en tiempo real no revierte ni invalida el despacho ya persistido
            }
        }

        var retirados = aRetirar
            .Select(e => new EnvioRetiradoDto(e.Id, e.CodigoSeguimientoPublico, motivosRetiro.GetValueOrDefault(e.Id, MotivoNoCargado)))
            .ToList();

        return new DespachoResultDto(
            ruta.Id,
            ruta.Estado,
            ruta.FechaDespacho,
            aDespachar.Count,
            retirados,
            false,
            retirados.Count == 0
                ? "Hoja de ruta despachada: todos los envíos quedaron en tránsito."
                : $"Hoja de ruta despachada con {retirados.Count} envío(s) retirado(s) por bultos no cargados; volvieron a depósito.");
    }

    private static OutboxMessage CrearMensaje(string agregado, Guid agregadoId, string tipoEvento, object payload) =>
        new(Guid.NewGuid(), agregado, agregadoId, tipoEvento, JsonSerializer.Serialize(payload));
}
