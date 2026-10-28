using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;

public class RecalcularIndicadoresCumplimientoCommandHandler : IRequestHandler<RecalcularIndicadoresCumplimientoCommand, ResultadoRecalculoIndicadores>
{
    private readonly IProcesosProgramadosRepository _procesosRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IIndicadorCumplimientoRepository _indicadorRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public RecalcularIndicadoresCumplimientoCommandHandler(
        IProcesosProgramadosRepository procesosRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IRutaRepository rutaRepository,
        IIndicadorCumplimientoRepository indicadorRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _procesosRepository = procesosRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _rutaRepository = rutaRepository;
        _indicadorRepository = indicadorRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ResultadoRecalculoIndicadores> Handle(RecalcularIndicadoresCumplimientoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (request.Fecha > ZonaHorariaOperacion.FechaLocalDe(_clock.UtcNow))
            throw new DomainValidationException("No se pueden calcular indicadores de una fecha futura.");

        var (desdeUtc, hastaUtc) = ZonaHorariaOperacion.LimitesUtcDelDia(request.Fecha);
        var periodo = request.Fecha.ToString("yyyy-MM-dd");

        var envios = await _procesosRepository.ListarEnviosConResultadoAsync(request.OperadorId, desdeUtc, hastaUtc, cancellationToken);

        var franjaIds = envios.Where(e => e.FranjaHorariaComprometidaId.HasValue).Select(e => e.FranjaHorariaComprometidaId!.Value).Distinct().ToList();
        var franjas = franjaIds.Count == 0
            ? new Dictionary<Guid, Domain.Entities.Operador.FranjaHoraria>()
            : (await _franjaHorariaRepository.GetByIdsAsync(franjaIds, cancellationToken)).ToDictionary(f => f.Id);

        var rutaIds = envios.Where(e => e.HojaDeRutaId.HasValue).Select(e => e.HojaDeRutaId!.Value).Distinct().ToList();
        var repartidorPorRuta = rutaIds.Count == 0
            ? new Dictionary<Guid, Guid?>()
            : (await _rutaRepository.GetByIdsAsync(rutaIds, request.OperadorId, cancellationToken)).ToDictionary(r => r.Id, r => r.RepartidorId);

        var acumulados = new Dictionary<ClaveIndicador, Acumulado>();
        foreach (var envio in envios)
        {
            var resultado = ResultadosDelDia.UltimoResultado(envio, desdeUtc, hastaUtc);
            if (resultado is null)
                continue;

            var franja = envio.FranjaHorariaComprometidaId.HasValue && franjas.TryGetValue(envio.FranjaHorariaComprometidaId.Value, out var f) ? f : null;
            Guid? repartidorId = envio.HojaDeRutaId.HasValue && repartidorPorRuta.TryGetValue(envio.HojaDeRutaId.Value, out var r) ? r : null;

            var entregado = resultado.EstadoNuevo == EstadoEnvio.Entregado;
            var enPlazo = entregado && ResultadosDelDia.EntregadoEnPlazo(resultado.Timestamp, franja);

            Acumular(acumulados, ClaveIndicador.General, entregado, enPlazo);
            if (envio.ZonaClasificadaId.HasValue)
                Acumular(acumulados, new ClaveIndicador(envio.ZonaClasificadaId, null, null), entregado, enPlazo);
            if (repartidorId.HasValue)
                Acumular(acumulados, new ClaveIndicador(null, repartidorId, null), entregado, enPlazo);
            Acumular(acumulados, new ClaveIndicador(null, null, envio.ComercioOperadorId), entregado, enPlazo);
        }

        // El indicador general se publica siempre: un día sin entregas es un 0 % explícito y no una ausencia de dato
        acumulados.TryAdd(ClaveIndicador.General, new Acumulado());

        var existentes = await _indicadorRepository.ListarPorPeriodoAsync(request.OperadorId, periodo, cancellationToken);
        var porClave = existentes.ToDictionary(i => new ClaveIndicador(i.ZonaCoberturaId, i.RepartidorId, i.ComercioOperadorId), i => i);

        int generados = 0, actualizados = 0;
        foreach (var (clave, a) in acumulados)
        {
            if (porClave.Remove(clave, out var indicador))
            {
                indicador.ActualizarMetricas(a.Total, a.EnPlazo, a.FueraDePlazo, a.NoEntregados);
                actualizados++;
            }
            else
            {
                await _indicadorRepository.AddAsync(
                    new IndicadorCumplimiento(Guid.NewGuid(), request.OperadorId, periodo, a.Total, a.EnPlazo, a.FueraDePlazo, a.NoEntregados,
                        clave.ZonaId, clave.RepartidorId, clave.ComercioOperadorId),
                    cancellationToken);
                generados++;
            }
        }

        // Los indicadores previos que ya no tienen resultados (por ejemplo, tras una corrección) se llevan a cero
        foreach (var obsoleto in porClave.Values)
        {
            obsoleto.ActualizarMetricas(0, 0, 0, 0);
            actualizados++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new ResultadoRecalculoIndicadores(periodo, acumulados[ClaveIndicador.General].Total, generados, actualizados);
    }

    private static void Acumular(Dictionary<ClaveIndicador, Acumulado> acumulados, ClaveIndicador clave, bool entregado, bool enPlazo)
    {
        if (!acumulados.TryGetValue(clave, out var a))
            acumulados[clave] = a = new Acumulado();

        a.Total++;
        if (!entregado)
            a.NoEntregados++;
        else if (enPlazo)
            a.EnPlazo++;
        else
            a.FueraDePlazo++;
    }

    private readonly record struct ClaveIndicador(Guid? ZonaId, Guid? RepartidorId, Guid? ComercioOperadorId)
    {
        public static ClaveIndicador General => default;
    }

    private sealed class Acumulado
    {
        public int Total;
        public int EnPlazo;
        public int FueraDePlazo;
        public int NoEntregados;
    }
}
