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

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public DespacharHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
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

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var envios = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);

        // Idempotencia: despachar una ruta ya despachada informa su estado sin repetir transiciones
        if (ruta.Estado is "Despachada" or "EnTransito" or "Finalizada")
        {
            return new DespachoResultDto(
                ruta.Id, ruta.Estado, ruta.FechaDespacho,
                envios.Count(e => e.EstadoActual == EstadoEnvio.EnTransito),
                Array.Empty<EnvioRetiradoDto>(), true,
                "La hoja de ruta ya se encontraba despachada (operación idempotente).");
        }

        if (ruta.Estado != "EnCarga")
            throw new DomainValidationException($"La hoja de ruta está en estado '{ruta.Estado}': la carga del vehículo debe estar escaneada y sincronizada antes de despachar.");

        if (await _rutaRepository.ExisteRutaSinRendirAsync(ruta.RepartidorId!.Value, ruta.Id, cancellationToken))
            throw new DomainValidationException("El repartidor tiene otra hoja de ruta despachada sin rendir (UC-16).");

        // Un envío viaja solo si todos sus bultos recibidos fueron cargados
        var aDespachar = new List<Envio>();
        var aRetirar = new List<Envio>();
        foreach (var envio in envios.Where(e => e.EstadoActual == EstadoEnvio.AsignadoARuta))
        {
            var esperados = envio.Bultos.Where(b => b.EstadoBulto is "Recibido" or "Danado").ToList();
            if (esperados.Count > 0 && esperados.All(b => ruta.BultoFueCargado(b.Id)))
                aDespachar.Add(envio);
            else
                aRetirar.Add(envio);
        }

        if (aDespachar.Count == 0)
            throw new DomainValidationException("Ningún envío de la hoja de ruta tiene todos sus bultos cargados: no se puede despachar.");

        var mensajes = new List<OutboxMessage>();
        foreach (var envio in aRetirar)
        {
            envio.DesasignarDeRutaPorFaltante(MotivoNoCargado, usuarioId);
            ruta.DesasignarEnvioFaltante(envio.Id);

            mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioDesasignadoPorFaltanteIntegrationEvent", new
            {
                EnvioId = envio.Id,
                HojaDeRutaId = ruta.Id,
                Motivo = "FaltanteEnCarga",
                Timestamp = _clock.UtcNow
            }));
        }

        foreach (var envio in aDespachar)
        {
            envio.DespacharEnRuta(ruta.Id, usuarioId, request.Observaciones);

            mensajes.Add(CrearMensaje(nameof(Envio), envio.Id, "EnvioEnTransitoIntegrationEvent", new
            {
                EnvioId = envio.Id,
                HojaDeRutaId = ruta.Id,
                RepartidorId = ruta.RepartidorId,
                VehiculoId = ruta.VehiculoId,
                Estado = EstadoEnvio.EnTransito.ToString(),
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
        await _envioRepository.UpdateRangeAsync(aDespachar.Concat(aRetirar), cancellationToken);
        await _outboxStorage.SaveRangeAsync(mensajes, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var retirados = aRetirar
            .Select(e => new EnvioRetiradoDto(e.Id, e.CodigoSeguimientoPublico, MotivoNoCargado))
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
