using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Tablero de operación en vivo: flota en calle y envíos en riesgo de incumplir su ventana (UC-12, RF-29).</summary>
public static class TableroEnVivoEndpoints
{
    public static IEndpointRouteBuilder MapTableroEnVivoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/tablero")
            .RequireAuthorization(PoliticasApi.Operaciones)
            .WithTags("Tablero de operación en vivo (UC-12, RF-29)");

        grupo.MapGet("/resumen", (AppDbContext db, ICurrentTenant tenant, IClock reloj, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var ahora = reloj.UtcNow;
                var vehiculos = await ConstructorTablero.ConstruirAsync(db, tenant.OperadorId, ahora, null, ct);

                var hoy = ConstructorTablero.FechaLocal(ahora);
                var rutasDeHoy = db.HojasDeRuta.AsNoTracking()
                    .Where(h => h.OperadorId == tenant.OperadorId && h.Fecha == hoy)
                    .Select(h => h.Id);
                var entregadosHoy = await db.Paradas.AsNoTracking()
                    .CountAsync(p => rutasDeHoy.Contains(p.HojaDeRutaId) && p.Estado == "Entregada", ct);
                var enTransito = await db.Envios.AsNoTracking()
                    .CountAsync(e => e.OperadorId == tenant.OperadorId && e.EstadoActual == EstadoEnvio.EnTransito, ct);

                // El retiro en domicilio se incorpora al tablero (RF 41): cuántos esperan una ruta y cuántos están en gestión
                var retirosPorEstado = await db.Retiros.AsNoTracking().IgnoreAutoIncludes()
                    .Where(r => r.OperadorId == tenant.OperadorId)
                    .GroupBy(r => r.EstadoActual)
                    .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                    .ToListAsync(ct);
                var retirosPendientes = retirosPorEstado.Where(r => r.Estado == EstadoRetiro.Solicitado).Sum(r => r.Cantidad);
                var retirosEnCurso = retirosPorEstado
                    .Where(r => r.Estado is EstadoRetiro.Solicitado or EstadoRetiro.AsignadoARuta or EstadoRetiro.Retirado or EstadoRetiro.NoRetirado or EstadoRetiro.EnDeposito)
                    .Sum(r => r.Cantidad);

                var alertas = vehiculos.SelectMany(v => v.EnviosEnRiesgo)
                    .OrderByDescending(a => ConstructorTablero.PesoNivel(a.NivelRiesgo))
                    .ThenByDescending(a => a.MinutosRetrasoEstimado)
                    .ToList();

                return Results.Ok(new ResumenTableroDto
                {
                    TotalRutasEnEjecucion = vehiculos.Count,
                    TotalRepartidoresEnCalle = vehiculos.Select(v => v.RepartidorId).Distinct().Count(),
                    TotalEnviosEnTransito = enTransito,
                    TotalEnviosEntregadosHoy = entregadosHoy,
                    TotalEnviosEnRiesgo = alertas.Count,
                    RetirosPendientes = retirosPendientes,
                    RetirosEnCurso = retirosEnCurso,
                    UltimaSincronizacion = ahora,
                    Vehiculos = vehiculos,
                    AlertasRiesgo = alertas
                });
            }))
            .WithName("ObtenerResumenTablero")
            .WithSummary("Métricas, posición de la flota y envíos en riesgo de la jornada");

        grupo.MapGet("/hojas-de-ruta/{hojaDeRutaId:guid}", (Guid hojaDeRutaId, AppDbContext db, ICurrentTenant tenant, IClock reloj, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var vehiculos = await ConstructorTablero.ConstruirAsync(db, tenant.OperadorId, reloj.UtcNow, hojaDeRutaId, ct);
                var vehiculo = vehiculos.FirstOrDefault();
                return vehiculo is null
                    ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "La hoja de ruta no está en ejecución.")
                    : Results.Ok(vehiculo);
            }))
            .WithName("ObtenerDetalleVehiculoEnVivo")
            .WithSummary("Telemetría y paradas de una hoja de ruta en curso");

        return app;
    }

}

