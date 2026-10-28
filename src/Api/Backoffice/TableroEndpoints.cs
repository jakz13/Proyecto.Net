using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Tablero de operación en vivo: flota en calle y envíos en riesgo de incumplir su ventana (UC-12, RF-29).</summary>
public static class TableroEndpoints
{
    private const int MaximoCaracteresInstruccion = 500;

    public static IEndpointRouteBuilder MapTableroEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/tablero")
            .RequireAuthorization()
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

        grupo.MapPost("/repartidores/{repartidorId:guid}/instrucciones", (
            Guid repartidorId,
            InstruccionRepartidorRequest peticion,
            AppDbContext db,
            ICurrentTenant tenant,
            IOutboxStorage outbox,
            IUnitOfWork unidadDeTrabajo,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
            {
                var mensaje = peticion.Mensaje?.Trim();
                if (string.IsNullOrEmpty(mensaje))
                    return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Escriba el mensaje para el repartidor.");
                if (mensaje.Length > MaximoCaracteresInstruccion)
                    return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"El mensaje no puede superar los {MaximoCaracteresInstruccion} caracteres.");

                var existe = await db.Repartidores.AsNoTracking()
                    .AnyAsync(r => r.Id == repartidorId && r.OperadorId == tenant.OperadorId, ct);
                if (!existe)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el repartidor.");

                // Se publica por la outbox: la entrega al dispositivo (notificación push) la resuelve el worker
                await outbox.SaveAsync(new OutboxMessage(Guid.NewGuid(), "Repartidor", repartidorId, "InstruccionRepartidorIntegrationEvent",
                    JsonSerializer.Serialize(new
                    {
                        Version = 1,
                        tenant.OperadorId,
                        RepartidorId = repartidorId,
                        Mensaje = mensaje,
                        EnviadaPorUsuarioId = tenant.UsuarioId,
                        OcurridoEn = DateTime.UtcNow
                    })), ct);
                await unidadDeTrabajo.SaveChangesAsync(ct);

                return Results.Accepted();
            }))
            .WithName("EnviarInstruccionRepartidor")
            .WithSummary("Envía un mensaje prioritario al dispositivo del repartidor");

        return app;
    }

    public record InstruccionRepartidorRequest(string? Mensaje);
}

/// <summary>Arma los datos del tablero a partir de las rutas despachadas, sus paradas y la última posición de cada vehículo.</summary>
internal static class ConstructorTablero
{
    private static readonly string[] EstadosRutaEnEjecucion = { "Despachada", "EnTransito" };
    private static readonly TimeSpan SinSenal = TimeSpan.FromMinutes(10);
    private const double VelocidadDetenidoKmh = 3;

    private static readonly TimeZoneInfo ZonaHoraria = ObtenerZonaHoraria();

