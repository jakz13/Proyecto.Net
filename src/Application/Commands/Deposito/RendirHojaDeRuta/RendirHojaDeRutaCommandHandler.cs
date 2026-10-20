using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
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

    public RendirHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRendicionRepository rendicionRepository,
        IOutboxStorage outboxStorage,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _rendicionRepository = rendicionRepository;
        _outboxStorage = outboxStorage;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
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

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        // Idempotencia: una ruta ya rendida devuelve la rendición registrada sin repetir transiciones
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

        // Primero se resuelve el motivo interno (si hace falta) para no modificar nada ante un dato inválido
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
                        // Los bultos que no se presentan quedan pendientes: se resuelven luego (por ejemplo, declarándolos extraviados)
                        enviosPendientes.Add(envio.CodigoSeguimientoPublico);
                    }
                    break;
            }
        }

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

        return new RendicionResultDto(
            ruta.Id, ruta.Estado, entregados, devueltos, pendientes,
            reintegrados, enDevolucion, enviosPendientes, sobrantes, false,
            pendientes == 0 && sobrantes.Count == 0
                ? "Rendición registrada sin diferencias. El repartidor puede despachar otra hoja de ruta."
                : $"Rendición registrada con diferencias: {pendientes} bulto(s) pendiente(s) y {sobrantes.Count} código(s) sobrante(s).");
    }

    /// <summary>Bultos que salieron en el vehículo con esta ruta (la entrega no cambia el estado del bulto).</summary>
    private static IEnumerable<Bulto> ViajaronEnElVehiculo(Envio envio) =>
        envio.Bultos.Where(b => b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito");

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
