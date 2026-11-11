using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;

public class RegistrarEntregaExitosaCommandHandler : IRequestHandler<RegistrarEntregaExitosaCommand, EntregaExitosaResultDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly ITableroRealtimeNotifier? _tableroNotifier;
    private readonly IFuncionalidadesService? _funcionalidades;

    public RegistrarEntregaExitosaCommandHandler(
        IEnvioRepository envioRepository,
        IRutaRepository rutaRepository,
        IUnitOfWork unitOfWork,
        IOutboxStorage outboxStorage,
        IDistributedCacheService cacheService,
        ICurrentTenant currentTenant,
        IClock clock,
        ITableroRealtimeNotifier? tableroNotifier = null,
        IFuncionalidadesService? funcionalidades = null)
    {
        _funcionalidades = funcionalidades;
        _envioRepository = envioRepository;
        _rutaRepository = rutaRepository;
        _unitOfWork = unitOfWork;
        _outboxStorage = outboxStorage;
        _cacheService = cacheService;
        _currentTenant = currentTenant;
        _clock = clock;
        _tableroNotifier = tableroNotifier;
    }

    public async Task<EntregaExitosaResultDto> Handle(RegistrarEntregaExitosaCommand request, CancellationToken cancellationToken)
    {
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.ParadaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        if (string.IsNullOrWhiteSpace(request.NombreReceptor))
            throw new DomainValidationException("El nombre del receptor es requerido.");

        if (string.IsNullOrWhiteSpace(request.DocumentoReceptor))
            throw new DomainValidationException("El documento del receptor es requerido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        var parada = ruta.Paradas.FirstOrDefault(p => p.Id == request.ParadaId)
            ?? throw new NotFoundException($"La parada '{request.ParadaId}' no pertenece a la hoja de ruta '{request.HojaDeRutaId}'.");

        if (parada.EnvioId != request.EnvioId)
            throw new DomainValidationException($"La parada '{request.ParadaId}' no corresponde al envío '{request.EnvioId}'.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        if (envio.EstadoActual == EstadoEnvio.Entregado || parada.Estado == "Entregada")
        {
            return new EntregaExitosaResultDto(
                envio.Id,
                ruta.Id,
                parada.Id,
                envio.CodigoSeguimientoPublico,
                envio.EstadoActual.ToString(),
                parada.Estado,
                _clock.UtcNow,
                true,
                "La entrega ya se encontraba registrada previamente (Operación idempotente).",
                envio.Bultos.Count,
                envio.Bultos.Count(b => b.EstaEntregado),
                envio.Bultos.Count(b => !b.EstaEntregado)
            );
        }

        // Manejo explícito de conflictos de sincronización (RF 24):
        // si el repartidor completó la entrega física offline pero el servidor ya había retirado o reasignado el envío (UC-10),
        // prevalece la entrega física real con su prueba y se asienta el conflicto en ConflictoSincronizacion y en el historial (UC-39).
        // Una segunda visita sobre un envío ya entregado parcialmente es el flujo normal de la entrega por bultos, no un conflicto.
        bool huboConflicto = (envio.EstadoActual is not (EstadoEnvio.EnTransito or EstadoEnvio.EntregadoParcialmente))
            || parada.Estado == "Cancelada";
        if (huboConflicto)
        {
            var conflicto = new ConflictoSincronizacion(
                Guid.NewGuid(),
                nameof(Envio),
                envio.Id,
                estadoServidor: envio.EstadoActual.ToString(),
                estadoDispositivo: EstadoEnvio.Entregado.ToString(),
                politicaAplicada: "PrevaleceEntregaFisicaConEvidencia",
                resolucion: $"Entrega física completada offline con prueba por el repartidor mientras el servidor mantenía el envío en estado '{envio.EstadoActual}'. Prevalece la entrega real.");

            await _envioRepository.RegistrarConflictoAsync(conflicto, cancellationToken);
        }

        // La hoja de ruta debe estar despachada o en tránsito, salvo que se esté resolviendo un conflicto offline (UC-09 / UC-18 / UC-20)
        if (ruta.Estado is not ("Despachada" or "EnTransito") && !huboConflicto)
        {
            throw new DomainValidationException($"No se puede registrar la entrega porque la hoja de ruta no se encuentra en tránsito (Estado actual: '{ruta.Estado}'). Debe escanear y despachar la carga previamente.");
        }

        var reglaOperativa = await _envioRepository.GetReglaOperativaVigenteAsync(ruta.OperadorId, cancellationToken)
            ?? new ReglaOperativa(
                Guid.NewGuid(),
                ruta.OperadorId,
                1,
                _clock.UtcNow.AddYears(-1),
                maxIntentosEntrega: 3,
                plazoEntreIntentos: TimeSpan.FromHours(24),
                tipoPruebaEntregaExigida: "Firma digital del receptor",
                politicaDevolucion: "Retorno a depósito central",
                plazoComprometidoEstandar: TimeSpan.FromHours(48),
                plazoComprometidoUrgente: TimeSpan.FromHours(24));

        var fechaDispositivoUtc = request.FechaHoraDispositivoUtc ?? _clock.UtcNow;

        bool tieneEscaneoPorBultos = (request.CodigosBultosEntregados is { Count: > 0 })
            || (request.BultosNoEntregados is { Count: > 0 });

        IReadOnlyList<string> codigosEntregadosResult;
        IReadOnlyList<string> codigosNoEntregadosResult = Array.Empty<string>();
        IReadOnlyList<string> conflictosResueltosResult = Array.Empty<string>();

        if (tieneEscaneoPorBultos)
        {
            // Dejar bultos sin entregar es entrega parcial: solo si la funcionalidad está habilitada para el operador
            if (request.BultosNoEntregados is { Count: > 0 })
                await _funcionalidades.ExigirAsync(ruta.OperadorId, Domain.Entities.Operador.Funcionalidad.EntregaParcial, cancellationToken);

            var inputsNoEntregados = new List<BultoNoEntregadoInput>();
            foreach (var item in request.BultosNoEntregados ?? Array.Empty<BultoNoEntregadoRequestDto>())
            {
                var motivo = await _envioRepository.GetMotivoNoEntregaAsync(
                    ruta.OperadorId,
                    item.MotivoNoEntregaId,
                    item.MotivoCodigo,
                    cancellationToken)
                    ?? throw new DomainValidationException(
                        $"El motivo de no entrega indicado para el bulto '{item.CodigoBulto}' no existe en el catálogo del operador.");

                inputsNoEntregados.Add(new BultoNoEntregadoInput(item.CodigoBulto, motivo, item.Observaciones));
            }

            var resultadoParcial = envio.RegistrarEntregaParcial(
                parada.Id,
                reglaOperativa,
                request.CodigosBultosEntregados,
                inputsNoEntregados,
                request.NombreReceptor,
                request.DocumentoReceptor,
                request.FirmaDigitalBase64,
                request.FotoEvidenciaBase64,
                request.Latitud,
                request.Longitud,
                fechaDispositivoUtc,
                _currentTenant.UsuarioId);

            if (resultadoParcial.EstadoFinalEnvio == EstadoEnvio.Entregado)
                ruta.RegistrarEntregaExitosaEnParada(parada.Id);
            else
                ruta.RegistrarEntregaParcialEnParada(parada.Id);

            codigosEntregadosResult = resultadoParcial.CodigosEntregadosEnVisita;
            codigosNoEntregadosResult = resultadoParcial.CodigosNoEntregadosEnVisita;
            conflictosResueltosResult = resultadoParcial.ConflictosResueltos;
        }
        else
        {
            envio.RegistrarEntregaExitosa(
                parada.Id,
                reglaOperativa,
                request.NombreReceptor,
                request.DocumentoReceptor,
                request.FirmaDigitalBase64,
                request.FotoEvidenciaBase64,
                request.Latitud,
                request.Longitud,
                fechaDispositivoUtc,
                _currentTenant.UsuarioId,
                resolverConflictoReasignacion: huboConflicto);

            ruta.RegistrarEntregaExitosaEnParada(parada.Id, resolverConflicto: huboConflicto);
            codigosEntregadosResult = envio.Bultos.Where(b => b.EstaEntregado).Select(b => b.Identificacion).ToList();
        }

        var payloadEvento = JsonSerializer.Serialize(new
        {
            EnvioId = envio.Id,
            HojaDeRutaId = ruta.Id,
            ParadaId = parada.Id,
            CodigoSeguimiento = envio.CodigoSeguimientoPublico,
            Estado = envio.EstadoActual.ToString(),
            NombreReceptor = request.NombreReceptor.Trim(),
            DocumentoReceptor = request.DocumentoReceptor.Trim(),
            TieneFirma = !string.IsNullOrWhiteSpace(request.FirmaDigitalBase64),
            TieneFoto = !string.IsNullOrWhiteSpace(request.FotoEvidenciaBase64),
            BultosEntregados = codigosEntregadosResult,
            BultosNoEntregados = codigosNoEntregadosResult,
            Latitud = request.Latitud,
            Longitud = request.Longitud,
            FechaHoraDispositivoUtc = fechaDispositivoUtc,
            FechaHoraServidorUtc = _clock.UtcNow,
            IdempotencyKey = request.IdempotencyKey,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            OperadorId = ruta.OperadorId
        });

        var tipoEventoOutbox = envio.EstadoActual == EstadoEnvio.EntregadoParcialmente
            ? "EnvioEntregadoParcialmenteIntegrationEvent"
            : "EnvioEntregadoIntegrationEvent";

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(Envio),
            envio.Id,
            tipoEventoOutbox,
            payloadEvento);

        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorCodigo(envio.CodigoSeguimientoPublico), cancellationToken);
        await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorOperadorYCodigo(envio.OperadorId, envio.CodigoSeguimientoPublico), cancellationToken);
        await _cacheService.RemoveAsync(ClavesCache.EnvioPorId(envio.Id), cancellationToken);

        if (_tableroNotifier is not null)
        {
            try
            {
                await _tableroNotifier.NotificarHitoParadaAsync(ruta.OperadorId, ruta.Id, parada.Id, parada.Estado, cancellationToken);
                if (ruta.Estado == "Finalizada")
                    await _tableroNotifier.NotificarCambioRutaAsync(ruta.OperadorId, ruta.Id, ruta.Estado, cancellationToken);
            }
            catch
            {
                // El fallo de notificación en tiempo real no revierte ni invalida la entrega ya persistida
            }
        }

        int totalBultos = envio.Bultos.Count(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion);
        int bultosEntregados = envio.Bultos.Count(b => b.EstaEntregado);
        int bultosPendientes = Math.Max(0, totalBultos - bultosEntregados);

        string mensajeResultado = huboConflicto
            ? "Entrega registrada resolviendo conflicto: prevaleció la entrega física con evidencia sobre la reasignación previa en servidor."
            : envio.EstadoActual == EstadoEnvio.EntregadoParcialmente
                ? $"Entrega parcial registrada correctamente ({bultosEntregados} de {totalBultos} bultos entregados)."
                : "Entrega exitosa registrada correctamente.";

        return new EntregaExitosaResultDto(
            envio.Id,
            ruta.Id,
            parada.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            parada.Estado,
            fechaDispositivoUtc,
            false,
            mensajeResultado,
            totalBultos,
            bultosEntregados,
            bultosPendientes,
            codigosEntregadosResult,
            codigosNoEntregadosResult,
            conflictosResueltosResult,
            ConflictoResuelto: huboConflicto
        );
    }
}
