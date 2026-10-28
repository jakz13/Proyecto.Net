using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Planificacion.ArmarHojaDeRuta;

public class ArmarHojaDeRutaCommandHandler : IRequestHandler<ArmarHojaDeRutaCommand, ArmarHojaDeRutaResultDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IOutboxStorage _outboxStorage;

    public ArmarHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRepartidorRepository repartidorRepository,
        IVehiculoRepository vehiculoRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        IOutboxStorage outboxStorage)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _repartidorRepository = repartidorRepository;
        _vehiculoRepository = vehiculoRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _outboxStorage = outboxStorage;
    }

    public async Task<ArmarHojaDeRutaResultDto> Handle(ArmarHojaDeRutaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RepartidorId == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor es inválido.");

        if (request.VehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo es inválido.");

        if (request.EnviosIds is null || request.EnviosIds.Count == 0)
            throw new DomainValidationException("Debe seleccionar al menos un envío para la hoja de ruta.");

        if (request.EnviosIds.Any(id => id == Guid.Empty))
            throw new DomainValidationException("La lista de envíos contiene un identificador inválido.");

        var hoy = DateOnly.FromDateTime(_clock.UtcNow);
        if (request.Fecha < hoy.AddDays(-1))
            throw new DomainValidationException("La fecha de la hoja de ruta no puede estar en el pasado.");

        var despachadorId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al despachador.");
        var operadorId = _currentTenant.OperadorId;

        var repartidor = await _repartidorRepository.GetByIdAsync(request.RepartidorId, cancellationToken)
            ?? throw new NotFoundException($"Repartidor '{request.RepartidorId}' no encontrado.");
        if (repartidor.OperadorId != operadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el repartidor no corresponde al operador actual.");
        if (repartidor.Estado != "Disponible")
            throw new DomainValidationException($"El repartidor no está disponible (estado '{repartidor.Estado}').");

        var vehiculo = await _vehiculoRepository.GetByIdAsync(request.VehiculoId, cancellationToken)
            ?? throw new NotFoundException($"Vehículo '{request.VehiculoId}' no encontrado.");
        if (vehiculo.OperadorId != operadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el vehículo no corresponde al operador actual.");
        if (!vehiculo.Activo)
            throw new DomainValidationException($"El vehículo {vehiculo.Patente} no está disponible.");

        var regla = await _envioRepository.GetReglaOperativaVigenteAsync(operadorId, cancellationToken)
            ?? throw new DomainValidationException("El operador no tiene reglas operativas vigentes.");

        var excluidos = new List<EnvioExcluidoDto>();
        var idsSolicitados = request.EnviosIds.Distinct().ToList();
        var envios = await _envioRepository.GetByIdsAsync(idsSolicitados, cancellationToken);

        // Un envío de otro operador se informa como inexistente para no revelar su existencia
        var visibles = envios.Where(e => e.OperadorId == operadorId).ToDictionary(e => e.Id);
        var candidatos = new List<Envio>();
        foreach (var id in idsSolicitados)
        {
            if (!visibles.TryGetValue(id, out var envio))
            {
                excluidos.Add(new EnvioExcluidoDto(id, "El envío no existe."));
                continue;
            }

            var motivo = envio.ObtenerMotivoNoAsignable();
            if (motivo is not null)
            {
                excluidos.Add(new EnvioExcluidoDto(id, motivo));
                continue;
            }

            candidatos.Add(envio);
        }

        var franjas = await ObtenerFranjasAsync(candidatos, cancellationToken);
        var compatibles = new List<Envio>();
        foreach (var envio in candidatos)
        {
            var motivoFranja = ValidarFranja(envio, franjas, request.Fecha);
            if (motivoFranja is not null)
                excluidos.Add(new EnvioExcluidoDto(envio.Id, motivoFranja));
            else
                compatibles.Add(envio);
        }

        var incluidos = SeleccionarSegunRestricciones(
            Ordenar(compatibles, franjas), vehiculo, regla, excluidos);

        if (incluidos.Count == 0)
        {
            var detalle = string.Join(" ", excluidos.Select(e => $"[{e.EnvioId}] {e.Motivo}"));
            throw new DomainValidationException($"Ningún envío pudo incluirse en la hoja de ruta. {detalle}".Trim());
        }

        if (request.SoloValidar)
        {
            // Simulación: mismas restricciones y orden, sin asignar envíos ni persistir
            return new ArmarHojaDeRutaResultDto(
                Guid.Empty,
                "Validada",
                request.Fecha,
                repartidor.Id,
                vehiculo.Id,
                incluidos.Sum(e => e.PesoTotal),
                incluidos.Sum(e => e.VolumenTotal),
                incluidos.Select((e, i) => new ParadaArmadaDto(i + 1, Guid.Empty, e.Id, e.CodigoSeguimientoPublico)).ToList(),
                excluidos);
        }

        var ruta = new HojaDeRuta(Guid.NewGuid(), operadorId, request.Fecha);
        ruta.AsignarRecursos(repartidor.Id, vehiculo.Id);

        var paradasDto = new List<ParadaArmadaDto>();
        for (var i = 0; i < incluidos.Count; i++)
        {
            var envio = incluidos[i];
            ruta.AgregarParada(envio.Id, i + 1, envio.DireccionEntregaId!.Value, envio.FranjaHorariaComprometidaId);
            ruta.RegistrarAsignacion(envio.Id, despachadorId);
            envio.AsignarRuta(ruta.Id, despachadorId);
            paradasDto.Add(new ParadaArmadaDto(i + 1, ruta.Paradas.Last().Id, envio.Id, envio.CodigoSeguimientoPublico));
        }

        ruta.Liberar();

        await _rutaRepository.AddAsync(ruta, cancellationToken);
        await _envioRepository.UpdateRangeAsync(incluidos, cancellationToken);

        var eventos = incluidos
            .Select((e, i) => EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioAsignadoARuta, e, new { HojaDeRutaId = ruta.Id, OrdenParada = i + 1 }))
            .Append(EventosIntegracion.DeRuta(EventosIntegracion.Tipos.RutaArmada, ruta, new { ruta.Fecha, CantidadParadas = incluidos.Count }));
        await _outboxStorage.SaveRangeAsync(eventos, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ArmarHojaDeRutaResultDto(
            ruta.Id,
            ruta.Estado,
            ruta.Fecha,
            repartidor.Id,
            vehiculo.Id,
            incluidos.Sum(e => e.PesoTotal),
            incluidos.Sum(e => e.VolumenTotal),
            paradasDto,
            excluidos);
    }

    private async Task<Dictionary<Guid, FranjaHoraria>> ObtenerFranjasAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken)
    {
        var ids = envios
            .Where(e => e.FranjaHorariaComprometidaId.HasValue)
            .Select(e => e.FranjaHorariaComprometidaId!.Value)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, FranjaHoraria>();

        var franjas = await _franjaHorariaRepository.GetByIdsAsync(ids, cancellationToken);
        return franjas.ToDictionary(f => f.Id);
    }

    /// <summary>Un envío con franja comprometida solo es compatible si la franja existe, está activa y corresponde al día de la ruta.</summary>
    private static string? ValidarFranja(Envio envio, IReadOnlyDictionary<Guid, FranjaHoraria> franjas, DateOnly fecha)
    {
        if (!envio.FranjaHorariaComprometidaId.HasValue)
            return null;

        if (!franjas.TryGetValue(envio.FranjaHorariaComprometidaId.Value, out var franja) || !franja.Activa)
            return "La franja horaria comprometida del envío no está disponible.";

        if (franja.DiaSemana != fecha.DayOfWeek)
            return $"La franja comprometida corresponde a {franja.DiaSemana} y la hoja de ruta es del {fecha:dd/MM/yyyy} ({fecha.DayOfWeek}).";

        return null;
    }

    /// <summary>
    /// Orden de las paradas: hora de inicio de la franja comprometida (sin franja al final), prioridad máxima de sus bultos
    /// (la más alta primero), zona operativa para agrupar cercanía y, por último, el código de seguimiento.
    /// </summary>
    private static List<Envio> Ordenar(IEnumerable<Envio> envios, IReadOnlyDictionary<Guid, FranjaHoraria> franjas) =>
        envios
            .OrderBy(e => e.FranjaHorariaComprometidaId.HasValue && franjas.TryGetValue(e.FranjaHorariaComprometidaId.Value, out var f)
                ? f.HoraInicio
                : TimeOnly.MaxValue)
            .ThenByDescending(e => e.PrioridadMaxima ?? 0)
            .ThenBy(e => e.ZonaClasificadaId.HasValue ? 0 : 1)
            .ThenBy(e => e.ZonaClasificadaId)
            .ThenBy(e => e.CodigoSeguimientoPublico, StringComparer.Ordinal)
            .ToList();

    /// <summary>Recorre los envíos en orden e incluye cada uno mientras entre en la cantidad de paradas y en la capacidad del vehículo.</summary>
    private static List<Envio> SeleccionarSegunRestricciones(
        IEnumerable<Envio> ordenados,
        Vehiculo vehiculo,
        ReglaOperativa regla,
        List<EnvioExcluidoDto> excluidos)
    {
        var incluidos = new List<Envio>();
        decimal peso = 0, volumen = 0;

        foreach (var envio in ordenados)
        {
            if (incluidos.Count >= regla.MaxParadasPorRuta)
            {
                excluidos.Add(new EnvioExcluidoDto(envio.Id, $"Se superaría la cantidad máxima de paradas por ruta ({regla.MaxParadasPorRuta})."));
                continue;
            }

            if (peso + envio.PesoTotal > vehiculo.CapacidadPeso)
            {
                excluidos.Add(new EnvioExcluidoDto(envio.Id, $"Se superaría la capacidad de peso del vehículo ({vehiculo.CapacidadPeso})."));
                continue;
            }

            if (volumen + envio.VolumenTotal > vehiculo.CapacidadVolumen)
            {
                excluidos.Add(new EnvioExcluidoDto(envio.Id, $"Se superaría la capacidad de volumen del vehículo ({vehiculo.CapacidadVolumen})."));
                continue;
            }

            incluidos.Add(envio);
            peso += envio.PesoTotal;
            volumen += envio.VolumenTotal;
        }

        return incluidos;
    }
}
