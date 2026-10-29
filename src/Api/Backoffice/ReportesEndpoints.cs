using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Reportes de gestión: cumplimiento, motivos de no entrega, volumen y liquidación por comercio (UC-13, RF-30).</summary>
public static class ReportesEndpoints
{
    public static readonly string[] TiposValidos =
    {
        "CumplimientoZona", "CumplimientoRepartidor", "CumplimientoComercio", "MotivosNoEntrega", "VolumenPeriodo", "LiquidacionComercio"
    };

    private const int MaximoDiasDelPeriodo = 366;

    public static IEndpointRouteBuilder MapReportesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reportes", (
            string? tipo,
            DateOnly? desde,
            DateOnly? hasta,
            Guid? comercioId,
            Guid? repartidorId,
            string? zona,
            AppDbContext db,
            ICurrentTenant tenant,
            IClock reloj,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var tipoReporte = string.IsNullOrWhiteSpace(tipo) ? TiposValidos[0] : tipo.Trim();
            if (!TiposValidos.Contains(tipoReporte))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"El tipo de reporte '{tipoReporte}' no existe.");

            var fin = hasta ?? ZonaHorariaOperador.FechaLocal(reloj.UtcNow);
            var inicio = desde ?? fin.AddDays(-30);
            if (inicio > fin)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "La fecha inicial no puede ser posterior a la final.");
            if (fin.DayNumber - inicio.DayNumber >= MaximoDiasDelPeriodo)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"El período no puede superar los {MaximoDiasDelPeriodo} días.");

            var filtro = new FiltroReporteDto
            {
                TipoReporte = tipoReporte,
                FechaDesde = inicio,
                FechaHasta = fin,
                Preajuste = "Personalizado",
                ComercioId = comercioId,
                RepartidorId = repartidorId,
                Zona = string.IsNullOrWhiteSpace(zona) ? null : zona.Trim()
            };

            var resultado = await new ConstructorReportes(db, tenant.OperadorId, filtro, reloj.UtcNow).GenerarAsync(ct);
            return Results.Ok(resultado);
        }))
        .RequireAuthorization()
        .WithTags("Reportes de gestión (UC-13, RF-30)")
        .WithName("GenerarReporteDeGestion")
        .WithSummary("Genera el reporte indicado para el período y los filtros recibidos");

        return app;
    }
}

/// <summary>
/// Criterios de cálculo. "En plazo": la entrega se hizo antes del cierre de la franja comprometida de la parada
/// (o, sin franja, el mismo día de la hoja de ruta); si no, "con demora". "No entregado": el envío tuvo un intento fallido y no
/// llegó a entregarse en el período. Los envíos aún pendientes cuentan como asignados pero en ninguna de las otras columnas.
/// La liquidación toma los envíos entregados en el período y descompone la tarifa de la regla con la que se calcularon.
/// </summary>
internal sealed class ConstructorReportes
{
    private const decimal TasaIva = 0.22m;

    private readonly AppDbContext _db;
    private readonly Guid _operadorId;
    private readonly FiltroReporteDto _filtro;
    private readonly DateTime _ahora;
    private readonly DateTime _desdeUtc;
    private readonly DateTime _hastaUtc;

    public ConstructorReportes(AppDbContext db, Guid operadorId, FiltroReporteDto filtro, DateTime ahora)
    {
        _db = db;
        _operadorId = operadorId;
        _filtro = filtro;
        _ahora = ahora;
        _desdeUtc = ZonaHorariaOperador.InicioDelDiaUtc(filtro.FechaDesde);
        _hastaUtc = ZonaHorariaOperador.InicioDelDiaUtc(filtro.FechaHasta.AddDays(1));
    }