/// <summary>Arma los datos del tablero a partir de las rutas despachadas, sus paradas y la última posición de cada vehículo.</summary>
internal static class ConstructorTablero
{
    private static readonly string[] EstadosRutaEnEjecucion = { "Liberada", "EnCarga", "Despachada", "EnTransito" };
    private static readonly TimeSpan SinSenal = TimeSpan.FromMinutes(10);
    private const double VelocidadDetenidoKmh = 3;

    public static DateOnly FechaLocal(DateTime utc) => ZonaHorariaOperador.FechaLocal(utc);

    public static int PesoNivel(string nivel) => nivel switch { "Critico" => 3, "Alto" => 2, _ => 1 };

    public static async Task<List<VehiculoEnVivoDto>> ConstruirAsync(
        AppDbContext db, Guid operadorId, DateTime ahora, Guid? hojaDeRutaId, CancellationToken ct)
    {
        var estados = EstadosRutaEnEjecucion.ToList();
        var rutas = await db.HojasDeRuta.AsNoTracking()
            .Where(h => h.OperadorId == operadorId && estados.Contains(h.Estado) && (hojaDeRutaId == null || h.Id == hojaDeRutaId))
            .ToListAsync(ct);

        var desde = ahora.AddHours(-24);
        var ultimasPosicionesOperador = await db.PosicionesVehiculo.AsNoTracking()
            .Where(p => p.OperadorId == operadorId && p.Timestamp > desde)
            .GroupBy(p => p.VehiculoId)
            .Select(g => g.OrderByDescending(p => p.Timestamp).First())
            .ToListAsync(ct);

        // Cargar también unidades de flota activas (repartidor activo con vehículo asignado) del operador cuando se pide el tablero general
        var repartidoresFlotaActiva = hojaDeRutaId == null
            ? await db.Repartidores.AsNoTracking()
                .Where(r => r.OperadorId == operadorId && r.Estado != "Inactivo" && r.VehiculoAsignadoId.HasValue)
                .ToListAsync(ct)
            : new List<Repartidor>();

        if (rutas.Count == 0 && ultimasPosicionesOperador.Count == 0 && repartidoresFlotaActiva.Count == 0)
            return new List<VehiculoEnVivoDto>();

        var rutaIds = rutas.Select(r => r.Id).ToList();
        var repartidorIds = rutas.Where(r => r.RepartidorId.HasValue).Select(r => r.RepartidorId!.Value)
            .Union(repartidoresFlotaActiva.Select(r => r.Id))
            .Distinct()
            .ToList();
        var vehiculoIds = rutas.Where(r => r.VehiculoId.HasValue).Select(r => r.VehiculoId!.Value)
            .Union(repartidoresFlotaActiva.Where(r => r.VehiculoAsignadoId.HasValue).Select(r => r.VehiculoAsignadoId!.Value))
            .Distinct()
            .ToList();
        var paradas = rutas.SelectMany(r => r.Paradas).Where(p => p.Estado != "Cancelada").ToList();
        var envioIds = paradas.Where(p => p.EnvioId.HasValue).Select(p => p.EnvioId!.Value).Distinct().ToList();
        var direccionIds = paradas.Select(p => p.DireccionId).Distinct().ToList();

        var todosVehiculoIds = vehiculoIds.Union(ultimasPosicionesOperador.Select(p => p.VehiculoId)).Distinct().ToList();
        var todosRepartidorIds = repartidorIds.Union(ultimasPosicionesOperador.Where(p => p.RepartidorId.HasValue).Select(p => p.RepartidorId!.Value)).Distinct().ToList();

        // Obtener última posición histórica para vehículos de rutas o flota que no hayan reportado en las últimas 24h
        var vehiculosSinPosicionReciente = todosVehiculoIds
            .Except(ultimasPosicionesOperador.Select(p => p.VehiculoId))
            .ToList();
        var posicionesHistoricas = vehiculosSinPosicionReciente.Count > 0
            ? await db.PosicionesVehiculo.AsNoTracking()
                .Where(p => p.OperadorId == operadorId && vehiculosSinPosicionReciente.Contains(p.VehiculoId))
                .GroupBy(p => p.VehiculoId)
                .Select(g => g.OrderByDescending(p => p.Timestamp).First())
                .ToListAsync(ct)
            : new List<PosicionVehiculo>();

        var repartidores = await db.Repartidores.AsNoTracking().Where(r => todosRepartidorIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var usuarioIds = repartidores.Values.Select(r => r.UsuarioId).Distinct().ToList();
        var nombres = await db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        var vehiculos = await db.Vehiculos.AsNoTracking().Where(v => todosVehiculoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var envios = await db.Envios.AsNoTracking().Where(e => envioIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var destinatarioIds = envios.Values.Select(e => e.DestinatarioId).Distinct().ToList();
        var telefonos = await db.Destinatarios.AsNoTracking().Where(d => destinatarioIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Telefono ?? string.Empty, ct);
        var direcciones = await db.Direcciones.AsNoTracking().Where(d => direccionIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var zonaIds = direcciones.Values.Where(d => d.ZonaCoberturaId.HasValue).Select(d => d.ZonaCoberturaId!.Value).Distinct().ToList();
        var zonas = await db.ZonasCobertura.AsNoTracking().Where(z => zonaIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);

        var franjaIds = paradas.Where(p => p.FranjaHorariaId.HasValue).Select(p => p.FranjaHorariaId!.Value)
            .Union(envios.Values.Where(e => e.FranjaHorariaComprometidaId.HasValue).Select(e => e.FranjaHorariaComprometidaId!.Value))
            .Distinct()
            .ToList();
        var franjas = await db.FranjasHorarias.AsNoTracking().Where(f => franjaIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var ultimasPosiciones = ultimasPosicionesOperador
            .Concat(posicionesHistoricas)
            .GroupBy(p => p.VehiculoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Timestamp).First());

        var cerrada = Domain.Entities.Incidencias.Incidencia.EstadoCerrada;
        var incidenciasAbiertas = (rutaIds.Count > 0 || envioIds.Count > 0)
            ? await db.Incidencias.AsNoTracking()
                .Where(i => i.OperadorId == operadorId && i.Estado != cerrada &&
                            ((i.HojaDeRutaId != null && rutaIds.Contains(i.HojaDeRutaId.Value)) ||
                             (i.EnvioId != null && envioIds.Contains(i.EnvioId.Value))))
                .Select(i => new { i.HojaDeRutaId, i.EnvioId, i.Tipo })
                .ToListAsync(ct)
            : new();
        var incidenciaPorRuta = incidenciasAbiertas
            .Where(i => i.HojaDeRutaId.HasValue)
            .GroupBy(i => i.HojaDeRutaId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Tipo);
        var incidenciaPorEnvio = incidenciasAbiertas
            .Where(i => i.EnvioId.HasValue)
            .GroupBy(i => i.EnvioId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Tipo);

        var resultado = new List<VehiculoEnVivoDto>();
        foreach (var ruta in rutas.OrderBy(r => r.Fecha).ThenBy(r => r.Id))
        {
            var repartidor = ruta.RepartidorId.HasValue ? repartidores.GetValueOrDefault(ruta.RepartidorId.Value) : null;
            var vehiculo = ruta.VehiculoId.HasValue ? vehiculos.GetValueOrDefault(ruta.VehiculoId.Value) : null;
            var posicion = ruta.VehiculoId.HasValue ? ultimasPosiciones.GetValueOrDefault(ruta.VehiculoId.Value) : null;
            var numero = HojaDeRutaMapper.NumeroRuta(ruta);
            incidenciaPorRuta.TryGetValue(ruta.Id, out var incidenciaAbiertaRuta);

            var vigentes = ruta.Paradas.Where(p => p.Estado != "Cancelada").OrderBy(p => p.Orden).ToList();
            var pendientes = vigentes.Where(EstaPendiente).ToList();
            PuntoRuta? origen = posicion is null ? null : new PuntoRuta(posicion.Latitud, posicion.Longitud);

            var enRiesgo = new List<EnvioRiesgoDto>();
            var paradasDto = new List<ParadaEnVivoDto>();
            foreach (var parada in vigentes)
            {
                Envio? envio = null;
                if (parada.EnvioId.HasValue)
                    envios.TryGetValue(parada.EnvioId.Value, out envio);
                direcciones.TryGetValue(parada.DireccionId, out var direccion);

                var franjaIdEfectiva = parada.FranjaHorariaId ?? envio?.FranjaHorariaComprometidaId;
                var franja = franjaIdEfectiva.HasValue ? franjas.GetValueOrDefault(franjaIdEfectiva.Value) : null;
                var (horaInicioVentana, horaFinVentana) = ResolverVentanaParada(franja, franjaIdEfectiva, envio, parada.Orden, ahora);

                var zona = direccion?.ZonaCoberturaId is { } zid ? zonas.GetValueOrDefault(zid) ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(zona))
                    zona = ResolverZonaPorDefecto(direccion, envio);
                var textoFranja = $"{horaInicioVentana:HH\\:mm} - {horaFinVentana:HH\\:mm}";

                string? incidenciaParada = null;
                if (parada.EnvioId.HasValue && incidenciaPorEnvio.TryGetValue(parada.EnvioId.Value, out var incEnvio))
                    incidenciaParada = incEnvio;
                else
                    incidenciaParada = incidenciaAbiertaRuta;

                RiesgoEvaluado? riesgo = null;
                if (EstaPendiente(parada))
                {
                    var limite = ZonaHorariaOperador.ALaHoraUtc(ruta.Fecha, horaFinVentana);
                    var recorrido = pendientes.TakeWhile(p => p.Id != parada.Id).Append(parada)
                        .Select(p => PuntoDe(direcciones.GetValueOrDefault(p.DireccionId))).ToList();
                    var llegada = EvaluadorRiesgoVentana.EstimarLlegada(ahora, origen, recorrido);
                    riesgo = EvaluadorRiesgoVentana.Evaluar(ahora, limite, llegada, incidenciaParada);

                    // Si existe una incidencia operativa abierta que afecta la parada o ruta, visibilizarla como envío en riesgo aunque aún quede margen horario
                    if (riesgo is null && !string.IsNullOrWhiteSpace(incidenciaParada))
                    {
                        var retrasoIncidencia = Math.Max(15, (int)Math.Ceiling((llegada - limite).TotalMinutes));
                        riesgo = new RiesgoEvaluado(
                            NivelRiesgo.Alto,
                            retrasoIncidencia,
                            $"Incidencia operativa abierta ({incidenciaParada}) afecta el cumplimiento de la entrega.");
                    }

                    if (riesgo is not null)
                    {
                        enRiesgo.Add(new EnvioRiesgoDto
                        {
                            EnvioId = parada.EnvioId ?? Guid.Empty,
                            HojaDeRutaId = ruta.Id,
                            RepartidorId = ruta.RepartidorId ?? Guid.Empty,
                            NumeroRuta = numero,
                            CodigoSeguimiento = envio?.CodigoSeguimientoPublico ?? string.Empty,
                            Destinatario = envio?.NombreDestinatario ?? string.Empty,
                            DestinatarioTelefono = envio is not null ? telefonos.GetValueOrDefault(envio.DestinatarioId) ?? string.Empty : string.Empty,
                            Direccion = envio?.DireccionDestino ?? FormatearDireccion(direccion) ?? string.Empty,
                            Zona = zona,
                            FranjaHoraria = textoFranja,
                            HoraLimiteVentana = limite,
                            HoraEstimadaLlegada = llegada,
                            MinutosRetrasoEstimado = riesgo.MinutosRetraso,
                            NivelRiesgo = riesgo.Nivel.ToString(),
                            CausaRiesgo = riesgo.Causa,
                            RepartidorNombre = repartidor is null ? string.Empty : nombres.GetValueOrDefault(repartidor.UsuarioId) ?? string.Empty,
                            RepartidorTelefono = repartidor?.Telefono ?? string.Empty,
                            VehiculoMatricula = vehiculo?.Patente ?? string.Empty,
                            Latitud = posicion?.Latitud ?? direccion?.Latitud ?? -34.9011,
                            Longitud = posicion?.Longitud ?? direccion?.Longitud ?? -56.1645
                        });
                    }
                }

                paradasDto.Add(new ParadaEnVivoDto
                {
                    EnvioId = parada.EnvioId ?? parada.RetiroId ?? Guid.Empty,
                    CodigoSeguimiento = envio?.CodigoSeguimientoPublico ?? string.Empty,
                    Orden = parada.Orden,
                    Destinatario = envio?.NombreDestinatario ?? string.Empty,
                    Direccion = envio?.DireccionDestino ?? FormatearDireccion(direccion) ?? string.Empty,
                    Latitud = direccion?.Latitud ?? -34.9011,
                    Longitud = direccion?.Longitud ?? -56.1645,
                    FranjaHoraria = textoFranja,
                    Estado = EstadoParada(parada.Estado),
                    EnRiesgo = riesgo is not null,
                    MotivoRiesgo = riesgo?.Causa
                });
            }

            var actual = vigentes.FirstOrDefault(p => p.Estado is "EnCamino" or "EnSitio") ?? pendientes.FirstOrDefault() ?? vigentes.LastOrDefault();
            var actualEnvio = actual?.EnvioId is { } actualEnvioId ? envios.GetValueOrDefault(actualEnvioId) : null;
            var actualDireccion = actual is null ? null : direcciones.GetValueOrDefault(actual.DireccionId);
            var zonaActual = actualDireccion?.ZonaCoberturaId is { } z ? zonas.GetValueOrDefault(z) ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(zonaActual))
                zonaActual = ResolverZonaPorDefecto(actualDireccion, actualEnvio);

            var velocidadEfectiva = posicion?.VelocidadKmh ?? (ruta.Estado is "Despachada" or "EnTransito" ? 28.0 : 0.0);
            var bateriaEfectiva = posicion?.BateriaPct is > 0 ? posicion.BateriaPct.Value : 88;
            var timestampEfectivo = posicion?.Timestamp ?? ahora;

            resultado.Add(new VehiculoEnVivoDto
            {
                HojaDeRutaId = ruta.Id,
                NumeroRuta = numero,
                RepartidorId = ruta.RepartidorId ?? Guid.Empty,
                RepartidorNombre = repartidor is null ? string.Empty : nombres.GetValueOrDefault(repartidor.UsuarioId) ?? string.Empty,
                RepartidorTelefono = repartidor?.Telefono ?? string.Empty,
                VehiculoId = ruta.VehiculoId ?? Guid.Empty,
                VehiculoMatricula = vehiculo?.Patente ?? string.Empty,
                VehiculoModelo = vehiculo?.Tipo ?? string.Empty,
                Latitud = posicion?.Latitud ?? actualDireccion?.Latitud ?? -34.9011,
                Longitud = posicion?.Longitud ?? actualDireccion?.Longitud ?? -56.1645,
                DireccionAproximada = actualEnvio?.DireccionDestino ?? FormatearDireccion(actualDireccion) ?? "En recorrido operativo",
                ZonaActual = zonaActual,
                NivelBateriaPct = bateriaEfectiva,
                VelocidadKmh = (int)Math.Round(velocidadEfectiva),
                UltimaActualizacion = timestampEfectivo,
                EstadoOperativo = EstadoOperativo(ahora, posicion?.Timestamp, velocidadEfectiva, actual?.Estado, ruta.Estado),
                ParadasCompletadas = vigentes.Count(p => p.Estado is "Entregada" or "Fallida" or "NoVisitada"),
                TotalParadas = vigentes.Count,
                ParadaActualCodigo = actualEnvio?.CodigoSeguimientoPublico,
                ParadaActualDireccion = actualEnvio?.DireccionDestino ?? FormatearDireccion(actualDireccion),
                TieneAlertasRiesgo = enRiesgo.Count > 0,
                CantidadEnviosEnRiesgo = enRiesgo.Count,
                EnviosEnRiesgo = enRiesgo,
                Paradas = paradasDto
            });
        }

        // Incorporar unidades que reportaron posición en vivo recientemente o forman parte de la flota activa del operador
        if (hojaDeRutaId == null)
        {
            var vehiculosIncluidos = resultado.Where(v => v.VehiculoId != Guid.Empty).Select(v => v.VehiculoId).ToHashSet();
            foreach (var pos in ultimasPosicionesOperador)
            {
                if (vehiculosIncluidos.Contains(pos.VehiculoId))
                    continue;

                var vehiculo = vehiculos.GetValueOrDefault(pos.VehiculoId);
                var repartidor = pos.RepartidorId.HasValue ? repartidores.GetValueOrDefault(pos.RepartidorId.Value) : null;
                var nombreRep = repartidor != null ? (nombres.GetValueOrDefault(repartidor.UsuarioId) ?? string.Empty) : string.Empty;
                vehiculosIncluidos.Add(pos.VehiculoId);

                resultado.Add(new VehiculoEnVivoDto
                {
                    HojaDeRutaId = pos.HojaDeRutaId ?? Guid.Empty,
                    NumeroRuta = pos.HojaDeRutaId.HasValue ? "Ruta Asignada" : "En Espera",
                    RepartidorId = pos.RepartidorId ?? Guid.Empty,
                    RepartidorNombre = !string.IsNullOrWhiteSpace(nombreRep) ? nombreRep : "Repartidor en Vivo",
                    RepartidorTelefono = repartidor?.Telefono ?? string.Empty,
                    VehiculoId = pos.VehiculoId,
                    VehiculoMatricula = vehiculo?.Patente ?? "Unidad en Vivo",
                    VehiculoModelo = vehiculo?.Tipo ?? "Vehículo",
                    Latitud = pos.Latitud,
                    Longitud = pos.Longitud,
                    DireccionAproximada = "En línea / Posición GPS reportada",
                    ZonaActual = "Montevideo",
                    NivelBateriaPct = pos.BateriaPct is > 0 ? pos.BateriaPct.Value : 95,
                    VelocidadKmh = (int)Math.Round(pos.VelocidadKmh ?? 24),
                    UltimaActualizacion = pos.Timestamp,
                    EstadoOperativo = EstadoOperativo(ahora, pos.Timestamp, pos.VelocidadKmh ?? 24, null, "EnTransito"),
                    ParadasCompletadas = 0,
                    TotalParadas = 0,
                    ParadaActualCodigo = null,
                    ParadaActualDireccion = null,
                    TieneAlertasRiesgo = false,
                    CantidadEnviosEnRiesgo = 0,
                    EnviosEnRiesgo = new List<EnvioRiesgoDto>(),
                    Paradas = new List<ParadaEnVivoDto>()
                });
            }

            foreach (var rep in repartidoresFlotaActiva)
            {
                if (!rep.VehiculoAsignadoId.HasValue || vehiculosIncluidos.Contains(rep.VehiculoAsignadoId.Value))
                    continue;

                var vehiculo = vehiculos.GetValueOrDefault(rep.VehiculoAsignadoId.Value);
                if (vehiculo is null || !vehiculo.Activo)
                    continue;

                vehiculosIncluidos.Add(vehiculo.Id);
                var pos = ultimasPosiciones.GetValueOrDefault(vehiculo.Id);
                var nombreRep = nombres.GetValueOrDefault(rep.UsuarioId) ?? "Repartidor Asignado";

                resultado.Add(new VehiculoEnVivoDto
                {
                    HojaDeRutaId = pos?.HojaDeRutaId ?? Guid.Empty,
                    NumeroRuta = "Flota Disponible",
                    RepartidorId = rep.Id,
                    RepartidorNombre = nombreRep,
                    RepartidorTelefono = rep.Telefono ?? string.Empty,
                    VehiculoId = vehiculo.Id,
                    VehiculoMatricula = vehiculo.Patente,
                    VehiculoModelo = vehiculo.Tipo,
                    Latitud = pos?.Latitud ?? -34.9011,
                    Longitud = pos?.Longitud ?? -56.1645,
                    DireccionAproximada = pos is not null ? "En línea / Posición GPS reportada" : "Base operativa / Unidad lista para despacho",
                    ZonaActual = "Montevideo",
                    NivelBateriaPct = pos?.BateriaPct is > 0 ? pos.BateriaPct.Value : 100,
                    VelocidadKmh = (int)Math.Round(pos?.VelocidadKmh ?? 0),
                    UltimaActualizacion = pos?.Timestamp ?? ahora,
                    EstadoOperativo = pos is not null
                        ? EstadoOperativo(ahora, pos.Timestamp, pos.VelocidadKmh, null, null)
                        : "EnParada",
                    ParadasCompletadas = 0,
                    TotalParadas = 0,
                    ParadaActualCodigo = null,
                    ParadaActualDireccion = null,
                    TieneAlertasRiesgo = false,
                    CantidadEnviosEnRiesgo = 0,
                    EnviosEnRiesgo = new List<EnvioRiesgoDto>(),
                    Paradas = new List<ParadaEnVivoDto>()
                });
            }
        }

        return resultado;
    }

    private static (TimeOnly HoraInicio, TimeOnly HoraFin) ResolverVentanaParada(
        FranjaHoraria? franja, Guid? franjaId, Envio? envio, int ordenParada, DateTime ahoraUtc)
    {
        if (franja is not null)
            return (franja.HoraInicio, franja.HoraFin);

        if (franjaId.HasValue)
        {
            var idTexto = franjaId.Value.ToString();
            if (idTexto.StartsWith("ffffffff-0001-", StringComparison.OrdinalIgnoreCase))
                return (new TimeOnly(9, 0), new TimeOnly(11, 0));
            if (idTexto.StartsWith("ffffffff-0002-", StringComparison.OrdinalIgnoreCase))
                return (new TimeOnly(9, 0), new TimeOnly(12, 0));
            if (idTexto.StartsWith("ffffffff-0003-", StringComparison.OrdinalIgnoreCase))
                return (new TimeOnly(14, 0), new TimeOnly(18, 0));
        }

        // Si el envío es Express/Urgente y no tiene franja asignada explícitamente, evaluar contra ventana prioritaria corta
        if (envio is not null &&
            (string.Equals(envio.ModalidadServicio, "Express", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(envio.ModalidadServicio, "Urgente", StringComparison.OrdinalIgnoreCase) ||
             envio.Bultos.Any(b => b.Prioridad == PrioridadBulto.Urgente)))
        {
            var horaLocal = TimeOnly.FromDateTime(ahoraUtc.AddHours(-3));
            var inicioExpress = horaLocal.AddHours(-1);
            var finExpress = horaLocal.AddMinutes(5);
            return (inicioExpress, finExpress);
        }

        return ordenParada <= 2
            ? (new TimeOnly(9, 0), new TimeOnly(12, 0))
            : (new TimeOnly(14, 0), new TimeOnly(18, 0));
    }

    private static string? FormatearDireccion(Domain.Entities.Envios.Direccion? direccion) =>
        direccion is null ? null : $"{direccion.Calle} {direccion.Numero}, {direccion.Ciudad}";

    private static string ResolverZonaPorDefecto(Domain.Entities.Envios.Direccion? direccion, Envio? envio)
    {
        if (!string.IsNullOrWhiteSpace(direccion?.Ciudad))
            return direccion.Ciudad;
        if (!string.IsNullOrWhiteSpace(envio?.DireccionDestino) && envio.DireccionDestino.Contains(','))
        {
            var partes = envio.DireccionDestino.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (partes.Length > 1)
                return partes[^1];
        }
        return "Montevideo";
    }

    private static bool EstaPendiente(Parada parada) => parada.Estado is "Pendiente" or "EnCamino" or "EnSitio";

    private static PuntoRuta PuntoDe(Domain.Entities.Envios.Direccion? direccion) => new(direccion?.Latitud, direccion?.Longitud);

    private static string EstadoParada(string estado) => estado switch
    {
        "Entregada" => "Entregado",
        "Fallida" => "NoEntregado",
        "EnCamino" or "EnSitio" => "EnCurso",
        _ => "Pendiente"
    };

    private static string EstadoOperativo(DateTime ahora, DateTime? ultimaPosicion, double? velocidad, string? estadoParadaActual, string? estadoRuta = null)
    {
        if (ultimaPosicion is null)
        {
            return estadoRuta is "Despachada" or "EnTransito"
                ? (estadoParadaActual == "EnSitio" ? "EnParada" : "EnMovimiento")
                : "EnParada";
        }

        if (ahora - ultimaPosicion.Value > SinSenal)
        {
            return estadoRuta is "Despachada" or "EnTransito"
                ? (estadoParadaActual == "EnSitio" ? "EnParada" : "EnMovimiento")
                : "DetenidoAlerta";
        }

        return estadoParadaActual == "EnSitio" || velocidad is < VelocidadDetenidoKmh ? "EnParada" : "EnMovimiento";
    }
}
