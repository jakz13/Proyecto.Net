using MediatR;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
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
    private readonly IRetiroRepository? _retiroRepository;
    private readonly IFuncionalidadesService? _funcionalidades;

    public ArmarHojaDeRutaCommandHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRepartidorRepository repartidorRepository,
        IVehiculoRepository vehiculoRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IUnitOfWork unitOfWork,
        ICurrentTenant currentTenant,
        IClock clock,
        IOutboxStorage outboxStorage,
        IRetiroRepository? retiroRepository = null,
        IFuncionalidadesService? funcionalidades = null)
    {
        _funcionalidades = funcionalidades;
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _repartidorRepository = repartidorRepository;
        _vehiculoRepository = vehiculoRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _clock = clock;
        _outboxStorage = outboxStorage;
        _retiroRepository = retiroRepository;
    }

    /// <summary>Un elemento candidato a parada: una entrega o un retiro, con lo que hace falta para ordenarlo y validarlo.</summary>
    private sealed record Candidato(Envio? Envio, Retiro? Retiro, TimeOnly InicioFranja, int Prioridad, Guid? Zona, string Codigo)
    {
        public bool EsRetiro => Retiro is not null;
        public Guid Id => EsRetiro ? Retiro!.Id : Envio!.Id;
        public decimal Peso => EsRetiro ? Retiro!.PesoTotal : Envio!.PesoTotal;
        public decimal Volumen => EsRetiro ? Retiro!.VolumenTotal : Envio!.VolumenTotal;
    }

    public async Task<ArmarHojaDeRutaResultDto> Handle(ArmarHojaDeRutaCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RepartidorId == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor es inválido.");

        if (request.VehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo es inválido.");

        var enviosIds = request.EnviosIds ?? Array.Empty<Guid>();
        var retirosIds = request.RetirosIds ?? Array.Empty<Guid>();

        if (enviosIds.Count == 0 && retirosIds.Count == 0)
            throw new DomainValidationException("Debe seleccionar al menos un envío o un retiro para la hoja de ruta.");

        if (enviosIds.Any(id => id == Guid.Empty))
            throw new DomainValidationException("La lista de envíos contiene un identificador inválido.");

        if (retirosIds.Any(id => id == Guid.Empty))
            throw new DomainValidationException("La lista de retiros contiene un identificador inválido.");

        if (retirosIds.Count > 0 && _retiroRepository is null)
            throw new DomainValidationException("La planificación de retiros no está disponible.");

        if (retirosIds.Count > 0)
            await _funcionalidades.ExigirAsync(_currentTenant.OperadorId, Domain.Entities.Operador.Funcionalidad.RetiroEnDomicilio, cancellationToken);

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
        var idsSolicitados = enviosIds.Distinct().ToList();
        var envios = idsSolicitados.Count == 0
            ? new List<Envio>()
            : await _envioRepository.GetByIdsAsync(idsSolicitados, cancellationToken);

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

        var retirosCandidatos = await CargarRetirosAsync(retirosIds.Distinct().ToList(), operadorId, excluidos, cancellationToken);

        var franjas = await ObtenerFranjasAsync(candidatos, retirosCandidatos, cancellationToken);
        var compatibles = new List<Candidato>();
        foreach (var envio in candidatos)
        {
            var motivoFranja = ValidarFranja(envio.FranjaHorariaComprometidaId, franjas, request.Fecha);
            if (motivoFranja is not null)
            {
                excluidos.Add(new EnvioExcluidoDto(envio.Id, motivoFranja));
                continue;
            }

            compatibles.Add(new Candidato(
                envio, null, InicioDeFranja(envio.FranjaHorariaComprometidaId, franjas), (int)(envio.PrioridadMaxima ?? 0),
                envio.ZonaClasificadaId, envio.CodigoSeguimientoPublico));
        }

        foreach (var retiro in retirosCandidatos)
        {
            if (retiro.FechaSolicitada.HasValue && retiro.FechaSolicitada.Value != request.Fecha)
            {
                excluidos.Add(new EnvioExcluidoDto(retiro.Id, $"El retiro fue pedido para el {retiro.FechaSolicitada:dd/MM/yyyy}, no para el {request.Fecha:dd/MM/yyyy}."));
                continue;
            }

            var motivoFranja = ValidarFranja(retiro.FranjaHorariaId, franjas, request.Fecha);
            if (motivoFranja is not null)
            {
                excluidos.Add(new EnvioExcluidoDto(retiro.Id, motivoFranja));
                continue;
            }

            compatibles.Add(new Candidato(
                null, retiro, InicioDeFranja(retiro.FranjaHorariaId, franjas), 0, null, retiro.CodigoSeguimientoPublico));
        }

        var incluidos = SeleccionarSegunRestricciones(Ordenar(compatibles), vehiculo, regla, excluidos, out var perfil);

        if (incluidos.Count == 0)
        {
            var detalle = string.Join(" ", excluidos.Select(e => $"[{e.EnvioId}] {e.Motivo}"));
            var sujeto = retirosIds.Count > 0 ? "Ningún envío ni retiro" : "Ningún envío";
            throw new DomainValidationException($"{sujeto} pudo incluirse en la hoja de ruta. {detalle}".Trim());
        }

        var enviosIncluidos = incluidos.Where(c => !c.EsRetiro).Select(c => c.Envio!).ToList();
        var pesoTotal = incluidos.Sum(c => c.Peso);
        var volumenTotal = incluidos.Sum(c => c.Volumen);

        if (request.SoloValidar)
        {
            // Simulación: mismas restricciones y orden, sin asignar envíos ni persistir
            return new ArmarHojaDeRutaResultDto(
                Guid.Empty,
                "Validada",
                request.Fecha,
                repartidor.Id,
                vehiculo.Id,
                enviosIncluidos.Sum(e => e.PesoTotal),
                enviosIncluidos.Sum(e => e.VolumenTotal),
                incluidos.Select((c, i) => ParadaDe(i + 1, Guid.Empty, c)).ToList(),
                excluidos,
                perfil.PicoPeso,
                perfil.PicoVolumen);
        }

        var ruta = new HojaDeRuta(Guid.NewGuid(), operadorId, request.Fecha);
        ruta.AsignarRecursos(repartidor.Id, vehiculo.Id);

        var paradasDto = new List<ParadaArmadaDto>();
        var retirosAsignados = new List<Retiro>();
        for (var i = 0; i < incluidos.Count; i++)
        {
            var candidato = incluidos[i];
            if (candidato.EsRetiro)
            {
                var retiro = candidato.Retiro!;
                var parada = ruta.AgregarParadaRetiro(retiro.Id, i + 1, retiro.DireccionRetiroId, retiro.FranjaHorariaId);
                retiro.AsignarARuta(ruta.Id, despachadorId);
                retirosAsignados.Add(retiro);
                paradasDto.Add(ParadaDe(i + 1, parada.Id, candidato));
            }
            else
            {
                var envio = candidato.Envio!;
                ruta.AgregarParada(envio.Id, i + 1, envio.DireccionEntregaId!.Value, envio.FranjaHorariaComprometidaId);
                ruta.RegistrarAsignacion(envio.Id, despachadorId);
                envio.AsignarRuta(ruta.Id, despachadorId);
                paradasDto.Add(ParadaDe(i + 1, ruta.Paradas.Last().Id, candidato));
            }
        }

        ruta.Liberar();

        await _rutaRepository.AddAsync(ruta, cancellationToken);
        if (enviosIncluidos.Count > 0)
            await _envioRepository.UpdateRangeAsync(enviosIncluidos, cancellationToken);

        foreach (var retiro in retirosAsignados)
            await _retiroRepository!.UpdateAsync(retiro, cancellationToken);

        var eventos = incluidos
            .Select((c, i) => (Candidato: c, Orden: i + 1))
            .Where(x => !x.Candidato.EsRetiro)
            .Select(x => EventosIntegracion.DeEnvio(EventosIntegracion.Tipos.EnvioAsignadoARuta, x.Candidato.Envio!, new { HojaDeRutaId = ruta.Id, OrdenParada = x.Orden }))
            .Append(EventosIntegracion.DeRuta(EventosIntegracion.Tipos.RutaArmada, ruta, new { ruta.Fecha, CantidadParadas = incluidos.Count, CantidadRetiros = retirosAsignados.Count }));
        await _outboxStorage.SaveRangeAsync(eventos, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ArmarHojaDeRutaResultDto(
            ruta.Id,
            ruta.Estado,
            ruta.Fecha,
            repartidor.Id,
            vehiculo.Id,
            enviosIncluidos.Sum(e => e.PesoTotal),
            enviosIncluidos.Sum(e => e.VolumenTotal),
            paradasDto,
            excluidos,
            perfil.PicoPeso,
            perfil.PicoVolumen);
    }

    private static ParadaArmadaDto ParadaDe(int orden, Guid paradaId, Candidato c) => c.EsRetiro
        ? new ParadaArmadaDto(orden, paradaId, Guid.Empty, c.Codigo, Parada.TipoRetiro, c.Retiro!.Id)
        : new ParadaArmadaDto(orden, paradaId, c.Envio!.Id, c.Codigo);

    /// <summary>Un retiro de otro operador se informa como inexistente; solo se asigna uno que siga solicitado.</summary>
    private async Task<List<Retiro>> CargarRetirosAsync(
        IReadOnlyList<Guid> ids, Guid operadorId, List<EnvioExcluidoDto> excluidos, CancellationToken cancellationToken)
    {
        var resultado = new List<Retiro>();
        if (ids.Count == 0)
            return resultado;

        var retiros = await _retiroRepository!.GetByIdsAsync(ids, cancellationToken);
        var visibles = retiros.Where(r => r.OperadorId == operadorId).ToDictionary(r => r.Id);

        foreach (var id in ids)
        {
            if (!visibles.TryGetValue(id, out var retiro))
            {
                excluidos.Add(new EnvioExcluidoDto(id, "El retiro no existe."));
                continue;
            }

            var motivo = retiro.ObtenerMotivoNoAsignable();
            if (motivo is not null)
            {
                excluidos.Add(new EnvioExcluidoDto(id, motivo));
                continue;
            }

            resultado.Add(retiro);
        }

        return resultado;
    }

    private async Task<Dictionary<Guid, FranjaHoraria>> ObtenerFranjasAsync(
        IEnumerable<Envio> envios, IEnumerable<Retiro> retiros, CancellationToken cancellationToken)
    {
        var ids = envios
            .Where(e => e.FranjaHorariaComprometidaId.HasValue)
            .Select(e => e.FranjaHorariaComprometidaId!.Value)
            .Concat(retiros.Where(r => r.FranjaHorariaId.HasValue).Select(r => r.FranjaHorariaId!.Value))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<Guid, FranjaHoraria>();

        var franjas = await _franjaHorariaRepository.GetByIdsAsync(ids, cancellationToken);
        return franjas.ToDictionary(f => f.Id);
    }

    /// <summary>Un elemento con franja comprometida solo es compatible si la franja existe, está activa y corresponde al día de la ruta.</summary>
    private static string? ValidarFranja(Guid? franjaId, IReadOnlyDictionary<Guid, FranjaHoraria> franjas, DateOnly fecha)
    {
        if (!franjaId.HasValue)
            return null;

        if (!franjas.TryGetValue(franjaId.Value, out var franja) || !franja.Activa)
            return "La franja horaria comprometida no está disponible.";

        if (franja.DiaSemana != fecha.DayOfWeek)
            return $"La franja comprometida corresponde a {franja.DiaSemana} y la hoja de ruta es del {fecha:dd/MM/yyyy} ({fecha.DayOfWeek}).";

        return null;
    }

    private static TimeOnly InicioDeFranja(Guid? franjaId, IReadOnlyDictionary<Guid, FranjaHoraria> franjas) =>
        franjaId.HasValue && franjas.TryGetValue(franjaId.Value, out var f) ? f.HoraInicio : TimeOnly.MaxValue;

    /// <summary>
    /// Orden de las paradas: hora de inicio de la franja comprometida (sin franja al final), prioridad máxima de sus bultos
    /// (la más alta primero), zona operativa para agrupar cercanía y, por último, el código de seguimiento.
    /// Las entregas y los retiros se ordenan con el mismo criterio, de modo que la ruta mixta respeta las ventanas de ambos.
    /// </summary>
    private static List<Candidato> Ordenar(IEnumerable<Candidato> candidatos) =>
        candidatos
            .OrderBy(c => c.InicioFranja)
            .ThenByDescending(c => c.Prioridad)
            .ThenBy(c => c.Zona.HasValue ? 0 : 1)
            .ThenBy(c => c.Zona)
            .ThenBy(c => c.Codigo, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Recorre los candidatos en orden e incluye cada uno mientras entre en la cantidad de paradas y en la capacidad del vehículo.
    /// La capacidad se valida a lo largo del recorrido (RF 39): las entregas salen cargadas desde el depósito y los retiros suman carga
    /// desde la parada en que se suben, por lo que cada incorporación se verifica sobre la ruta completa en orden.
    /// </summary>
    private static List<Candidato> SeleccionarSegunRestricciones(
        IEnumerable<Candidato> ordenados,
        Vehiculo vehiculo,
        ReglaOperativa regla,
        List<EnvioExcluidoDto> excluidos,
        out ResultadoPerfilCarga perfilFinal)
    {
        var incluidos = new List<Candidato>();
        perfilFinal = new ResultadoPerfilCarga(true, 0, 0, null, null);

        foreach (var candidato in ordenados)
        {
            if (incluidos.Count >= regla.MaxParadasPorRuta)
            {
                excluidos.Add(new EnvioExcluidoDto(candidato.Id, $"Se superaría la cantidad máxima de paradas por ruta ({regla.MaxParadasPorRuta})."));
                continue;
            }

            var prueba = incluidos.Append(candidato)
                .Select(c => new ParadaCarga(c.EsRetiro, c.Peso, c.Volumen))
                .ToList();
            var perfil = PerfilCargaRuta.Evaluar(prueba, vehiculo.CapacidadPeso, vehiculo.CapacidadVolumen);

            if (!perfil.Cabe)
            {
                excluidos.Add(new EnvioExcluidoDto(candidato.Id, MotivoDeCapacidad(perfil, incluidos, candidato, vehiculo)));
                continue;
            }

            incluidos.Add(candidato);
            perfilFinal = perfil;
        }

        return incluidos;
    }

    private static string MotivoDeCapacidad(ResultadoPerfilCarga perfil, List<Candidato> incluidos, Candidato candidato, Vehiculo vehiculo)
    {
        // Con la carga inicial excedida se conserva el mensaje histórico, que es el que ven los despachadores
        if (perfil.PosicionQueExcede == 0)
        {
            var pesoInicial = incluidos.Append(candidato).Where(c => !c.EsRetiro).Sum(c => c.Peso);
            return pesoInicial > vehiculo.CapacidadPeso
                ? $"Se superaría la capacidad de peso del vehículo ({vehiculo.CapacidadPeso})."
                : $"Se superaría la capacidad de volumen del vehículo ({vehiculo.CapacidadVolumen}).";
        }

        return perfil.Motivo ?? "Se superaría la capacidad del vehículo.";
    }
}