    public static DateOnly FechaLocal(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, ZonaHoraria));

    public static int PesoNivel(string nivel) => nivel switch { "Critico" => 3, "Alto" => 2, _ => 1 };

    public static async Task<List<VehiculoEnVivoDto>> ConstruirAsync(
        AppDbContext db, Guid operadorId, DateTime ahora, Guid? hojaDeRutaId, CancellationToken ct)
    {
        var estados = EstadosRutaEnEjecucion.ToList();
        var rutas = await db.HojasDeRuta.AsNoTracking()
            .Where(h => h.OperadorId == operadorId && estados.Contains(h.Estado) && (hojaDeRutaId == null || h.Id == hojaDeRutaId))
            .ToListAsync(ct);
        if (rutas.Count == 0)
            return new List<VehiculoEnVivoDto>();

        var rutaIds = rutas.Select(r => r.Id).ToList();
        var repartidorIds = rutas.Where(r => r.RepartidorId.HasValue).Select(r => r.RepartidorId!.Value).Distinct().ToList();
        var vehiculoIds = rutas.Where(r => r.VehiculoId.HasValue).Select(r => r.VehiculoId!.Value).Distinct().ToList();
        var paradas = rutas.SelectMany(r => r.Paradas).Where(p => p.Estado != "Cancelada").ToList();
        var envioIds = paradas.Select(p => p.EnvioId).Distinct().ToList();
        var direccionIds = paradas.Select(p => p.DireccionId).Distinct().ToList();
        var franjaIds = paradas.Where(p => p.FranjaHorariaId.HasValue).Select(p => p.FranjaHorariaId!.Value).Distinct().ToList();

        var repartidores = await db.Repartidores.AsNoTracking().Where(r => repartidorIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var usuarioIds = repartidores.Values.Select(r => r.UsuarioId).ToList();
        var nombres = await db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        var vehiculos = await db.Vehiculos.AsNoTracking().Where(v => vehiculoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);
        var envios = await db.Envios.AsNoTracking().Where(e => envioIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, ct);
        var destinatarioIds = envios.Values.Select(e => e.DestinatarioId).Distinct().ToList();
        var telefonos = await db.Destinatarios.AsNoTracking().Where(d => destinatarioIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Telefono ?? string.Empty, ct);
        var direcciones = await db.Direcciones.AsNoTracking().Where(d => direccionIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var zonaIds = direcciones.Values.Where(d => d.ZonaCoberturaId.HasValue).Select(d => d.ZonaCoberturaId!.Value).Distinct().ToList();
        var zonas = await db.ZonasCobertura.AsNoTracking().Where(z => zonaIds.Contains(z.Id)).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);
        var franjas = await db.FranjasHorarias.AsNoTracking().Where(f => franjaIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);

        var desde = ahora.AddHours(-24);
        var ultimasPosiciones = await db.PosicionesVehiculo.AsNoTracking()
            .Where(p => p.OperadorId == operadorId && vehiculoIds.Contains(p.VehiculoId) && p.Timestamp > desde)
            .GroupBy(p => p.VehiculoId)
            .Select(g => g.OrderByDescending(p => p.Timestamp).First())
            .ToDictionaryAsync(p => p.VehiculoId, ct);

        var cerrada = Domain.Entities.Incidencias.Incidencia.EstadoCerrada;
        var incidencias = await db.Incidencias.AsNoTracking()
            .Where(i => i.OperadorId == operadorId && i.HojaDeRutaId != null && rutaIds.Contains(i.HojaDeRutaId.Value) && i.Estado != cerrada)
            .Select(i => new { HojaDeRutaId = i.HojaDeRutaId!.Value, i.Tipo })
            .ToListAsync(ct);
        var incidenciaPorRuta = incidencias.GroupBy(i => i.HojaDeRutaId).ToDictionary(g => g.Key, g => g.First().Tipo);

        var resultado = new List<VehiculoEnVivoDto>();
        foreach (var ruta in rutas.OrderBy(r => r.Fecha).ThenBy(r => r.Id))
        {
            var repartidor = ruta.RepartidorId.HasValue ? repartidores.GetValueOrDefault(ruta.RepartidorId.Value) : null;
            var vehiculo = ruta.VehiculoId.HasValue ? vehiculos.GetValueOrDefault(ruta.VehiculoId.Value) : null;
            var posicion = ruta.VehiculoId.HasValue ? ultimasPosiciones.GetValueOrDefault(ruta.VehiculoId.Value) : null;
            var numero = HojaDeRutaMapper.NumeroRuta(ruta);
            incidenciaPorRuta.TryGetValue(ruta.Id, out var incidenciaAbierta);

            var vigentes = ruta.Paradas.Where(p => p.Estado != "Cancelada").OrderBy(p => p.Orden).ToList();
            var pendientes = vigentes.Where(EstaPendiente).ToList();
            PuntoRuta? origen = posicion is null ? null : new PuntoRuta(posicion.Latitud, posicion.Longitud);

            var enRiesgo = new List<EnvioRiesgoDto>();
            var paradasDto = new List<ParadaEnVivoDto>();
            foreach (var parada in vigentes)
            {
                envios.TryGetValue(parada.EnvioId, out var envio);
                direcciones.TryGetValue(parada.DireccionId, out var direccion);
                var franja = parada.FranjaHorariaId.HasValue ? franjas.GetValueOrDefault(parada.FranjaHorariaId.Value) : null;
                var zona = direccion?.ZonaCoberturaId is { } zid ? zonas.GetValueOrDefault(zid) ?? string.Empty : string.Empty;
                var textoFranja = franja is null ? string.Empty : $"{franja.HoraInicio:HH\\:mm} - {franja.HoraFin:HH\\:mm}";

                RiesgoEvaluado? riesgo = null;
                if (EstaPendiente(parada) && franja is not null)
                {
                    var limite = ALaHoraUtc(ruta.Fecha, franja.HoraFin);
                    var recorrido = pendientes.TakeWhile(p => p.Id != parada.Id).Append(parada)
                        .Select(p => PuntoDe(direcciones.GetValueOrDefault(p.DireccionId))).ToList();
                    var llegada = EvaluadorRiesgoVentana.EstimarLlegada(ahora, origen, recorrido);
                    riesgo = EvaluadorRiesgoVentana.Evaluar(ahora, limite, llegada, incidenciaAbierta);

                    if (riesgo is not null)
                    {
                        enRiesgo.Add(new EnvioRiesgoDto
                        {
                            EnvioId = parada.EnvioId,
                            HojaDeRutaId = ruta.Id,
                            NumeroRuta = numero,
                            CodigoSeguimiento = envio?.CodigoSeguimientoPublico ?? string.Empty,
                            Destinatario = envio?.NombreDestinatario ?? string.Empty,
                            DestinatarioTelefono = envio is not null ? telefonos.GetValueOrDefault(envio.DestinatarioId) ?? string.Empty : string.Empty,
                            Direccion = envio?.DireccionDestino ?? string.Empty,
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
                            Latitud = posicion?.Latitud ?? direccion?.Latitud ?? 0,
                            Longitud = posicion?.Longitud ?? direccion?.Longitud ?? 0
                        });
                    }
                }

                paradasDto.Add(new ParadaEnVivoDto
                {
                    EnvioId = parada.EnvioId,
                    CodigoSeguimiento = envio?.CodigoSeguimientoPublico ?? string.Empty,
                    Orden = parada.Orden,
                    Destinatario = envio?.NombreDestinatario ?? string.Empty,
                    Direccion = envio?.DireccionDestino ?? string.Empty,
                    Latitud = direccion?.Latitud ?? 0,
                    Longitud = direccion?.Longitud ?? 0,
                    FranjaHoraria = textoFranja,
                    Estado = EstadoParada(parada.Estado),
                    EnRiesgo = riesgo is not null,
                    MotivoRiesgo = riesgo?.Causa
                });
            }

            var actual = vigentes.FirstOrDefault(p => p.Estado is "EnCamino" or "EnSitio") ?? pendientes.FirstOrDefault();
            var actualEnvio = actual is null ? null : envios.GetValueOrDefault(actual.EnvioId);
            var actualDireccion = actual is null ? null : direcciones.GetValueOrDefault(actual.DireccionId);

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
                Latitud = posicion?.Latitud ?? actualDireccion?.Latitud ?? 0,
                Longitud = posicion?.Longitud ?? actualDireccion?.Longitud ?? 0,
                DireccionAproximada = actualEnvio?.DireccionDestino ?? string.Empty,
                ZonaActual = actualDireccion?.ZonaCoberturaId is { } z ? zonas.GetValueOrDefault(z) ?? string.Empty : string.Empty,
                NivelBateriaPct = posicion?.BateriaPct ?? 0,
                VelocidadKmh = (int)Math.Round(posicion?.VelocidadKmh ?? 0),
                UltimaActualizacion = posicion?.Timestamp ?? ahora,
                EstadoOperativo = EstadoOperativo(ahora, posicion?.Timestamp, posicion?.VelocidadKmh, actual?.Estado),
                ParadasCompletadas = vigentes.Count(p => p.Estado is "Entregada" or "Fallida" or "NoVisitada"),
                TotalParadas = vigentes.Count,
                ParadaActualCodigo = actualEnvio?.CodigoSeguimientoPublico,
                ParadaActualDireccion = actualEnvio?.DireccionDestino,
                TieneAlertasRiesgo = enRiesgo.Count > 0,
                CantidadEnviosEnRiesgo = enRiesgo.Count,
                EnviosEnRiesgo = enRiesgo,
                Paradas = paradasDto
            });
        }

        return resultado;
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

    private static string EstadoOperativo(DateTime ahora, DateTime? ultimaPosicion, double? velocidad, string? estadoParadaActual)
    {
        if (ultimaPosicion is null || ahora - ultimaPosicion.Value > SinSenal)
            return "DetenidoAlerta";

        return estadoParadaActual == "EnSitio" || velocidad is < VelocidadDetenidoKmh ? "EnParada" : "EnMovimiento";
    }

    /// <summary>Las franjas se configuran en hora local del operador; los instantes se manejan en UTC.</summary>
    private static DateTime ALaHoraUtc(DateOnly fecha, TimeOnly hora) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fecha.ToDateTime(hora), DateTimeKind.Unspecified), ZonaHoraria);

    private static TimeZoneInfo ObtenerZonaHoraria()
    {
        foreach (var id in new[] { "America/Montevideo", "Montevideo Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC-03", TimeSpan.FromHours(-3), "UTC-03", "UTC-03");
    }
}
