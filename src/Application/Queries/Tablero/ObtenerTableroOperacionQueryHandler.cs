using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Queries.Tablero;

public class ObtenerTableroOperacionQueryHandler : IRequestHandler<ObtenerTableroOperacionQuery, TableroOperacionDto>
{
    private static readonly string[] EstadosEnEjecucion = { "Despachada", "EnTransito" };
    private static readonly string[] EstadosPendientes = { "Pendiente", "EnCamino", "EnSitio" };

    private const string OrigenEnParada = "EnParada";
    private const string OrigenUltimoIntento = "UltimoIntentoDeEntrega";

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public ObtenerTableroOperacionQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        IUsuarioRepository usuarioRepository,
        IDireccionRepository direccionRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _repartidorRepository = repartidorRepository;
        _usuarioRepository = usuarioRepository;
        _direccionRepository = direccionRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<TableroOperacionDto> Handle(ObtenerTableroOperacionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ahoraUtc = _clock.UtcNow;

        // Siempre se acota al operador del usuario autenticado: nunca se consulta entre operadores
        var rutas = await _rutaRepository.ListarAsync(_currentTenant.OperadorId, null, null, EstadosEnEjecucion, cancellationToken);
        if (rutas.Count == 0)
            return new TableroOperacionDto(0, 0, 0, 0, 0, ahoraUtc, Array.Empty<VehiculoEnVivoDto>(), Array.Empty<EnvioRiesgoDto>());

        var contexto = await CargarContextoAsync(rutas, cancellationToken);
        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, ZonaHorariaOperacion.Zona);

        var vehiculosEnVivo = rutas
            .Select(r => ConstruirRuta(r, contexto, ahoraLocal))
            .OfType<VehiculoEnVivoDto>()
            .OrderByDescending(v => v.CantidadEnviosEnRiesgo)
            .ThenBy(v => v.NumeroRuta, StringComparer.Ordinal)
            .ToList();

        var alertas = vehiculosEnVivo
            .SelectMany(v => v.EnviosEnRiesgo)
            .OrderBy(a => SeveridadDe(a.NivelRiesgo))
            .ThenBy(a => a.MinutosParaCierreVentana)
            .ToList();

        var paradas = vehiculosEnVivo.SelectMany(v => v.Paradas).ToList();

