using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaParcial;

public class RegistrarEntregaParcialCommandHandler : IRequestHandler<RegistrarEntregaParcialCommand, EntregaParcialResultDto>
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

    public RegistrarEntregaParcialCommandHandler(
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

    public async Task<EntregaParcialResultDto> Handle(RegistrarEntregaParcialCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _funcionalidades.ExigirAsync(_currentTenant.OperadorId, Domain.Entities.Operador.Funcionalidad.EntregaParcial, cancellationToken);

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

        // Idempotencia offline: si el envío ya está totalmente entregado, o si los mismos bultos solicitados ya están entregados en esta parada
        var solicitadosNormalizados = (request.CodigosBultosEntregados ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        bool mismosBultosYaEntregados = solicitadosNormalizados.Count > 0
            && solicitadosNormalizados.All(c => envio.Bultos.Any(b => b.Identificacion == c && b.EstaEntregado));

        if (envio.EstadoActual == EstadoEnvio.Entregado || (parada.Estado == "EntregadaParcialmente" && mismosBultosYaEntregados))
        {
            int operativosCount = envio.Bultos.Count(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion);
            int entregadosCount = envio.Bultos.Count(b => b.EstaEntregado);
            int pendientesCount = Math.Max(0, operativosCount - entregadosCount);

            return new EntregaParcialResultDto(
                envio.Id,
                ruta.Id,
                parada.Id,
                envio.CodigoSeguimientoPublico,
                envio.EstadoActual.ToString(),
                parada.Estado,
                operativosCount,
                entregadosCount,
                pendientesCount,
                $"{entregadosCount} de {operativosCount} bultos entregados",
                reglaOperativa.PoliticaEntregaParcial,
                false,
                envio.Bultos.Where(b => b.EstaEntregado).Select(b => b.Identificacion).ToList(),
                envio.Bultos.Where(b => !b.EstaEntregado && b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion).Select(b => b.Identificacion).ToList(),
                Array.Empty<string>(),
                _clock.UtcNow,
                true,
                "La entrega de bultos ya se encontraba registrada previamente (Operación idempotente)."
            );
        }

        var inputsNoEntregados = new List<BultoNoEntregadoInput>();
        foreach (var item in request.BultosNoEntregados ?? Array.Empty<BultoNoEntregadoRequestDto>())
        {
            if (item is null || string.IsNullOrWhiteSpace(item.CodigoBulto))
                throw new DomainValidationException("Cada bulto no entregado debe indicar su código.");

            var motivo = await _envioRepository.GetMotivoNoEntregaAsync(
                ruta.OperadorId,
                item.MotivoNoEntregaId,
                item.MotivoCodigo,
                cancellationToken)
                ?? throw new DomainValidationException(
                    $"El motivo de no entrega indicado para el bulto '{item.CodigoBulto}' no existe en el catálogo del operador.");

            inputsNoEntregados.Add(new BultoNoEntregadoInput(item.CodigoBulto, motivo, item.Observaciones));
        }

        var fechaDispositivoUtc = request.FechaHoraDispositivoUtc ?? _clock.UtcNow;

        var resultado = envio.RegistrarEntregaParcial(
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

        if (resultado.EstadoFinalEnvio == EstadoEnvio.Entregado)
            ruta.RegistrarEntregaExitosaEnParada(parada.Id);
        else
            ruta.RegistrarEntregaParcialEnParada(parada.Id);

        var payloadEvento = JsonSerializer.Serialize(new
        {
            EnvioId = envio.Id,
            HojaDeRutaId = ruta.Id,
            ParadaId = parada.Id,
            CodigoSeguimiento = envio.CodigoSeguimientoPublico,
            Estado = resultado.EstadoFinalEnvio.ToString(),
            TotalBultos = resultado.TotalBultosOperativos,
            BultosEntregados = resultado.BultosEntregadosAcumulados,
            BultosPendientes = resultado.BultosPendientes,
            CodigosEntregados = resultado.CodigosEntregadosEnVisita,
            CodigosNoEntregados = resultado.CodigosNoEntregadosEnVisita,
            PoliticaAplicada = resultado.PoliticaAplicada,
            RecargoAplicado = resultado.RecargoAplicado,
            ConflictosResueltos = resultado.ConflictosResueltos,
            NombreReceptor = request.NombreReceptor.Trim(),
            DocumentoReceptor = request.DocumentoReceptor.Trim(),
            Latitud = request.Latitud,
            Longitud = request.Longitud,
            FechaHoraDispositivoUtc = fechaDispositivoUtc,
            FechaHoraServidorUtc = _clock.UtcNow,
            IdempotencyKey = request.IdempotencyKey,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            OperadorId = ruta.OperadorId
        });

        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            nameof(Envio),
            envio.Id,
            resultado.EstadoFinalEnvio == EstadoEnvio.Entregado
                ? "EnvioEntregadoIntegrationEvent"
                : "EnvioEntregadoParcialmenteIntegrationEvent",
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

        string avance = $"{resultado.BultosEntregadosAcumulados} de {resultado.TotalBultosOperativos} bultos entregados";
        string mensaje = resultado.EstadoFinalEnvio == EstadoEnvio.Entregado
            ? $"Todos los bultos del envío fueron entregados ({avance})."
            : $"Entrega parcial registrada ({avance}). Política para pendientes: {resultado.PoliticaAplicada}.";

        return new EntregaParcialResultDto(
            envio.Id,
            ruta.Id,
            parada.Id,
            envio.CodigoSeguimientoPublico,
            resultado.EstadoFinalEnvio.ToString(),
            parada.Estado,
            resultado.TotalBultosOperativos,
            resultado.BultosEntregadosAcumulados,
            resultado.BultosPendientes,
            avance,
            resultado.PoliticaAplicada,
            resultado.RecargoAplicado,
            resultado.CodigosEntregadosEnVisita,
            resultado.CodigosNoEntregadosEnVisita,
            resultado.ConflictosResueltos,
            fechaDispositivoUtc,
            false,
            mensaje
        );
    }
}

