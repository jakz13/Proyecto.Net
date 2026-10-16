using System.Text.Json;
using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
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

    public RegistrarEntregaExitosaCommandHandler(
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

    public async Task<EntregaExitosaResultDto> Handle(RegistrarEntregaExitosaCommand request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.NombreReceptor))
            throw new DomainValidationException("El nombre del receptor es requerido.");

        if (string.IsNullOrWhiteSpace(request.DocumentoReceptor))
            throw new DomainValidationException("El documento del receptor es requerido.");

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
                "La entrega ya se encontraba registrada previamente (Operación idempotente)."
            );
        }

        // 5. Obtención y evaluación de la Regla Operativa vigente del operador
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

        // 6. Lógica de Dominio encapsulada: Registro de PruebaDeEntrega, IntentoEntrega y Transición de Estados
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
            _currentTenant.UsuarioId);

        ruta.RegistrarEntregaExitosaEnParada(parada.Id);

        // 7. Generación del Integration Event en Outbox dentro de la misma transacción atómica
        var payloadEvento = JsonSerializer.Serialize(new
        {
            EnvioId = envio.Id,
            HojaDeRutaId = ruta.Id,
            ParadaId = parada.Id,
            CodigoSeguimiento = envio.CodigoSeguimientoPublico,
            Estado = EstadoEnvio.Entregado.ToString(),
            NombreReceptor = request.NombreReceptor.Trim(),
            DocumentoReceptor = request.DocumentoReceptor.Trim(),
            TieneFirma = !string.IsNullOrWhiteSpace(request.FirmaDigitalBase64),
            TieneFoto = !string.IsNullOrWhiteSpace(request.FotoEvidenciaBase64),
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
            "EnvioEntregadoIntegrationEvent",
            payloadEvento);

        // 8. Persistencia Atómica con IUnitOfWork
        await _envioRepository.UpdateAsync(envio, cancellationToken);
        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _outboxStorage.SaveAsync(outboxMessage, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 9. Invalidación inmediata de la caché de seguimiento público
        await _cacheService.RemoveAsync($"seguimiento:{envio.CodigoSeguimientoPublico}", cancellationToken);
        await _cacheService.RemoveAsync($"envio:{envio.Id}", cancellationToken);

        return new EntregaExitosaResultDto(
            envio.Id,
            ruta.Id,
            parada.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            parada.Estado,
            fechaDispositivoUtc,
            false,
            "Entrega exitosa registrada correctamente."
        );
    }
}