        return new TableroOperacionDto(
            vehiculosEnVivo.Count,
            vehiculosEnVivo.Select(v => v.RepartidorId).Distinct().Count(),
            paradas.Count(p => p.Estado is "Pendiente" or "EnCurso"),
            paradas.Count(p => p.Estado == "Entregado"),
            alertas.Count,
            ahoraUtc,
            vehiculosEnVivo,
            alertas);
    }

    private async Task<ContextoTablero> CargarContextoAsync(IReadOnlyList<HojaDeRuta> rutas, CancellationToken ct)
    {
        var paradas = rutas.SelectMany(HojaDeRutaMapper.ParadasVigentes).ToList();

        var envios = (await _envioRepository.GetByIdsAsync(paradas.Select(p => p.EnvioId), ct))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(e => e.Id);

        var vehiculos = (await _vehiculoRepository.GetByIdsAsync(
                rutas.Where(r => r.VehiculoId.HasValue).Select(r => r.VehiculoId!.Value), ct))
            .Where(v => v.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(v => v.Id);

        var repartidores = (await _repartidorRepository.GetByIdsAsync(
                rutas.Where(r => r.RepartidorId.HasValue).Select(r => r.RepartidorId!.Value), ct))
            .Where(r => r.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(r => r.Id);

        var nombres = (await _usuarioRepository.ObtenerPorIdsAsync(
                repartidores.Values.Select(r => r.UsuarioId),
                _currentTenant.OperadorId,
                ct))
            .ToDictionary(u => u.Id, u => u.Nombre);

        var direccionIds = envios.Values
            .Where(e => e.DireccionEntregaId.HasValue)
            .Select(e => e.DireccionEntregaId!.Value)
            .Concat(paradas.Where(p => p.DireccionId != Guid.Empty).Select(p => p.DireccionId))
            .Distinct();

        var direcciones = (await _direccionRepository.GetByIdsAsync(direccionIds, ct))
            .ToDictionary(d => d.Id);

        var franjas = (await _franjaHorariaRepository.GetByIdsAsync(
                paradas.Select(p => p.FranjaHorariaId ?? envios.GetValueOrDefault(p.EnvioId)?.FranjaHorariaComprometidaId)
                    .OfType<Guid>(), ct))
            .ToDictionary(f => f.Id);

        return new ContextoTablero(envios, vehiculos, repartidores, nombres, direcciones, franjas);
    }

    private static VehiculoEnVivoDto? ConstruirRuta(HojaDeRuta ruta, ContextoTablero ctx, DateTime ahoraLocal)
    {
        // Una ruta en ejecución sin repartidor o unidad asignados no debería existir; se omite en lugar de mostrar datos incompletos
        if (ruta.RepartidorId is not { } repartidorId || ruta.VehiculoId is not { } vehiculoId)
            return null;
        if (!ctx.Repartidores.TryGetValue(repartidorId, out var repartidor) || !ctx.Vehiculos.TryGetValue(vehiculoId, out var vehiculo))
            return null;

        var numeroRuta = HojaDeRutaMapper.NumeroRuta(ruta);
        var nombreRepartidor = ctx.Nombres.GetValueOrDefault(repartidor.UsuarioId) ?? repartidor.Documento;

        var paradas = HojaDeRutaMapper.ParadasVigentes(ruta).Where(p => ctx.Envios.ContainsKey(p.EnvioId)).ToList();
        var pendientes = paradas.Where(p => EstadosPendientes.Contains(p.Estado)).ToList();

        var riesgos = new Dictionary<Guid, EnvioRiesgoDto>();
        for (var i = 0; i < pendientes.Count; i++)
        {
            var riesgo = EvaluarParada(pendientes[i], i + 1, ruta, ctx, ahoraLocal);
            if (riesgo is null)
                continue;

            var (envio, cierre, ventana, evaluacion) = riesgo.Value;
            var direccion = DireccionDe(envio, ctx);
            riesgos[envio.Id] = new EnvioRiesgoDto(
                envio.Id, ruta.Id, numeroRuta, envio.CodigoSeguimientoPublico, envio.NombreDestinatario,
                direccion?.ToString() ?? envio.DireccionDestino, ventana, cierre, evaluacion.MinutosParaCierreVentana,
                evaluacion.Nivel, evaluacion.Causa, nombreRepartidor, repartidor.Telefono, vehiculo.Patente,
                direccion?.Latitud, direccion?.Longitud);
        }

        var paradaActual = pendientes.FirstOrDefault(p => p.Estado == "EnSitio")
            ?? pendientes.FirstOrDefault(p => p.Estado == "EnCamino")
            ?? pendientes.FirstOrDefault();
        var envioActual = paradaActual is null ? null : ctx.Envios[paradaActual.EnvioId];

        var (latitud, longitud, direccionPosicion, origen, actualizadaEn) = UbicarUnidad(paradas, pendientes, ctx);

        return new VehiculoEnVivoDto(
            ruta.Id, numeroRuta, repartidor.Id, nombreRepartidor, repartidor.Telefono,
            vehiculo.Id, vehiculo.Patente, vehiculo.Tipo,
            latitud, longitud, direccionPosicion, origen, actualizadaEn,
            EstadoOperativoDe(pendientes),
            paradas.Count(p => p.Estado == "Entregada"),
            paradas.Count,
            envioActual?.CodigoSeguimientoPublico,
            envioActual is null ? null : DireccionTexto(envioActual, ctx),
            riesgos.Count > 0,
            riesgos.Count,
            riesgos.Values.OrderBy(r => SeveridadDe(r.NivelRiesgo)).ThenBy(r => r.MinutosParaCierreVentana).ToList(),
            paradas.Select(p => ConstruirParada(p, ctx, riesgos)).ToList());
    }

    /// <summary>La ventana aplica solo si la franja corresponde al día de la hoja de ruta; sin franja no hay compromiso que evaluar.</summary>
    private static (Envio Envio, DateTime Cierre, string Ventana, RiesgoVentana Riesgo)? EvaluarParada(
        Parada parada, int posicionPendiente, HojaDeRuta ruta, ContextoTablero ctx, DateTime ahoraLocal)
    {
        var envio = ctx.Envios[parada.EnvioId];
        var franjaId = parada.FranjaHorariaId ?? envio.FranjaHorariaComprometidaId;
        if (franjaId is null || !ctx.Franjas.TryGetValue(franjaId.Value, out var franja) || franja.DiaSemana != ruta.Fecha.DayOfWeek)
            return null;

        var cierre = ruta.Fecha.ToDateTime(franja.HoraFin);
        var riesgo = EvaluadorRiesgoVentana.Evaluar(ahoraLocal, cierre, posicionPendiente);
        return riesgo is null ? null : (envio, cierre, HojaDeRutaMapper.FormatearVentana(franja)!, riesgo);
    }

    /// <summary>
    /// Última posición conocida: la dirección de la parada donde el repartidor registró su arribo o, si no hay ninguna,
    /// el punto desde el que reportó su último intento de entrega. Sin ninguno de los dos no hay posición.
    /// </summary>
    private static (double? Latitud, double? Longitud, string? Direccion, string? Origen, DateTime? ActualizadaEn) UbicarUnidad(
        IReadOnlyList<Parada> paradas, IReadOnlyList<Parada> pendientes, ContextoTablero ctx)
    {
        foreach (var enSitio in pendientes.Where(p => p.Estado == "EnSitio"))
        {
            var envio = ctx.Envios[enSitio.EnvioId];
            var direccion = DireccionDe(envio, ctx, enSitio.DireccionId);
            if (direccion?.Latitud is { } lat && direccion.Longitud is { } lon)
                return (lat, lon, direccion.ToString(), OrigenEnParada, null);
        }

        var idsParadas = paradas.Select(p => p.Id).ToHashSet();
        var ultimo = paradas
            .SelectMany(p => ctx.Envios[p.EnvioId].IntentosEntrega.Where(i => i.ParadaId == p.Id))
            .Where(i => i.Latitud.HasValue && i.Longitud.HasValue && idsParadas.Contains(i.ParadaId))
            .OrderByDescending(i => i.Timestamp)
            .FirstOrDefault();

        if (ultimo is null)
            return (null, null, null, null, null);

        return (ultimo.Latitud, ultimo.Longitud, DireccionTexto(ctx.Envios[ultimo.EnvioId], ctx), OrigenUltimoIntento, ultimo.Timestamp);
    }

    private static ParadaEnVivoDto ConstruirParada(Parada parada, ContextoTablero ctx, IReadOnlyDictionary<Guid, EnvioRiesgoDto> riesgos)
    {
        var envio = ctx.Envios[parada.EnvioId];
        var direccion = DireccionDe(envio, ctx, parada.DireccionId);
        var franjaId = parada.FranjaHorariaId ?? envio.FranjaHorariaComprometidaId;
        var ventana = franjaId.HasValue && ctx.Franjas.TryGetValue(franjaId.Value, out var franja)
            ? HojaDeRutaMapper.FormatearVentana(franja)
            : null;
        riesgos.TryGetValue(envio.Id, out var riesgo);

        return new ParadaEnVivoDto(
            envio.Id, envio.CodigoSeguimientoPublico, parada.Orden, envio.NombreDestinatario, DireccionTexto(envio, ctx, parada.DireccionId),
            direccion?.Latitud, direccion?.Longitud, ventana, EstadoParadaPublico(parada.Estado), riesgo is not null, riesgo?.CausaRiesgo);
    }

    private static string EstadoParadaPublico(string estado) => estado switch
    {
        "Entregada" => "Entregado",
        "EnCamino" or "EnSitio" => "EnCurso",
        "Fallida" or "NoVisitada" => "NoEntregado",
        _ => "Pendiente"
    };

    private static string EstadoOperativoDe(IReadOnlyList<Parada> pendientes)
    {
        if (pendientes.Any(p => p.Estado == "EnSitio"))
            return "EnParada";
        return pendientes.Any(p => p.Estado == "EnCamino") ? "EnCamino" : "SinParadaActiva";
    }

    private static Direccion? DireccionDe(Envio envio, ContextoTablero ctx, Guid? paradaDireccionId = null)
    {
        if (envio.DireccionEntregaId.HasValue && ctx.Direcciones.TryGetValue(envio.DireccionEntregaId.Value, out var dirEnvio))
            return dirEnvio;

        if (paradaDireccionId.HasValue && ctx.Direcciones.TryGetValue(paradaDireccionId.Value, out var dirParada))
            return dirParada;

        return null;
    }

    private static string DireccionTexto(Envio envio, ContextoTablero ctx, Guid? paradaDireccionId = null) =>
        DireccionDe(envio, ctx, paradaDireccionId)?.ToString() ?? envio.DireccionDestino;

    private static int SeveridadDe(string nivel) => nivel switch
    {
        "Critico" => 0,
        "Alto" => 1,
        _ => 2
    };

    private sealed record ContextoTablero(
        IReadOnlyDictionary<Guid, Envio> Envios,
        IReadOnlyDictionary<Guid, Vehiculo> Vehiculos,
        IReadOnlyDictionary<Guid, Repartidor> Repartidores,
        IReadOnlyDictionary<Guid, string> Nombres,
        IReadOnlyDictionary<Guid, Direccion> Direcciones,
        IReadOnlyDictionary<Guid, FranjaHoraria> Franjas);
}
