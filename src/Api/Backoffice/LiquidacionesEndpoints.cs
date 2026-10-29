using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Frontend.Contracts.Modelos.Liquidaciones;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Liquidación a los comercios y cuenta corriente (RF-30, RF-34). El operador cierra el período y genera las liquidaciones;
/// cada comercio consulta su estado de cuenta y las liquidaciones que le corresponden.
/// </summary>
public static class LiquidacionesEndpoints
{
    public const decimal TasaIva = 0.22m;
    private const int MaximoMovimientosConsultados = 200;
    private const int MaximoDiasDelPeriodo = 366;

    public static IEndpointRouteBuilder MapLiquidacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var operador = app.MapGroup("/api/liquidaciones")
            .RequireAuthorization()
            .WithTags("Liquidaciones a comercios (RF-30)");

        operador.MapPost("/generar", (GenerarLiquidacionesDto peticion, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                if (peticion.Desde > peticion.Hasta)
                    return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "La fecha inicial no puede ser posterior a la final.");
                if (peticion.Hasta.DayNumber - peticion.Desde.DayNumber >= MaximoDiasDelPeriodo)
                    return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"El período no puede superar los {MaximoDiasDelPeriodo} días.");

                return Results.Ok(await GenerarAsync(db, tenant.OperadorId, peticion, ct));
            }))
            .WithName("GenerarLiquidaciones")
            .WithSummary("Cierra el período y liquida los envíos entregados que aún no se cobraron");

        operador.MapGet("", (Guid? comercioOperadorId, string? estado, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var propias = db.ComerciosOperadores.AsNoTracking().Where(c => c.OperadorId == tenant.OperadorId).Select(c => c.Id);
                var consulta = db.Liquidaciones.AsNoTracking().Where(l => propias.Contains(l.ComercioOperadorId));
                if (comercioOperadorId.HasValue)
                    consulta = consulta.Where(l => l.ComercioOperadorId == comercioOperadorId);
                if (!string.IsNullOrWhiteSpace(estado))
                {
                    var buscado = estado.Trim();
                    consulta = consulta.Where(l => l.Estado == buscado);
                }

                return Results.Ok(await ListarAsync(db, consulta.OrderByDescending(l => l.FechaGeneracion).Take(200), ct));
            }))
            .WithName("ListarLiquidaciones")
            .WithSummary("Liquidaciones del operador, con filtro por comercio y estado");

        operador.MapPost("/{id:guid}/aprobar", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var liquidacion = await BuscarPropiaAsync(db, tenant.OperadorId, id, ct);
                if (liquidacion is null)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la liquidación.");

                liquidacion.Aprobar();
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }))
            .WithName("AprobarLiquidacion")
            .WithSummary("Aprueba una liquidación pendiente");

        operador.MapPost("/{id:guid}/pagar", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var liquidacion = await BuscarPropiaAsync(db, tenant.OperadorId, id, ct);
                if (liquidacion is null)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la liquidación.");

                liquidacion.Pagar();

                // El pago baja el saldo que adeuda el comercio
                var cuenta = await db.CuentasCorrientes.FirstOrDefaultAsync(c => c.ComercioOperadorId == liquidacion.ComercioOperadorId, ct)
                    ?? throw new InvalidOperationException("La liquidación no tiene cuenta corriente asociada.");
                cuenta.RegistrarMovimiento($"Pago de la liquidación del {FechaCorta(liquidacion.PeriodoDesde)} al {FechaCorta(liquidacion.PeriodoHasta)}",
                    -liquidacion.MontoTotal, liquidacionId: liquidacion.Id);

                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }))
            .WithName("PagarLiquidacion")
            .WithSummary("Registra el pago de una liquidación aprobada");

        // Vista del propio comercio (portal): solo ve su información
        var comercio = app.MapGroup("/api/comercio")
            .RequireAuthorization()
            .WithTags("Cuenta corriente y liquidaciones del comercio (RF-34)");

        comercio.MapGet("/cuenta-corriente", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                if (tenant.ComercioOperadorId is not { } vinculoId)
                    return Results.Forbid();

                var cuenta = await db.CuentasCorrientes.AsNoTracking().FirstOrDefaultAsync(c => c.ComercioOperadorId == vinculoId, ct);
                if (cuenta is null)
                    return Results.Ok(new CuentaCorrienteDto { ComercioOperadorId = vinculoId });

                var movimientos = await db.MovimientosCuentaCorriente.AsNoTracking()
                    .Where(m => m.CuentaCorrienteId == cuenta.Id)
                    .OrderByDescending(m => m.Fecha)
                    .Take(MaximoMovimientosConsultados)
                    .Select(m => new MovimientoCuentaCorrienteDto
                    {
                        Id = m.Id, Fecha = m.Fecha, Concepto = m.Concepto, Monto = m.Monto, EnvioId = m.EnvioId, LiquidacionId = m.LiquidacionId
                    })
                    .ToListAsync(ct);

                return Results.Ok(new CuentaCorrienteDto
                {
                    ComercioOperadorId = vinculoId, Moneda = cuenta.Moneda, Saldo = cuenta.Saldo, Movimientos = movimientos
                });
            }))
            .WithName("ObtenerCuentaCorrienteDelComercio")
            .WithSummary("Saldo y últimos movimientos del comercio autenticado");

        comercio.MapGet("/liquidaciones", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                if (tenant.ComercioOperadorId is not { } vinculoId)
                    return Results.Forbid();

                var consulta = db.Liquidaciones.AsNoTracking()
                    .Where(l => l.ComercioOperadorId == vinculoId && l.Estado != Liquidacion.EstadoAnulada)
                    .OrderByDescending(l => l.FechaGeneracion)
                    .Take(200);
                return Results.Ok(await ListarAsync(db, consulta, ct));
            }))
            .WithName("ListarLiquidacionesDelComercio")
            .WithSummary("Liquidaciones del comercio autenticado");

        return app;
    }

    /// <summary>
    /// Liquida los envíos entregados en el período que todavía no figuran en ninguna liquidación. Se carga la tarifa con la que se
    /// admitió cada envío (la vigente en su alta), por lo que un cambio posterior del cuadro tarifario no altera lo ya cobrado.
    /// </summary>
    internal static async Task<ResultadoGeneracionLiquidacionesDto> GenerarAsync(
        AppDbContext db, Guid operadorId, GenerarLiquidacionesDto peticion, CancellationToken ct)
    {
        var desdeUtc = ZonaHorariaOperador.InicioDelDiaUtc(peticion.Desde);
        var hastaUtc = ZonaHorariaOperador.InicioDelDiaUtc(peticion.Hasta.AddDays(1));

        var pendientes = await db.IntentosEntrega.AsNoTracking()
            .Where(i => i.Resultado == "Exitoso" && i.Timestamp >= desdeUtc && i.Timestamp < hastaUtc)
            .Join(db.Envios.AsNoTracking().Where(e => e.OperadorId == operadorId && e.TarifaCalculada > 0
                        && (peticion.ComercioOperadorId == null || e.ComercioOperadorId == peticion.ComercioOperadorId)
                        && !db.MovimientosCuentaCorriente.Any(m => m.EnvioId == e.Id && m.LiquidacionId != null)),
                i => i.EnvioId, e => e.Id,
                (i, e) => new { e.Id, e.ComercioOperadorId, e.CodigoSeguimientoPublico, e.TarifaCalculada })
            .Distinct()
            .ToListAsync(ct);

        if (pendientes.Count == 0)
        {
            return new ResultadoGeneracionLiquidacionesDto
            {
                Mensaje = "No hay envíos entregados pendientes de liquidar en el período indicado."
            };
        }

        var comercios = await NombresDeComerciosAsync(db, pendientes.Select(p => p.ComercioOperadorId), ct);
        var resultado = new List<LiquidacionDto>();
        var ahora = DateTime.UtcNow;

        foreach (var grupo in pendientes.GroupBy(p => p.ComercioOperadorId))
        {
            var subtotal = grupo.Sum(p => p.TarifaCalculada);
            var iva = Math.Round(subtotal * TasaIva, 2);
            var liquidacion = new Liquidacion(Guid.NewGuid(), grupo.Key, desdeUtc, hastaUtc.AddTicks(-1), subtotal + iva);

            var cuenta = await db.CuentasCorrientes.FirstOrDefaultAsync(c => c.ComercioOperadorId == grupo.Key, ct);
            if (cuenta is null)
            {
                cuenta = new CuentaCorriente(Guid.NewGuid(), grupo.Key);
                db.CuentasCorrientes.Add(cuenta);
            }

            foreach (var envio in grupo.OrderBy(p => p.CodigoSeguimientoPublico))
                cuenta.RegistrarMovimiento($"Envío {envio.CodigoSeguimientoPublico}", envio.TarifaCalculada, envio.Id, liquidacion.Id);

            cuenta.RegistrarMovimiento($"IVA {TasaIva:P0} de la liquidación del {peticion.Desde:dd/MM/yyyy} al {peticion.Hasta:dd/MM/yyyy}", iva, liquidacionId: liquidacion.Id);

            db.Liquidaciones.Add(liquidacion);
            resultado.Add(new LiquidacionDto
            {
                Id = liquidacion.Id,
                ComercioOperadorId = grupo.Key,
                Comercio = comercios.GetValueOrDefault(grupo.Key) ?? string.Empty,
                PeriodoDesde = peticion.Desde,
                PeriodoHasta = peticion.Hasta,
                CantidadEnvios = grupo.Count(),
                Subtotal = subtotal,
                Iva = iva,
                MontoTotal = liquidacion.MontoTotal,
                Estado = liquidacion.Estado,
                FechaGeneracion = ahora
            });
        }

        await db.SaveChangesAsync(ct);

        return new ResultadoGeneracionLiquidacionesDto
        {
            Liquidaciones = resultado,
            EnviosLiquidados = pendientes.Count,
            Mensaje = $"Se generaron {resultado.Count} liquidación(es) por {pendientes.Count} envío(s)."
        };
    }

    private static async Task<Liquidacion?> BuscarPropiaAsync(AppDbContext db, Guid operadorId, Guid id, CancellationToken ct)
    {
        // Sin AsNoTracking en la subconsulta: EF lo aplicaría a toda la consulta y la liquidación no se rastrearía para guardarla
        var propias = db.ComerciosOperadores.Where(c => c.OperadorId == operadorId).Select(c => c.Id);
        return await db.Liquidaciones.FirstOrDefaultAsync(l => l.Id == id && propias.Contains(l.ComercioOperadorId), ct);
    }

    private static async Task<List<LiquidacionDto>> ListarAsync(AppDbContext db, IQueryable<Liquidacion> consulta, CancellationToken ct)
    {
        var liquidaciones = await consulta.ToListAsync(ct);
        if (liquidaciones.Count == 0)
            return new List<LiquidacionDto>();

        var ids = liquidaciones.Select(l => l.Id).ToList();
        var movimientos = await db.MovimientosCuentaCorriente.AsNoTracking()
            .Where(m => m.LiquidacionId != null && ids.Contains(m.LiquidacionId.Value))
            .Select(m => new { LiquidacionId = m.LiquidacionId!.Value, m.EnvioId, m.Monto })
            .ToListAsync(ct);
        var porLiquidacion = movimientos.ToLookup(m => m.LiquidacionId);
        var nombres = await NombresDeComerciosAsync(db, liquidaciones.Select(l => l.ComercioOperadorId), ct);

        return liquidaciones.Select(l =>
        {
            var cargos = porLiquidacion[l.Id].Where(m => m.Monto > 0).ToList();
            var subtotal = cargos.Where(m => m.EnvioId != null).Sum(m => m.Monto);
            return new LiquidacionDto
            {
                Id = l.Id,
                ComercioOperadorId = l.ComercioOperadorId,
                Comercio = nombres.GetValueOrDefault(l.ComercioOperadorId) ?? string.Empty,
                PeriodoDesde = ZonaHorariaOperador.FechaLocal(l.PeriodoDesde),
                PeriodoHasta = ZonaHorariaOperador.FechaLocal(l.PeriodoHasta),
                CantidadEnvios = cargos.Count(m => m.EnvioId != null),
                Subtotal = subtotal,
                Iva = l.MontoTotal - subtotal,
                MontoTotal = l.MontoTotal,
                Estado = l.Estado,
                FechaGeneracion = l.FechaGeneracion
            };
        }).ToList();
    }

    private static async Task<Dictionary<Guid, string>> NombresDeComerciosAsync(AppDbContext db, IEnumerable<Guid> comercioOperadorIds, CancellationToken ct)
    {
        var ids = comercioOperadorIds.Distinct().ToList();
        var vinculos = await db.ComerciosOperadores.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.ComercioId }).ToListAsync(ct);
        var comercioIds = vinculos.Select(v => v.ComercioId).ToList();
        var comercios = await db.Comercios.AsNoTracking().Where(c => comercioIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        return vinculos.ToDictionary(
            v => v.Id,
            v => comercios.GetValueOrDefault(v.ComercioId) is { } c ? (string.IsNullOrWhiteSpace(c.RazonSocial) ? c.NombreFantasia : c.RazonSocial) : string.Empty);
    }

    private static string FechaCorta(DateTime utc) => ZonaHorariaOperador.FechaLocal(utc).ToString("dd/MM/yyyy");
}
