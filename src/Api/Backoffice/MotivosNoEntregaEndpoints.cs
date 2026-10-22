using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Catálogo de motivos de no entrega que ofrece la app del repartidor.</summary>
public static class MotivosNoEntregaEndpoints
{
    public static IEndpointRouteBuilder MapMotivosNoEntregaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/motivos-no-entrega")
            .WithTags("Backoffice · Motivos de no entrega")
            .RequireAuthorization();

        grupo.MapGet("/", (
            [FromQuery] string? busqueda,
            [FromQuery] bool? soloActivos,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = CatalogoVisible(db, tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var patron = RespuestasBackoffice.PatronContiene(busqueda);
                consulta = consulta.Where(m => EF.Functions.ILike(m.Codigo, patron, "\\") || EF.Functions.ILike(m.Descripcion, patron, "\\"));
            }

            if (soloActivos == true)
                consulta = consulta.Where(m => m.Activo);

            var motivos = await consulta.OrderBy(m => m.Codigo).ToListAsync(ct);
            return Results.Ok(await MapearAsync(db, motivos, ct));
        }))
        .WithName("ListarMotivosNoEntrega")
        .WithSummary("Listar el catálogo de motivos de no entrega");

        grupo.MapGet("/resumen", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivos = await CatalogoVisible(db, tenant.OperadorId).ToListAsync(ct);
            return Results.Ok(new ResumenMotivosNoEntregaDto
            {
                TotalMotivos = motivos.Count,
                MotivosActivosParaMovil = motivos.Count(m => m.Activo),
                MotivosInactivos = motivos.Count(m => !m.Activo),
                UltimaSincronizacionApp = DateTime.UtcNow
            });
        }))
        .WithName("ResumenMotivosNoEntrega")
        .WithSummary("Totales del catálogo de motivos");

        grupo.MapGet("/sincronizacion-movil", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivos = await CatalogoVisible(db, tenant.OperadorId).Where(m => m.Activo).OrderBy(m => m.Codigo).ToListAsync(ct);
            return Results.Ok(await MapearAsync(db, motivos, ct));
        }))
        .WithName("MotivosParaSincronizacionMovil")
        .WithSummary("Motivos activos que descarga la app del repartidor");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivo = await CatalogoVisible(db, tenant.OperadorId).FirstOrDefaultAsync(m => m.Id == id, ct);
            return motivo is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el motivo de no entrega.")
                : Results.Ok((await MapearAsync(db, new[] { motivo }, ct)).Single());
        }))
        .WithName("ObtenerMotivoNoEntrega")
        .WithSummary("Detalle de un motivo");

        grupo.MapPost("/", (
            [FromBody] CrearMotivoNoEntregaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
            if (await db.MotivosNoEntrega.AnyAsync(m => m.OperadorId == tenant.OperadorId && m.Codigo == codigo, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"Ya existe un motivo con el código {codigo}.");

            var motivo = new MotivoNoEntrega(Guid.NewGuid(), tenant.OperadorId, codigo, dto.Descripcion);
            if (!dto.IniciarActivo)
                motivo.Desactivar();

            await db.MotivosNoEntrega.AddAsync(motivo, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/motivos-no-entrega/{motivo.Id}", (await MapearAsync(db, new[] { motivo }, ct)).Single());
        }))
        .WithName("CrearMotivoNoEntrega")
        .WithSummary("Agregar un motivo al catálogo");

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarMotivoNoEntregaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivo = await CatalogoVisible(db, tenant.OperadorId, seguimiento: true).FirstOrDefaultAsync(m => m.Id == id, ct);
            if (motivo is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el motivo de no entrega.");

            motivo.ModificarDescripcion(dto.Descripcion);
            if (dto.Activo) motivo.Activar(); else motivo.Desactivar();
            await db.SaveChangesAsync(ct);

            return Results.Ok((await MapearAsync(db, new[] { motivo }, ct)).Single());
        }))
        .WithName("ActualizarMotivoNoEntrega")
        .WithSummary("Modificar la descripción o el estado de un motivo");

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivo = await CatalogoVisible(db, tenant.OperadorId, seguimiento: true).FirstOrDefaultAsync(m => m.Id == id, ct);
            if (motivo is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el motivo de no entrega.");

            if (cuerpo.Activo) motivo.Activar(); else motivo.Desactivar();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoMotivoNoEntrega")
        .WithSummary("Activar o desactivar un motivo");

        return app;
    }

    /// <summary>Los motivos internos del depósito son parte del sistema: no se listan ni se pueden modificar desde el catálogo.</summary>
    private static IQueryable<MotivoNoEntrega> CatalogoVisible(AppDbContext db, Guid operadorId, bool seguimiento = false)
    {
        var motivos = seguimiento ? db.MotivosNoEntrega : db.MotivosNoEntrega.AsNoTracking();
        return motivos.Where(m => m.OperadorId == operadorId && !m.EsInterno);
    }

    private static async Task<List<MotivoNoEntregaDto>> MapearAsync(AppDbContext db, IReadOnlyCollection<MotivoNoEntrega> motivos, CancellationToken ct)
    {
        var ids = motivos.Select(m => m.Id).ToList();
        var usos = await db.IntentosEntrega.AsNoTracking()
            .Where(i => i.MotivoNoEntregaId != null && ids.Contains(i.MotivoNoEntregaId.Value))
            .GroupBy(i => i.MotivoNoEntregaId!.Value)
            .Select(g => new { Id = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Cantidad, ct);

        return motivos.Select(m => new MotivoNoEntregaDto
        {
            Id = m.Id,
            Codigo = m.Codigo,
            Descripcion = m.Descripcion,
            Activo = m.Activo,
            FechaAlta = m.FechaAlta,
            CantidadUsos = usos.GetValueOrDefault(m.Id)
        }).ToList();
    }
}