    public async Task<ResultadoReporteDto> GenerarAsync(CancellationToken ct)
    {
        var cumplimiento = new List<ItemReporteCumplimientoDto>();
        var motivos = new List<ItemReporteMotivosDto>();
        var volumen = new List<ItemReporteVolumenDto>();
        var liquidacion = new List<ItemReporteLiquidacionDto>();

        switch (_filtro.TipoReporte)
        {
            case "CumplimientoZona":
            case "CumplimientoRepartidor":
            case "CumplimientoComercio":
                cumplimiento = await CumplimientoAsync(ct);
                break;
            case "MotivosNoEntrega":
                motivos = await MotivosAsync(ct);
                break;
            case "VolumenPeriodo":
                volumen = await VolumenAsync(ct);
                break;
            case "LiquidacionComercio":
                liquidacion = await LiquidacionAsync(ct);
                break;
        }

        var asignados = cumplimiento.Sum(i => i.TotalAsignados);
        var entregados = cumplimiento.Sum(i => i.EntregadosEnPlazo + i.EntregadosConDemora);
        var noEntregados = cumplimiento.Sum(i => i.NoEntregados);
        var totalNoEntregadosMotivos = motivos.Sum(m => m.CantidadOcurrencias);

        return new ResultadoReporteDto
        {
            Filtro = _filtro,
            FechaGeneracion = _ahora,
            TotalEnviosProcesados = asignados + totalNoEntregadosMotivos + volumen.Sum(v => v.CantidadGuias) + liquidacion.Sum(l => l.CantidadEnviosLiquidados),
            TasaEfectividadGlobal = asignados > 0 ? Math.Round((decimal)cumplimiento.Sum(i => i.EntregadosEnPlazo) / asignados * 100, 1) : 0,
            TotalEntregados = entregados,
            TotalNoEntregados = noEntregados + totalNoEntregadosMotivos,
            TotalFacturadoLiquidacion = liquidacion.Sum(l => l.TotalFacturado),
            ItemsCumplimiento = cumplimiento,
            ItemsMotivos = motivos,
            ItemsVolumen = volumen,
            ItemsLiquidacion = liquidacion
        };
    }

    // ---------------------------------------------------------------- Cumplimiento

    private async Task<List<ItemReporteCumplimientoDto>> CumplimientoAsync(CancellationToken ct)
    {
        var rutasQuery = _db.HojasDeRuta.AsNoTracking()
            .Where(h => h.OperadorId == _operadorId && h.Fecha >= _filtro.FechaDesde && h.Fecha <= _filtro.FechaHasta
                        && (_filtro.RepartidorId == null || h.RepartidorId == _filtro.RepartidorId));
        var rutas = await rutasQuery.Select(h => new { h.Id, h.Fecha, h.RepartidorId, h.VehiculoId }).ToListAsync(ct);
        if (rutas.Count == 0)
            return new List<ItemReporteCumplimientoDto>();

        var rutaPorId = rutas.ToDictionary(r => r.Id);
        var rutaIds = rutas.Select(r => r.Id).ToList();
        var paradas = await _db.Paradas.AsNoTracking()
            .Where(p => rutaIds.Contains(p.HojaDeRutaId) && p.Estado != "Cancelada")
            .Select(p => new { p.EnvioId, p.HojaDeRutaId, p.Estado, p.DireccionId, p.FranjaHorariaId })
            .ToListAsync(ct);

        // Un envío reprogramado tiene paradas en varias rutas: cuenta la más reciente del período
        var ultimaParada = paradas
            .GroupBy(p => p.EnvioId)
            .Select(g => g.OrderByDescending(p => rutaPorId[p.HojaDeRutaId].Fecha).First())
            .ToList();
        var envioIds = ultimaParada.Select(p => p.EnvioId).ToList();

        var envios = await _db.Envios.AsNoTracking()
            .Where(e => e.OperadorId == _operadorId && envioIds.Contains(e.Id)
                        && (_filtro.ComercioId == null || e.ComercioOperadorId == _filtro.ComercioId))
            .Select(e => new { e.Id, e.ComercioOperadorId })
            .ToDictionaryAsync(e => e.Id, ct);

        var intentos = (await _db.IntentosEntrega.AsNoTracking()
                .Where(i => envioIds.Contains(i.EnvioId))
                .Select(i => new { i.EnvioId, i.Resultado, i.Timestamp })
                .ToListAsync(ct))
            .ToLookup(i => i.EnvioId);

        var direccionIds = ultimaParada.Select(p => p.DireccionId).Distinct().ToList();
        var direcciones = await _db.Direcciones.AsNoTracking()
            .Where(d => direccionIds.Contains(d.Id))
            .Select(d => new { d.Id, d.ZonaCoberturaId })
            .ToDictionaryAsync(d => d.Id, d => d.ZonaCoberturaId, ct);
        var zonas = await _db.ZonasCobertura.AsNoTracking().Where(z => z.OperadorId == _operadorId).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);

