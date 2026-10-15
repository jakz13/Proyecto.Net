using System.Text.Json;
using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;

public class RegistrarIntentoFallidoCommandHandler : IRequestHandler<RegistrarIntentoFallidoCommand, ResultadoIntentoFallidoDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IDistributedCacheService _cacheService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public RegistrarIntentoFallidoCommandHandler(
        IEnvioRepository envioRepository,
        IRutaRepository rutaRepository,
        IUnitOfWork unitOfWork,
        IOutboxStorage outboxStorage,
        IDistributedCacheService cacheService,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _envioRepository = envioRepository;
        _rutaRepository = rutaRepository;
        _unitOfWork = unitOfWork;
        _outboxStorage = outboxStorage;
        _cacheService = cacheService;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<ResultadoIntentoFallidoDto> Handle(RegistrarIntentoFallidoCommand request, CancellationToken cancellationToken)
    {
        // 1. Validaciones estructurales de entrada
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (request.ParadaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada es inválido.");

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        if (!request.MotivoNoEntregaId.HasValue && string.IsNullOrWhiteSpace(request.MotivoCodigo))
            throw new DomainValidationException("Debe especificar el motivo de no entrega (Id o Código de catálogo).");

        if (string.IsNullOrWhiteSpace(request.Observaciones))
            throw new DomainValidationException("Las observaciones de la incidencia son obligatorias.");

        // 2. Carga y verificación de la Hoja de Ruta (Aislamiento Multitenant)
        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        var parada = ruta.Paradas.FirstOrDefault(p => p.Id == request.ParadaId)
            ?? throw new NotFoundException($"La parada '{request.ParadaId}' no pertenece a la hoja de ruta '{request.HojaDeRutaId}'.");

        if (parada.EnvioId != request.EnvioId)
            throw new DomainValidationException($"La parada '{request.ParadaId}' no corresponde al envío '{request.EnvioId}'.");

        // 3. Carga del Envio
        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // 4. Soporte Offline-First e Idempotencia estricta
        if (parada.Estado == "Fallida" && (envio.EstadoActual == EstadoEnvio.Reprogramado || envio.EstadoActual == EstadoEnvio.EnDevolucion))
        {
            return new ResultadoIntentoFallidoDto(
                envio.Id,
                ruta.Id,
                parada.Id,
                envio.CodigoSeguimientoPublico,
                envio.EstadoActual.ToString(),
                envio.CantidadIntentos,
                0,
                3,
                "Intento fallido previo",
                _clock.UtcNow,
                true,
                "El intento fallido ya se encontraba registrado previamente (Operación idempotente)."
            );
        }

        // 5. Validación del catálogo MotivoNoEntrega para el operador actual
        var motivo = await _envioRepository.GetMotivoNoEntregaAsync(
            ruta.OperadorId,
            request.MotivoNoEntregaId,
            request.MotivoCodigo,
            cancellationToken);

        if (motivo is null)
        {
            var identificadorMotivo = request.MotivoNoEntregaId?.ToString() ?? request.MotivoCodigo;
            throw new NotFoundException($"El motivo de no entrega '{identificadorMotivo}' no existe en el catálogo del operador.");
        }

        if (!motivo.Activo)
            throw new DomainValidationException($"El motivo de no entrega '{motivo.Codigo}' está inactivo en el catálogo del operador.");

        // 6. Obtención de la Regla Operativa vigente para evaluación de SLA y máximos de reintento
        var reglaOperativa = await _envioRepository.GetReglaOperativaVigenteAsync(ruta.OperadorId, cancellationToken)
            ?? new ReglaOperativa(
                Guid.NewGuid(),
                ruta.OperadorId,
                1,
                _clock.UtcNow.AddYears(-1),
                maxIntentosEntrega: 3,
                plazoEntreIntentos: TimeSpan.FromHours(24),
                tipoPruebaEntregaExigida: "Firma digital del receptor",
                politicaDevolucion: "Retorno automático a depósito",
                plazoComprometidoEstandar: TimeSpan.FromHours(48),
                plazoComprometidoUrgente: TimeSpan.FromHours(24));

        var fechaDispositivoUtc = request.FechaHoraDispositivoUtc ?? _clock.UtcNow;

        // 7. Lógica de Dominio y Máquina de Estados (Doble Transición Encapsulada)
        // Paso 1: EnTransito -> NoEntregado + EventoEnvio
        // Paso 2: Evaluación automática de ReglaOperativa -> Reprogramado ó EnDevolucion + EventoEnvio
        var (intento, estadoFinal, intentosRestantes) = envio.RegistrarIntentoFallido(
            parada.Id,
            motivo,
            reglaOperativa,
            request.Observaciones,
            request.FotoEvidenciaBase64,
            request.Latitud,
            request.Longitud,
            fechaDispositivoUtc,
            _currentTenant.UsuarioId);

        ruta.RegistrarIntentoFallidoEnParada(parada.Id);

        // 8. Persistencia del Integration Event en Outbox dentro de la misma transacción atómica
        var payloadEvento = JsonSerializer.Serialize(new
        {
            EnvioId = envio.Id,
            HojaDeRutaId = ruta.Id,
            ParadaId = parada.Id,
            CodigoSeguimiento = envio.CodigoSeguimientoPublico,
            EstadoTransicionPaso1 = EstadoEnvio.NoEntregado.ToString(),
            EstadoFinalResuelto = estadoFinal.ToString(),
            MotivoNoEntregaId = motivo.Id,
            MotivoCodigo = motivo.Codigo,
            MotivoDescripcion = motivo.Descripcion,
            Observaciones = request.Observaciones.Trim(),
            TieneFoto = !string.IsNullOrWhiteSpace(request.FotoEvidenciaBase64),
            IntentosAcumulados = envio.CantidadIntentos,
            IntentosRestantes = intentosRestantes,
            MaxIntentosPermitidos = reglaOperativa.MaxIntentosEntrega,
            PoliticaDevolucion = reglaOperativa.PoliticaDevolucion,
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
            nameof(Domain.Entities.Envios.Envio),
            envio.Id,
            "EnvioNoEntregadoIntegrationEvent",
            payloadEvento);

        // 9. Persistencia Atómica con IUnitOfWork
        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 10. Invalidación inmediata de la caché de seguimiento
        await _cacheService.RemoveAsync($"seguimiento:{envio.CodigoSeguimientoPublico}", cancellationToken);
        await _cacheService.RemoveAsync($"envio:{envio.Id}", cancellationToken);

        string mensaje = estadoFinal == EstadoEnvio.EnDevolucion
            ? $"Intento fallido registrado. Límite de {reglaOperativa.MaxIntentosEntrega} intentos alcanzado. El envío ha pasado a estado EnDevolucion."
            : $"Intento fallido registrado. El envío ha sido reprogramado. Quedan {intentosRestantes} intento(s) permitido(s).";

        return new ResultadoIntentoFallidoDto(
            envio.Id,
            ruta.Id,
            parada.Id,
            envio.CodigoSeguimientoPublico,
            estadoFinal.ToString(),
            envio.CantidadIntentos,
            intentosRestantes,
            reglaOperativa.MaxIntentosEntrega,
            motivo.Descripcion,
            fechaDispositivoUtc,
            false,
            mensaje
        );
    }
}
