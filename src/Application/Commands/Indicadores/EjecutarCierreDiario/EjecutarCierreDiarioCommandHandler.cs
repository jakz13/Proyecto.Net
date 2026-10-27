using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;

public class EjecutarCierreDiarioCommandHandler : IRequestHandler<EjecutarCierreDiarioCommand, ResultadoCierreDiario>
{
    private readonly ICierreDiarioRepository _cierreRepository;
    private readonly IProcesosProgramadosRepository _procesosRepository;
    private readonly IRutaRepository _rutaRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public EjecutarCierreDiarioCommandHandler(
        ICierreDiarioRepository cierreRepository,
        IProcesosProgramadosRepository procesosRepository,
        IRutaRepository rutaRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _cierreRepository = cierreRepository;
        _procesosRepository = procesosRepository;
        _rutaRepository = rutaRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ResultadoCierreDiario> Handle(EjecutarCierreDiarioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.OperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (request.Fecha > ZonaHorariaOperacion.FechaLocalDe(_clock.UtcNow))
            throw new DomainValidationException("No se puede cerrar una jornada futura.");

        var existente = await _cierreRepository.GetAsync(request.OperadorId, request.Fecha, cancellationToken);
        if (existente is not null)
            return new ResultadoCierreDiario(existente.Id, true, existente.ResumenJson);

        var (desdeUtc, hastaUtc) = ZonaHorariaOperacion.LimitesUtcDelDia(request.Fecha);

        var rutas = await _rutaRepository.ListarAsync(request.OperadorId, request.Fecha, null, null, cancellationToken);
        var envios = await _procesosRepository.ListarEnviosConResultadoAsync(request.OperadorId, desdeUtc, hastaUtc, cancellationToken);
        var admitidos = await _procesosRepository.ContarEnviosAdmitidosAsync(request.OperadorId, desdeUtc, hastaUtc, cancellationToken);
        var incidencias = await _procesosRepository.ContarIncidenciasAsync(request.OperadorId, desdeUtc, hastaUtc, cancellationToken);

        var resultados = envios
            .Select(e => ResultadosDelDia.UltimoResultado(e, desdeUtc, hastaUtc))
            .OfType<Domain.Entities.Envios.EventoEnvio>()
            .ToList();

        var resumen = JsonSerializer.Serialize(new
        {
            fecha = request.Fecha.ToString("yyyy-MM-dd"),
            enviosAdmitidos = admitidos,
            hojasDeRuta = new
            {
                total = rutas.Count,
                porEstado = rutas.GroupBy(r => r.Estado).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count())
            },
            resultadosDeEntrega = new
            {
                total = resultados.Count,
                entregados = resultados.Count(r => r.EstadoNuevo == EstadoEnvio.Entregado),
                noEntregados = resultados.Count(r => r.EstadoNuevo == EstadoEnvio.NoEntregado)
            },
            incidencias = new { abiertasEnElDia = incidencias.Abiertas, sinCerrarAlCierre = incidencias.SinCerrar }
        }, EventoEnvioPublicado.OpcionesJson);

        var cierre = new CierreDiarioOperacion(Guid.NewGuid(), request.OperadorId, request.Fecha, resumen);
        await _cierreRepository.AddAsync(cierre, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoCierreDiario(cierre.Id, false, resumen);
    }
}