        var franjaIds = ultimaParada.Where(p => p.FranjaHorariaId.HasValue).Select(p => p.FranjaHorariaId!.Value).Distinct().ToList();
        var franjas = await _db.FranjasHorarias.AsNoTracking().Where(f => franjaIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.HoraFin, ct);

        var nombres = await NombresAsync(rutas.Select(r => r.RepartidorId), rutas.Select(r => r.VehiculoId), envios.Values.Select(e => e.ComercioOperadorId), ct);

        var filas = new Dictionary<string, (string? Detalle, int Asignados, int EnPlazo, int Demora, int NoEntregados)>();
        foreach (var parada in ultimaParada)
        {
            if (!envios.TryGetValue(parada.EnvioId, out var envio))
                continue;

            var zona = direcciones.GetValueOrDefault(parada.DireccionId) is { } zid ? zonas.GetValueOrDefault(zid) ?? "Sin zona" : "Sin zona";
            if (_filtro.Zona is not null && !string.Equals(zona, _filtro.Zona, StringComparison.OrdinalIgnoreCase))
                continue;

            var ruta = rutaPorId[parada.HojaDeRutaId];
            var delEnvio = intentos[parada.EnvioId].ToList();
            var exitoso = delEnvio.Where(i => i.Resultado == "Exitoso").OrderByDescending(i => i.Timestamp).FirstOrDefault();

            var enPlazo = false;
            var conDemora = false;
            var noEntregado = false;
            if (exitoso is not null)
            {
                var limite = parada.FranjaHorariaId.HasValue && franjas.TryGetValue(parada.FranjaHorariaId.Value, out var fin)
                    ? ZonaHorariaOperador.ALaHoraUtc(ruta.Fecha, fin)
                    : ZonaHorariaOperador.InicioDelDiaUtc(ruta.Fecha.AddDays(1));
                if (exitoso.Timestamp <= limite) enPlazo = true; else conDemora = true;
            }
            else if (parada.Estado == "Fallida" || delEnvio.Any(i => i.Resultado == "Fallido"))
            {
                noEntregado = true;
            }

            var (clave, detalle) = _filtro.TipoReporte switch
            {
                "CumplimientoZona" => (zona, (string?)null),
                "CumplimientoRepartidor" => (
                    nombres.Repartidor(ruta.RepartidorId) ?? "Sin repartidor",
                    nombres.Vehiculo(ruta.VehiculoId) is { } patente ? $"Vehículo {patente}" : null),
                _ => (nombres.Comercio(envio.ComercioOperadorId).Nombre, nombres.Comercio(envio.ComercioOperadorId).Detalle)
            };

            filas.TryGetValue(clave, out var actual);
            filas[clave] = (detalle ?? actual.Detalle, actual.Asignados + 1, actual.EnPlazo + (enPlazo ? 1 : 0),
                actual.Demora + (conDemora ? 1 : 0), actual.NoEntregados + (noEntregado ? 1 : 0));
        }

