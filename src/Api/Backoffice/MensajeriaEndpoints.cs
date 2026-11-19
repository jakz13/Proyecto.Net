using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Commands.Mensajeria.DescartarAviso;
using UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Mensajeria.ListarAvisosEnColaDeFallidos;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Cola de mensajes fallidos (dead-letter): consulta, reintento manual y descarte.
/// Incluye los avisos a comercios cuyos reintentos automáticos se agotaron (letra 6.9): su estado vive en el propio aviso
/// (única fuente de verdad), y aquí se presentan como un mensaje más de tipo <see cref="TipoMensajeAviso"/>.
/// </summary>
public static class MensajeriaEndpoints
{
    /// <summary>Tipo que la pantalla del Backoffice ya reconoce como "Aviso a Comercio (Webhook)".</summary>
    public const string TipoMensajeAviso = "NotificacionWebhookComercio";
    public const string ColaOrigenAvisos = "avisos-comercio";

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
            IMediator mediator,
            IWebHostEnvironment env,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = db.MensajesFallidos.AsNoTracking().Where(m => m.OperadorId == null || m.OperadorId == tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(tipo))
                consulta = consulta.Where(m => m.TipoMensaje == tipo);

            var estados = EstadosPersistidos(estado);
            if (estados is not null)
                consulta = consulta.Where(m => estados.Contains(m.Estado));

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
            var respuesta = mensajes.Select(m => Mapear(m, env)).ToList();

            // Los avisos agotados esperan una decisión del administrador, igual que cualquier otro mensaje fallido
            if (IncluyeAvisos(tipo, estado))
            {
                var avisos = await mediator.Send(new ListarAvisosEnColaDeFallidosQuery(), ct);
                respuesta.AddRange(avisos
                    .Where(a => CoincideBusqueda(a, busqueda))
                    .Select(a => MapearAviso(a, env)));
            }

