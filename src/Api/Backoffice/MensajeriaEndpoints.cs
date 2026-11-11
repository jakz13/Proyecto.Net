using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Cola de mensajes fallidos (dead-letter): consulta, reintento manual y descarte.</summary>
public static class MensajeriaEndpoints
{
    public static IEndpointRouteBuilder MapMensajeriaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/mensajeria/cola-fallidos")
            .WithTags("Backoffice · Cola de mensajes fallidos")
            .RequireAuthorization(PoliticasApi.Administracion);

        grupo.MapGet("/", (
            [FromQuery] string? tipo,
            [FromQuery] string? estado,
            [FromQuery] string? busqueda,
            AppDbContext db,
            ICurrentTenant tenant,
            IWebHostEnvironment env,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = db.MensajesFallidos.AsNoTracking().Where(m => m.OperadorId == null || m.OperadorId == tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(tipo))
                consulta = consulta.Where(m => m.TipoMensaje == tipo);

            if (!string.IsNullOrWhiteSpace(estado))
                consulta = consulta.Where(m => m.Estado == estado);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var patron = RespuestasBackoffice.PatronContiene(busqueda);
                consulta = consulta.Where(m =>
                    EF.Functions.ILike(m.TipoMensaje, patron, "\\") ||
                    EF.Functions.ILike(m.Excepcion, patron, "\\") ||
                    EF.Functions.ILike(m.PayloadOriginal, patron, "\\") ||
                    EF.Functions.ILike(m.ColaOrigen, patron, "\\"));
            }

            var mensajes = await consulta.OrderByDescending(m => m.UltimoFallo).ToListAsync(ct);
            return Results.Ok(mensajes.Select(m => Mapear(m, env)).ToList());
        }))
        .WithName("ListarMensajesFallidos")
        .WithSummary("Listar los mensajes de la cola de fallidos");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, IWebHostEnvironment env, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var mensaje = await db.MensajesFallidos.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            return mensaje is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.")
                : Results.Ok(Mapear(mensaje, env));
        }))
        .WithName("ObtenerMensajeFallido")
        .WithSummary("Detalle de un mensaje fallido");

        grupo.MapPost("/reintentar", (
            [FromBody] ReintentarMensajesRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var ids = (cuerpo.Ids ?? new List<Guid>()).Distinct().ToList();
            if (ids.Count == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Debes seleccionar al menos un mensaje de la cola para reintentar.");

            var mensajes = await db.MensajesFallidos
                .Where(m => ids.Contains(m.Id) && (m.OperadorId == null || m.OperadorId == tenant.OperadorId))
                .ToListAsync(ct);

            var detalles = new List<string>();
            var exitosos = 0;
            var fallidos = 0;

            foreach (var id in ids.Where(i => mensajes.All(m => m.Id != i)))
                detalles.Add($"[Ignorado] El mensaje con ID '{id}' no se encontró en la cola de fallidos.");

            foreach (var mensaje in mensajes)
            {
                var corto = mensaje.Id.ToString()[..8];

                if (mensaje.Estado == "Resuelto")
                {
                    detalles.Add($"[Ignorado] '{mensaje.TipoMensaje}' ({corto}) ya estaba resuelto.");
                    continue;
                }

                if (cuerpo.ForzarFallo)
                {
                    mensaje.RegistrarReintentoFallido($"Reintento manual fallido en {DateTime.UtcNow:HH:mm:ss} UTC. El servicio de destino rechazó la solicitud.");
                    fallidos++;
                    detalles.Add($"[Fallo] '{mensaje.TipoMensaje}' ({corto}): Reintento no completado. Permanece en la cola con {mensaje.Intentos} intentos.");
                    continue;
                }

                // El mensaje original se reencola en el outbox para que el procesador de eventos lo vuelva a consumir
                await db.OutboxMessages.AddAsync(
                    new OutboxMessage(Guid.NewGuid(), "ColaFallidos", mensaje.Id, mensaje.TipoMensaje, mensaje.PayloadOriginal), ct);
                mensaje.Resolver();
                exitosos++;
                detalles.Add($"[Éxito] '{mensaje.TipoMensaje}' ({corto}): Reencolado para su procesamiento.");
            }

            await db.SaveChangesAsync(ct);

            var resumen = cuerpo.ForzarFallo
                ? $"Se ejecutaron {fallidos} reintento(s) y el sistema registró nueva falla (permanecen en la cola)."
                : $"Se reencolaron exitosamente {exitosos} mensaje(s) para su procesamiento.";

            return Results.Ok(new ResultadoReintentoDto
            {
                TotalSeleccionados = ids.Count,
                Exitosos = exitosos,
                Fallidos = fallidos,
                MensajeResumen = resumen,
                Detalles = detalles
            });
        }))
        .WithName("ReintentarMensajesFallidos")
        .WithSummary("Reintentar manualmente uno o varios mensajes");

        grupo.MapPost("/{id:guid}/descartar", (
            Guid id,
            [FromBody] DescartarMensajeRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            IWebHostEnvironment env,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivo = (cuerpo.Motivo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(motivo))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El motivo de descarte es obligatorio.");

            var mensaje = await db.MensajesFallidos
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            if (mensaje is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.");

            mensaje.Descartar(motivo);
            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(mensaje, env));
        }))
        .WithName("DescartarMensajeFallidoPost")
        .WithSummary("Descartar lógicamente un mensaje de la cola registrando el motivo");

        grupo.MapDelete("/{id:guid}", (
            Guid id,
            [FromQuery] string? motivo,
            AppDbContext db,
            ICurrentTenant tenant,
            IWebHostEnvironment env,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivoFinal = string.IsNullOrWhiteSpace(motivo) ? "Descartado manualmente por el administrador" : motivo.Trim();

            var mensaje = await db.MensajesFallidos
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            if (mensaje is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.");

            // Descarte lógico: pasa a estado Descartado y se conserva para auditoría
            mensaje.Descartar(motivoFinal);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("DescartarMensajeFallido")
        .WithSummary("Descartar lógicamente un mensaje de la cola");

        grupo.MapPost("/limpiar-resueltos", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var eliminados = await db.MensajesFallidos
                .Where(m => (m.Estado == "Resuelto" || m.Estado == "Descartado") && (m.OperadorId == null || m.OperadorId == tenant.OperadorId))
                .ExecuteDeleteAsync(ct);
            return Results.Ok(eliminados);
        }))
        .WithName("LimpiarMensajesResueltos")
        .WithSummary("Eliminar de la cola los mensajes ya resueltos o archivados");

        return app;
    }

    private static MensajeFallidoDto Mapear(MensajeFallidoCola m, IWebHostEnvironment env)
    {
        // El motivo es la primera línea de la excepción; el detalle conserva el texto completo
        var motivo = m.Excepcion.Split('\n', 2)[0].Trim();

        // En entornos que no sean Development (Producción/Staging), el servidor NO expone la traza técnica (UC-42)
        var detalleExcepcion = env.IsDevelopment() ? m.Excepcion : string.Empty;

        return new MensajeFallidoDto
        {
            Id = m.Id,
            TipoMensaje = m.TipoMensaje,
            ColaOrigen = m.ColaOrigen,
            PayloadOriginal = m.PayloadOriginal,
            MotivoFallo = motivo,
            DetalleExcepcion = detalleExcepcion,
            Intentos = m.Intentos,
            PrimerFallo = m.PrimerFallo,
            UltimoFallo = m.UltimoFallo,
            Estado = m.Estado,
            OperadorId = m.OperadorId?.ToString(),
            MotivoDescarte = m.Estado == "Descartado" ? motivo : null
        };
    }
}

public record ReintentarMensajesRequest(List<Guid>? Ids, bool ForzarFallo = false);

public record DescartarMensajeRequest(string Motivo);
