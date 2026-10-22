using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Historial de los envíos retirados, reasignados o reprogramados de una hoja de ruta (UC-10).</summary>
public static class ReasignacionesEndpoints
{
    // List y no arreglo: con C# 14 Contains sobre un arreglo no se traduce a SQL en EF Core 8
    private static readonly List<string> TiposDeEvento = new() { "RetiroDeRuta", "RetiroEnRuta", "ReprogramacionFranja" };

    public static IEndpointRouteBuilder MapReasignacionesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/planificacion/hojas-de-ruta/{hojaDeRutaId:guid}/reasignaciones", (
            Guid hojaDeRutaId,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var ruta = await db.HojasDeRuta.AsNoTracking()
                .Where(h => h.Id == hojaDeRutaId && h.OperadorId == tenant.OperadorId)
                .Select(h => new { h.Id })
                .FirstOrDefaultAsync(ct);
            if (ruta is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la hoja de ruta solicitada.");

            // Un envío retirado de la ruta conserva su asignación (ya no vigente), que es el vínculo con la ruta
            var enviosDeLaRuta = db.AsignacionesEnvioRuta.AsNoTracking()
                .Where(a => a.HojaDeRutaId == hojaDeRutaId)
                .Select(a => a.EnvioId);

            var eventos = await db.EventosEnvio.AsNoTracking()
                .Where(e => enviosDeLaRuta.Contains(e.EnvioId) && TiposDeEvento.Contains(e.TipoEvento))
                .OrderByDescending(e => e.Timestamp)
                .Take(200)
                .Select(e => new { e.Id, e.EnvioId, e.TipoEvento, e.Timestamp, e.Payload, e.ResponsableUsuarioId })
                .ToListAsync(ct);

            var envioIds = eventos.Select(e => e.EnvioId).Distinct().ToList();
            var envios = await db.Envios.AsNoTracking()
                .Where(e => envioIds.Contains(e.Id))
                .Select(e => new { e.Id, e.CodigoSeguimientoPublico, e.NombreDestinatario })
                .ToDictionaryAsync(e => e.Id, ct);

            return Results.Ok(eventos.Select(e => new ReasignacionHistorialDto(
                e.Id, e.EnvioId,
                envios.GetValueOrDefault(e.EnvioId)?.CodigoSeguimientoPublico ?? string.Empty,
                envios.GetValueOrDefault(e.EnvioId)?.NombreDestinatario ?? string.Empty,
                e.TipoEvento, e.Timestamp, e.Payload, e.ResponsableUsuarioId)).ToList());
        }))
        .RequireAuthorization()
        .WithTags("Planificación (UC-08, UC-09, UC-10)")
        .WithName("ListarReasignacionesDeHojaDeRuta")
        .WithSummary("Historial de retiros y reprogramaciones de los envíos de una hoja de ruta");

        return app;
    }
}

public record ReasignacionHistorialDto(
    Guid EventoId,
    Guid EnvioId,
    string CodigoSeguimiento,
    string Destinatario,
    string TipoEvento,
    DateTime Fecha,
    string? Detalle,
    Guid? ResponsableUsuarioId);
