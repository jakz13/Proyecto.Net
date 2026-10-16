using System.Text.Json;
using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;

public class ConfirmarCargaYDespacharRutaCommandHandler : IRequestHandler<ConfirmarCargaYDespacharRutaCommand, ConfirmarCargaResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxStorage _outboxStorage;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public ConfirmarCargaYDespacharRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IUnitOfWork unitOfWork,
        IOutboxStorage outboxStorage,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _unitOfWork = unitOfWork;
        _outboxStorage = outboxStorage;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<ConfirmarCargaResultDto> Handle(ConfirmarCargaYDespacharRutaCommand request, CancellationToken cancellationToken)
    {
        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new DomainValidationException("La clave de idempotencia es requerida.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: La hoja de ruta no corresponde al operador actual.");

        // Manejo estricto de Idempotencia para conectividad móvil intermitente (.NET MAUI)
        if (ruta.Estado == "EnTransito" || ruta.Estado == "Despachada")
        {
            var enviosActuales = await _envioRepository.GetByHojaDeRutaIdAsync(request.HojaDeRutaId, cancellationToken);
            return new ConfirmarCargaResultDto(
                ruta.Id,
                ruta.Estado,
                ruta.FechaDespacho ?? _clock.UtcNow,
                request.CodigosBultosCargados?.Count ?? 0,
                enviosActuales.Count(e => e.EstadoActual == EstadoEnvio.EnTransito),
                0,
                true,
                "La hoja de ruta ya se encontraba despachada e iniciada (Operación idempotente)."
            );
        }

        var codigosCargadosSet = (request.CodigosBultosCargados ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet();

        if (!codigosCargadosSet.Any())
            throw new DomainValidationException("Debe ingresar al menos un bulto cargado para despachar la ruta.");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(request.HojaDeRutaId, cancellationToken);
        var outboxMessages = new List<OutboxMessage>();

        int enviosEnTransito = 0;
        int enviosDesasignados = 0;

        foreach (var envio in envios)
        {
            var bultosEnvio = envio.Bultos.Select(b => b.Identificacion).ToHashSet();
            bool tieneBultosCargados = bultosEnvio.Any(b => codigosCargadosSet.Contains(b));
            bool todosCargados = bultosEnvio.All(b => codigosCargadosSet.Contains(b));

            if (tieneBultosCargados && todosCargados)
            {
                envio.DespacharEnRuta(
                    ruta.Id,
                    _currentTenant.UsuarioId,
                    request.ObservacionesDiscrepancia);

                enviosEnTransito++;

                var payloadEnvio = JsonSerializer.Serialize(new
                {
                    EnvioId = envio.Id,
                    HojaDeRutaId = ruta.Id,
                    RepartidorId = ruta.RepartidorId,
                    VehiculoId = ruta.VehiculoId,
                    Estado = EstadoEnvio.EnTransito.ToString(),
                    Timestamp = _clock.UtcNow
                });

                outboxMessages.Add(new OutboxMessage(
                    Guid.NewGuid(),
                    nameof(Domain.Entities.Envios.Envio),
                    envio.Id,
                    "EnvioEnTransitoIntegrationEvent",
                    payloadEnvio));
            }
            else
            {
                // Si el bulto quedó en tierra o faltante, se desasigna volviendo a EnDeposito
                envio.DesasignarDeRutaPorFaltante(
                    "Bulto faltante durante verificación de carga en depósito.",
                    _currentTenant.UsuarioId);

                ruta.DesasignarEnvioFaltante(envio.Id);
                enviosDesasignados++;

                var payloadDesasignado = JsonSerializer.Serialize(new
                {
                    EnvioId = envio.Id,
                    HojaDeRutaId = ruta.Id,
                    Motivo = "FaltanteEnCarga",
                    Timestamp = _clock.UtcNow
                });

                outboxMessages.Add(new OutboxMessage(
                    Guid.NewGuid(),
                    nameof(Domain.Entities.Envios.Envio),
                    envio.Id,
                    "EnvioDesasignadoPorFaltanteIntegrationEvent",
                    payloadDesasignado));
            }
        }

        // Lógica de dominio en HojaDeRuta
        ruta.DespacharConCarga(codigosCargadosSet, request.ObservacionesDiscrepancia);

        var payloadRuta = JsonSerializer.Serialize(new
        {
            HojaDeRutaId = ruta.Id,
            RepartidorId = ruta.RepartidorId,
            VehiculoId = ruta.VehiculoId,
            TotalBultosCargados = codigosCargadosSet.Count,
            TotalEnviosDespachados = enviosEnTransito,
            IdempotencyKey = request.IdempotencyKey,
            FechaDespacho = ruta.FechaDespacho ?? _clock.UtcNow
        });

        outboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(),
            nameof(Domain.Entities.Planificacion.HojaDeRuta),
            ruta.Id,
            "RutaDespachadaIntegrationEvent",
            payloadRuta));

        // Persistencia atómica
        await _rutaRepository.UpdateAsync(ruta, cancellationToken);
        await _envioRepository.UpdateRangeAsync(envios, cancellationToken);
        await _outboxStorage.SaveRangeAsync(outboxMessages, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmarCargaResultDto(
            ruta.Id,
            ruta.Estado,
            ruta.FechaDespacho ?? _clock.UtcNow,
            codigosCargadosSet.Count,
            enviosEnTransito,
            enviosDesasignados,
            false,
            "Carga confirmada y hoja de ruta despachada exitosamente."
        );
    }
}