        return filas
            .Select(f => new ItemReporteCumplimientoDto
            {
                Agrupador = f.Key,
                DetalleAdicional = f.Value.Detalle,
                TotalAsignados = f.Value.Asignados,
                EntregadosEnPlazo = f.Value.EnPlazo,
                EntregadosConDemora = f.Value.Demora,
                NoEntregados = f.Value.NoEntregados
            })
            .OrderByDescending(i => i.TotalAsignados).ThenBy(i => i.Agrupador)
            .ToList();
    }

    // ---------------------------------------------------------------- Motivos de no entrega

    private async Task<List<ItemReporteMotivosDto>> MotivosAsync(CancellationToken ct)
    {
        var fallidos = await _db.IntentosEntrega.AsNoTracking()
            .Where(i => i.Resultado == "Fallido" && i.MotivoNoEntregaId != null && i.Timestamp >= _desdeUtc && i.Timestamp < _hastaUtc)
            .Join(_db.Envios.AsNoTracking().Where(e => e.OperadorId == _operadorId
                        && (_filtro.ComercioId == null || e.ComercioOperadorId == _filtro.ComercioId)),
                i => i.EnvioId, e => e.Id, (i, e) => i.MotivoNoEntregaId!.Value)
            .ToListAsync(ct);
        if (fallidos.Count == 0)
            return new List<ItemReporteMotivosDto>();

        var ids = fallidos.Distinct().ToList();
        var catalogo = await _db.MotivosNoEntrega.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);

        return fallidos
            .GroupBy(id => id)
            .Select(g => new { Motivo = catalogo.GetValueOrDefault(g.Key), Cantidad = g.Count() })
            .Where(x => x.Motivo is not null)
            .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Motivo!.Codigo)
            .Select(x => new ItemReporteMotivosDto
            {
                CodigoMotivo = x.Motivo!.Codigo,
                DescripcionMotivo = x.Motivo.Descripcion,
                Categoria = x.Motivo.EsInterno ? "Interno" : "ExternoDestinatario",
                CantidadOcurrencias = x.Cantidad,
                PorcentajeDelTotal = Math.Round((decimal)x.Cantidad / fallidos.Count * 100, 1),
                ImpactoComun = "Según la política de reintento y devolución del operador"
            })
            .ToList();
    }

    // ---------------------------------------------------------------- Volumen

    private async Task<List<ItemReporteVolumenDto>> VolumenAsync(CancellationToken ct)
    {
        var envios = await _db.Envios.AsNoTracking()
            .Where(e => e.OperadorId == _operadorId && e.FechaAlta >= _desdeUtc && e.FechaAlta < _hastaUtc
                        && (_filtro.ComercioId == null || e.ComercioOperadorId == _filtro.ComercioId))
            .Select(e => new { e.Id, e.FechaAlta })
            .ToListAsync(ct);
        var envioIds = envios.Select(e => e.Id).ToList();
        var bultos = (await _db.Bultos.AsNoTracking()
                .Where(b => envioIds.Contains(b.EnvioId) && b.EstadoBulto != "FaltanteEnRecepcion")
                .Select(b => new { b.EnvioId, b.Peso, b.Alto, b.Ancho, b.Profundo })
                .ToListAsync(ct))
            .ToLookup(b => b.EnvioId);

        var despachos = (await _db.HojasDeRuta.AsNoTracking()
                .Where(h => h.OperadorId == _operadorId && h.FechaDespacho != null && h.FechaDespacho >= _desdeUtc && h.FechaDespacho < _hastaUtc)
                .Select(h => h.FechaDespacho!.Value)
                .ToListAsync(ct))
            .GroupBy(ZonaHorariaOperador.FechaLocal)
            .ToDictionary(g => g.Key, g => g.Count());

        var porDia = envios.GroupBy(e => ZonaHorariaOperador.FechaLocal(e.FechaAlta)).ToDictionary(g => g.Key, g => g.ToList());

        return porDia.Keys.Union(despachos.Keys).OrderBy(f => f)
            .Select(fecha =>
            {
                var delDia = porDia.GetValueOrDefault(fecha) ?? new();
                var bultosDelDia = delDia.SelectMany(e => bultos[e.Id]).ToList();
                return new ItemReporteVolumenDto
                {
                    Fecha = fecha,
                    CantidadGuias = delDia.Count,
                    CantidadBultos = bultosDelDia.Count,
                    PesoTotalKg = Math.Round(bultosDelDia.Sum(b => b.Peso), 2),
                    VolumenTotalM3 = Math.Round(bultosDelDia.Sum(b => b.Alto * b.Ancho * b.Profundo / 1_000_000m), 3),
                    RutasDespachadas = despachos.GetValueOrDefault(fecha)
                };
            })
            .ToList();
    }

    // ---------------------------------------------------------------- Liquidación

    private async Task<List<ItemReporteLiquidacionDto>> LiquidacionAsync(CancellationToken ct)
    {
        var entregados = await _db.IntentosEntrega.AsNoTracking()
            .Where(i => i.Resultado == "Exitoso" && i.Timestamp >= _desdeUtc && i.Timestamp < _hastaUtc)
            .Join(_db.Envios.AsNoTracking().Where(e => e.OperadorId == _operadorId
                        && (_filtro.ComercioId == null || e.ComercioOperadorId == _filtro.ComercioId)),
                i => i.EnvioId, e => e.Id, (i, e) => new { e.Id, e.ComercioOperadorId, e.ReglaTarifaAplicadaId, e.TarifaCalculada })
            .Distinct()
            .ToListAsync(ct);
        if (entregados.Count == 0)
            return new List<ItemReporteLiquidacionDto>();

        var reglaIds = entregados.Where(e => e.ReglaTarifaAplicadaId.HasValue).Select(e => e.ReglaTarifaAplicadaId!.Value).Distinct().ToList();
        var reglas = await _db.ReglasTarifa.AsNoTracking().Where(r => reglaIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);
        var nombres = await NombresAsync(Array.Empty<Guid?>(), Array.Empty<Guid?>(), entregados.Select(e => e.ComercioOperadorId), ct);

        return entregados
            .GroupBy(e => e.ComercioOperadorId)
            .Select(g =>
            {
                var comercio = nombres.Comercio(g.Key);
                decimal baseTotal = 0, recargo = 0, descuento = 0;
                foreach (var envio in g)
                {
                    if (envio.ReglaTarifaAplicadaId is { } rid && reglas.TryGetValue(rid, out var regla))
                    {
                        baseTotal += regla.PrecioBase;
                        recargo += regla.Recargo;
                        descuento += regla.Bonificacion;
                    }
                    else
                    {
                        baseTotal += envio.TarifaCalculada;
                    }
                }

                return new ItemReporteLiquidacionDto
                {
                    ComercioId = g.Key,
                    RazonSocial = comercio.Nombre,
                    Rut = comercio.Documento,
                    CantidadEnviosLiquidados = g.Count(),
                    TarifaBaseTotal = baseTotal,
                    RecargoPesoTotal = recargo,
                    RecargoVolumenTotal = 0,
                    DescuentoVolumenTotal = descuento
                };
            })
            .OrderBy(i => i.RazonSocial)
            .ToList();
    }

    // ---------------------------------------------------------------- Nombres

    private async Task<NombresReporte> NombresAsync(
        IEnumerable<Guid?> repartidorIds, IEnumerable<Guid?> vehiculoIds, IEnumerable<Guid> comercioOperadorIds, CancellationToken ct)
    {
        var repIds = repartidorIds.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var vehIds = vehiculoIds.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        var coIds = comercioOperadorIds.Distinct().ToList();

        var repartidores = await _db.Repartidores.AsNoTracking().Where(r => repIds.Contains(r.Id)).Select(r => new { r.Id, r.UsuarioId }).ToListAsync(ct);
        var usuarioIds = repartidores.Select(r => r.UsuarioId).ToList();
        var usuarios = await _db.Usuarios.AsNoTracking().Where(u => usuarioIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Nombre, ct);
        var vehiculos = await _db.Vehiculos.AsNoTracking().Where(v => vehIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Patente, ct);

        var vinculos = await _db.ComerciosOperadores.AsNoTracking().Where(c => coIds.Contains(c.Id)).Select(c => new { c.Id, c.ComercioId }).ToListAsync(ct);
        var comercioIds = vinculos.Select(v => v.ComercioId).ToList();
        var comercios = await _db.Comercios.AsNoTracking().Where(c => comercioIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        return new NombresReporte(
            repartidores.ToDictionary(r => r.Id, r => usuarios.GetValueOrDefault(r.UsuarioId) ?? string.Empty),
            vehiculos,
            vinculos.ToDictionary(v => v.Id, v => comercios.GetValueOrDefault(v.ComercioId)));
    }

    private sealed class NombresReporte
    {
        private readonly Dictionary<Guid, string> _repartidores;
        private readonly Dictionary<Guid, string> _vehiculos;
        private readonly Dictionary<Guid, Domain.Entities.Comercio.Comercio?> _comercios;

        public NombresReporte(Dictionary<Guid, string> repartidores, Dictionary<Guid, string> vehiculos, Dictionary<Guid, Domain.Entities.Comercio.Comercio?> comercios)
        {
            _repartidores = repartidores;
            _vehiculos = vehiculos;
            _comercios = comercios;
        }

        public string? Repartidor(Guid? id) => id.HasValue ? _repartidores.GetValueOrDefault(id.Value) : null;

        public string? Vehiculo(Guid? id) => id.HasValue ? _vehiculos.GetValueOrDefault(id.Value) : null;

        public (string Nombre, string? Detalle, string Documento) Comercio(Guid comercioOperadorId)
        {
            var comercio = _comercios.GetValueOrDefault(comercioOperadorId);
            if (comercio is null)
                return ("Comercio desconocido", null, string.Empty);

            var nombre = string.IsNullOrWhiteSpace(comercio.RazonSocial) ? comercio.NombreFantasia : comercio.RazonSocial;
            return (nombre, $"RUT {comercio.Documento}", comercio.Documento);
        }
    }
}