            return Results.Ok(respuesta.OrderByDescending(m => m.UltimoFallo).ToList());
        }))
        .WithName("ListarMensajesFallidos")
        .WithSummary("Listar los mensajes de la cola de fallidos");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, IMediator mediator, IWebHostEnvironment env, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var mensaje = await db.MensajesFallidos.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            if (mensaje is not null)
                return Results.Ok(Mapear(mensaje, env));

            var aviso = await BuscarAvisoEnColaAsync(mediator, id, ct);
            return aviso is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.")
                : Results.Ok(MapearAviso(aviso, env));
        }))
        .WithName("ObtenerMensajeFallido")
        .WithSummary("Detalle de un mensaje fallido");

        grupo.MapPost("/reintentar", (
            [FromBody] ReintentarMensajesRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            IMediator mediator,
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

            // Lo que no está entre los mensajes puede ser un aviso agotado: se reintenta reenviándolo, no reencolando el evento original
            // (el consumidor ya lo procesó y lo vería como duplicado)
            foreach (var id in ids.Where(i => mensajes.All(m => m.Id != i)))
            {
                var aviso = await BuscarAvisoEnColaAsync(mediator, id, ct);
                if (aviso is null)
                {
                    detalles.Add($"[Ignorado] El mensaje con ID '{id}' no se encontró en la cola de fallidos.");
                    continue;
                }

                var corto = id.ToString()[..8];
                if (cuerpo.ForzarFallo)
                {
                    detalles.Add($"[Ignorado] El aviso a comercio ({corto}) no admite simular un fallo: se reenvía de verdad.");
                    continue;
                }

                try
                {
                    var resultado = await mediator.Send(new ReenviarAvisoManualCommand(id), ct);
                    if (resultado.Estado == "Entregado")
                    {
                        exitosos++;
                        detalles.Add($"[Éxito] 'Aviso a Comercio' ({corto}): el comercio recibió el aviso ({resultado.UltimaRespuestaHttp}).");
                    }
                    else
                    {
                        fallidos++;
                        detalles.Add($"[Fallo] 'Aviso a Comercio' ({corto}): el comercio respondió '{resultado.UltimaRespuestaHttp}'. " +
                                     "Se reiniciaron los reintentos automáticos; si se agotan, vuelve a esta cola.");
                    }
                }
                catch (DomainValidationException ex)
                {
                    detalles.Add($"[Ignorado] 'Aviso a Comercio' ({corto}): {ex.Message}");
                }
            }

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
                : fallidos > 0
                    ? $"Se reintentaron {exitosos + fallidos} mensaje(s): {exitosos} con éxito y {fallidos} volvieron a fallar."
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
            IMediator mediator,
            IWebHostEnvironment env,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivo = (cuerpo.Motivo ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(motivo))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El motivo de descarte es obligatorio.");

            var mensaje = await db.MensajesFallidos
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            if (mensaje is null)
            {
                var aviso = await BuscarAvisoEnColaAsync(mediator, id, ct);
                if (aviso is null)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.");

                await mediator.Send(new DescartarAvisoCommand(id, motivo), ct);
                return Results.Ok(MapearAviso(aviso, env, "Descartado", motivo));
            }

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
            IMediator mediator,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var motivoFinal = string.IsNullOrWhiteSpace(motivo) ? "Descartado manualmente por el administrador" : motivo.Trim();

            var mensaje = await db.MensajesFallidos
                .FirstOrDefaultAsync(m => m.Id == id && (m.OperadorId == null || m.OperadorId == tenant.OperadorId), ct);
            if (mensaje is null)
            {
                if (await BuscarAvisoEnColaAsync(mediator, id, ct) is null)
                    return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el mensaje en la cola de fallidos.");

                await mediator.Send(new DescartarAvisoCommand(id, motivoFinal), ct);
                return Results.NoContent();
            }

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

    /// <summary>
    /// La pantalla filtra con el vocabulario canónico ("Pendiente", "Reintentado", "Descartado") y la base guarda los estados reales
    /// ("Fallido", "Reintentando", "Resuelto", "Descartado"): se traduce, porque comparar uno con otro no coincidía nunca.
    /// Devuelve <c>null</c> cuando no hay filtro por estado.
    /// </summary>
    /// <remarks>Devuelve una lista y no un arreglo a propósito: en C# 14 <c>arreglo.Contains(x)</c> dentro de una consulta de EF se enlaza a
    /// <c>MemoryExtensions.Contains(ReadOnlySpan)</c>, que EF no puede evaluar (500 en tiempo de ejecución, no al compilar).</remarks>
    internal static List<string>? EstadosPersistidos(string? estado) => estado?.Trim() switch
    {
        null or "" or "todos" => null,
        "Pendiente" or "Fallido" => new List<string> { "Fallido" },
        "Reintentado" => new List<string> { "Reintentando", "Resuelto" },
        var otro => new List<string> { otro }
    };

    /// <summary>Los avisos agotados son mensajes pendientes de decisión: aparecen sin filtro de estado o al filtrar por pendientes.</summary>
    internal static bool IncluyeAvisos(string? tipo, string? estado)
    {
        var tipoCoincide = string.IsNullOrWhiteSpace(tipo) || tipo == "todos" || tipo == TipoMensajeAviso;
        var estados = EstadosPersistidos(estado);
        return tipoCoincide && (estados is null || estados.Contains("Fallido"));
    }

    private static bool CoincideBusqueda(AvisoEnColaDeFallidosDto aviso, string? busqueda)
    {
        if (string.IsNullOrWhiteSpace(busqueda))
            return true;

        var texto = busqueda.Trim();
        return aviso.Payload.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || (aviso.UltimaRespuestaHttp?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false)
            || TipoMensajeAviso.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || ColaOrigenAvisos.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || aviso.Id.ToString().Contains(texto, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<AvisoEnColaDeFallidosDto?> BuscarAvisoEnColaAsync(IMediator mediator, Guid id, CancellationToken ct) =>
        (await mediator.Send(new ListarAvisosEnColaDeFallidosQuery(id), ct)).FirstOrDefault();

    private static MensajeFallidoDto Mapear(MensajeFallidoCola m, IWebHostEnvironment env)
    {
        // El motivo es la primera línea de la excepción; el detalle conserva el texto completo
        var motivo = m.Excepcion.Split('\n', 2)[0].Trim();

        // En entornos que no sean Development (Producción/Staging), el servidor NO expone la traza técnica
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

    /// <summary>
    /// El aviso no guarda la fecha de su último intento, solo la de creación: ambas fechas de la fila usan esa.
    /// </summary>
    private static MensajeFallidoDto MapearAviso(AvisoEnColaDeFallidosDto a, IWebHostEnvironment env, string estado = "Fallido", string? motivoDescarte = null)
    {
        var respuesta = a.UltimaRespuestaHttp ?? string.Empty;
        var motivo = string.IsNullOrWhiteSpace(respuesta) ? "El comercio no recibió el aviso" : respuesta.Split('\n', 2)[0].Trim();

        return new MensajeFallidoDto
        {
            Id = a.Id,
            TipoMensaje = TipoMensajeAviso,
            ColaOrigen = ColaOrigenAvisos,
            PayloadOriginal = a.Payload,
            MotivoFallo = motivo,
            DetalleExcepcion = env.IsDevelopment() ? respuesta : string.Empty,
            Intentos = a.Intentos,
            PrimerFallo = a.Timestamp,
            UltimoFallo = a.Timestamp,
            Estado = estado,
            MotivoDescarte = motivoDescarte
        };
    }
}

public record ReintentarMensajesRequest(List<Guid>? Ids, bool ForzarFallo = false);

public record DescartarMensajeRequest(string Motivo);
