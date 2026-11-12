using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

public class ConfirmarCargaCommandHandler : IRequestHandler<ConfirmarCargaCommand, ConfirmarCargaResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IOutboxStorage _outboxStorage;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;
    private readonly IClock? _clock;
    private readonly IDistributedCacheService? _cacheService;

    public ConfirmarCargaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IOutboxStorage outboxStorage,
        ITableroRealtimeNotifier? tableroNotifier = null,
        IClock? clock = null,
        IDistributedCacheService? cacheService = null)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _outboxStorage = outboxStorage;
        _tableroNotifier = tableroNotifier;
        _clock = clock;
        _cacheService = cacheService;
    }

    public async Task<ConfirmarCargaResultDto> Handle(ConfirmarCargaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        if (ruta.Estado is "Despachada" or "EnTransito" or "Finalizada")
        {
            var enviosActuales = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);
            var enTransitoCount = enviosActuales.Count(e => e.EstadoActual == EstadoEnvio.EnTransito);
            return new ConfirmarCargaResultDto(
                ruta.Id,
                ruta.Estado,
                ruta.EscaneosCarga.Count,
                0,
                0,
                true,
                "La hoja de ruta ya fue despachada: la carga no se puede modificar (operación idempotente).",
                enTransitoCount,
                Array.Empty<Guid>());
        }

        var codigos = (request.CodigosBultosCargados ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet();

        if (codigos.Count == 0)
            throw new DomainValidationException("Debe ingresar al menos un bulto cargado.");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);
        var bultosDeLaRuta = envios
            .SelectMany(e => e.Bultos)
            .Where(b => b.EstaDisponibleParaRuta)
            .ToList();

        var cargados = bultosDeLaRuta.Where(b => codigos.Contains(b.Identificacion)).ToList();
        var codigosSobrantes = codigos.Where(c => bultosDeLaRuta.All(b => b.Identificacion != c)).ToList();
        var sobrantes = codigosSobrantes.Count;
        var faltantes = bultosDeLaRuta.Count - cargados.Count;

        var yaRegistrada = ruta.Estado == "EnCarga";
        ruta.RegistrarCargaEscaneada(cargados.Select(b => b.Id));

        await _outboxStorage.SaveAsync(EventosIntegracion.DeRuta(EventosIntegracion.Tipos.CargaVehiculoRegistrada, ruta, new
        {
            BultosCargados = cargados.Count,
            Faltantes = faltantes,
            Sobrantes = sobrantes,
            CodigosSobrantes = codigosSobrantes,
            Observaciones = request.ObservacionesDiscrepancia
        }), cancellationToken);

        var enviosEnTransitoCount = 0;
        var enviosRetiradosIds = new List<Guid>();

        if (request.DespacharAutomaticamente)
        {
            var aDespachar = new List<Envio>();
            var aRetirar = new List<(Envio Envio, List<Bulto> Faltantes)>();

            foreach (var envio in envios.Where(e => e.EstadoActual == EstadoEnvio.AsignadoARuta))
            {
                var esperados = envio.Bultos.Where(b => b.EstaDisponibleParaRuta).ToList();
                var bultosNoCargados = esperados.Where(b => !ruta.BultoFueCargado(b.Id)).ToList();

                if (esperados.Count > 0 && bultosNoCargados.Count == 0)
                    aDespachar.Add(envio);
                else
                    aRetirar.Add((envio, bultosNoCargados));
            }

            var tieneRetirosActivos = ruta.Paradas.Any(p => p.EsRetiro && p.Estado != "Cancelada");
            var tieneOtraRutaActiva = ruta.RepartidorId.HasValue
                && await _rutaRepository.ExisteRutaSinRendirAsync(ruta.RepartidorId.Value, ruta.Id, cancellationToken);

            if ((aDespachar.Count > 0 || tieneRetirosActivos) && !tieneOtraRutaActiva)
            {
                var usuarioId = _currentTenant.UsuarioId;
                var ahora = _clock?.UtcNow ?? DateTime.UtcNow;

                foreach (var (envio, bultosNoCargados) in aRetirar)
                {
                    var codigosFaltantes = bultosNoCargados.Select(b => b.Identificacion).ToList();
                    var motivoFaltante = string.IsNullOrWhiteSpace(request.ObservacionesDiscrepancia)
                        ? $"Envío retirado del despacho por bulto(s) faltante(s) en la carga: {string.Join(", ", codigosFaltantes)}."
                        : $"Envío retirado del despacho por bulto(s) faltante(s) en la carga ({string.Join(", ", codigosFaltantes)}). Observaciones: {request.ObservacionesDiscrepancia.Trim()}";

                    envio.DesasignarDeRutaPorFaltante(motivoFaltante, usuarioId);
                    ruta.DesasignarEnvioFaltante(envio.Id);
                    enviosRetiradosIds.Add(envio.Id);

                    await _envioRepository.UpdateAsync(envio, cancellationToken);
                    await _outboxStorage.SaveAsync(EventosIntegracion.DeEnvio(
                        "EnvioDesasignadoPorFaltanteIntegrationEvent",
                        envio,
                        new
                        {
                            EnvioId = envio.Id,
                            HojaDeRutaId = ruta.Id,
                            Motivo = motivoFaltante,
                            BultosFaltantes = codigosFaltantes,
                            Timestamp = ahora
                        }), cancellationToken);
                }

                var detalleSalida = string.IsNullOrWhiteSpace(request.ObservacionesDiscrepancia)
                    ? "Salida a reparto confirmada desde aplicación móvil tras verificación de carga."
                    : $"Salida a reparto confirmada desde aplicación móvil. Observaciones: {request.ObservacionesDiscrepancia.Trim()}";

                foreach (var envio in aDespachar)
                {
                    envio.DespacharEnRuta(ruta.Id, usuarioId, detalleSalida);
                    await _envioRepository.UpdateAsync(envio, cancellationToken);

                    await _outboxStorage.SaveAsync(EventosIntegracion.DeEnvio(
                        "EnvioEnTransitoIntegrationEvent",
                        envio,
                        new
                        {
                            EnvioId = envio.Id,
                            HojaDeRutaId = ruta.Id,
                            RepartidorId = ruta.RepartidorId,
                            VehiculoId = ruta.VehiculoId,
                            Estado = EstadoEnvio.EnTransito.ToString(),
                            BultosCargados = envio.Bultos
                                .Where(b => b.EstadoBulto == "CargadoEnVehiculo")
                                .Select(b => b.Identificacion)
                                .ToList(),
                            Timestamp = ahora
                        }), cancellationToken);
                }

                ruta.Despachar();
                ruta.IniciarRecorrido();
                enviosEnTransitoCount = aDespachar.Count;

                await _outboxStorage.SaveAsync(EventosIntegracion.DeRuta(
                    "RutaDespachadaIntegrationEvent",
                    ruta,
                    new
                    {
                        HojaDeRutaId = ruta.Id,
                        RepartidorId = ruta.RepartidorId,
                        VehiculoId = ruta.VehiculoId,
                        TotalEnviosDespachados = aDespachar.Count,
                        TotalEnviosRetirados = enviosRetiradosIds.Count,
                        FechaDespacho = ruta.FechaDespacho ?? ahora,
                        Observaciones = request.ObservacionesDiscrepancia
                    }), cancellationToken);

                await _outboxStorage.SaveAsync(EventosIntegracion.DeRuta(
                    EventosIntegracion.Tipos.RutaIniciada,
                    ruta,
                    new
                    {
                        HojaDeRutaId = ruta.Id,
                        FechaInicioUtc = ahora,
                        IdempotencyKey = request.IdempotencyKey
                    }), cancellationToken);
            }
        }

        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_cacheService is not null && (enviosEnTransitoCount > 0 || enviosRetiradosIds.Count > 0))
        {
            foreach (var envio in envios)
            {
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorCodigo(envio.CodigoSeguimientoPublico), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorOperadorYCodigo(envio.OperadorId, envio.CodigoSeguimientoPublico), cancellationToken);
                await _cacheService.RemoveAsync(ClavesCache.EnvioPorId(envio.Id), cancellationToken);
            }
        }

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no debe revertir la transacción confirmada
            }
        }

        var mensaje = faltantes > 0 || sobrantes > 0
            ? $"Carga registrada con discrepancias: {faltantes} bulto(s) faltante(s) y {sobrantes} sobrante(s)."
            : "Carga registrada y hoja de ruta en tránsito sin discrepancias.";

        return new ConfirmarCargaResultDto(
            ruta.Id,
            ruta.Estado,
            cargados.Count,
            faltantes,
            sobrantes,
            yaRegistrada,
            mensaje,
            enviosEnTransitoCount,
            enviosRetiradosIds);
    }
}
