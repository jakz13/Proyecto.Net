using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Api.Auth;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Identidad visual (marca blanca) del operador: nombre, colores, logo y datos de contacto.</summary>
public static partial class IdentidadVisualEndpoints
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorHexRegex();

    public static IEndpointRouteBuilder MapIdentidadVisualEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/identidad-visual")
            .WithTags("Backoffice · Identidad visual");

        grupo.MapGet("/", (
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct,
            IDistributedCacheService? cache = null) => RespuestasBackoffice.Ejecutar(async () =>
            Results.Ok(await ObtenerAsync(db, tenant.OperadorId, ct, cache))))
        .WithName("ObtenerIdentidadVisual")
        .WithSummary("Identidad visual del operador en sesión")
        .AllowAnonymous();

        grupo.MapGet("/operadores-demo", (
            AppDbContext db,
            IConfiguration configuracion,
            IWebHostEnvironment entorno,
            CancellationToken ct,
            IDistributedCacheService? cache = null) => RespuestasBackoffice.Ejecutar(async () =>
        {
            // Listar a todos los operadores sin sesión solo tiene sentido en la demo local; en el servidor se oculta
            if (!ModoDemo.Permitido(configuracion, entorno))
                return Results.NotFound();

            var operadorIds = await db.Operadores.AsNoTracking().OrderBy(o => o.NombreComercial).Select(o => o.Id).ToListAsync(ct);
            var identidades = new List<IdentidadVisualDto>();
            foreach (var id in operadorIds)
                identidades.Add(await ObtenerAsync(db, id, ct, cache));
            return Results.Ok(identidades);
        }))
        .WithName("ListarOperadoresDemo")
        .WithSummary("Identidad visual de todos los operadores (selector de la demo)")
        .AllowAnonymous();

        grupo.MapGet("/operadores-demo/{operadorId}", (
            string operadorId,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct,
            IDistributedCacheService? cache = null) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var id = Guid.TryParse(operadorId, out var parsed) ? parsed : tenant.OperadorId;
                return Results.Ok(await ObtenerAsync(db, id, ct, cache));
            }))
        .WithName("ObtenerIdentidadOperadorDemo")
        .WithSummary("Identidad visual de un operador puntual")
        .AllowAnonymous();

        grupo.MapGet("/logos/{nombreArchivo}", (string nombreArchivo, IWebHostEnvironment env) =>
        {
            // Solo un nombre de archivo simple con extensión de imagen permitida (sin rutas ni SVG, que puede llevar scripts)
            if (string.IsNullOrWhiteSpace(nombreArchivo)
                || nombreArchivo != Path.GetFileName(nombreArchivo)
                || nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || nombreArchivo.Contains("..")
                || Path.GetExtension(nombreArchivo).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                return Results.BadRequest();

            var rutasPosibles = new[]
            {
                Path.Combine(env.ContentRootPath, "..", "Web", "Backoffice", "wwwroot", "uploads", "logos", nombreArchivo),
                Path.Combine(env.ContentRootPath, "uploads", "logos", nombreArchivo),
                Path.Combine(AppContext.BaseDirectory, "uploads", "logos", nombreArchivo)
            };

            foreach (var ruta in rutasPosibles)
            {
                if (File.Exists(ruta))
                {
                    var extension = Path.GetExtension(ruta).ToLowerInvariant();
                    var tipoMime = extension switch
                    {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".webp" => "image/webp",
                        _ => "application/octet-stream"
                    };
                    return Results.File(ruta, tipoMime);
                }
            }

            return Results.NotFound();
        })
        .WithName("ObtenerLogoOperador")
        .WithSummary("Servicio público de imagen o logotipo corporativo del operador")
        .AllowAnonymous();

        grupo.MapPut("/", (
            [FromBody] ActualizarIdentidadVisualDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct,
            ITableroRealtimeNotifier? notifier = null,
            IDistributedCacheService? cache = null) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var nombre = (dto.NombreComercial ?? string.Empty).Trim();
            if (nombre.Length == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El nombre comercial es obligatorio.");

            if (!ColorHexRegex().IsMatch(dto.ColorPrimario ?? string.Empty) || !ColorHexRegex().IsMatch(dto.ColorSecundario ?? string.Empty))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Los colores deben tener formato hexadecimal #RRGGBB.");

            var operador = await db.Operadores.FirstOrDefaultAsync(o => o.Id == tenant.OperadorId, ct);
            if (operador is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el operador en sesión.");

            if (db.Database.IsRelational())
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"IdentidadesVisuales\" ALTER COLUMN \"LogoUrl\" TYPE text;", ct);
                }
                catch
                {
                    // Columna ya de tipo text
                }
            }

            operador.ActualizarDatos(operador.RazonSocial, nombre);

            var identidad = await db.IdentidadesVisuales.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.OperadorId == tenant.OperadorId, ct);
            var colorPrimario = dto.ColorPrimario!.ToUpperInvariant();
            var colorSecundario = dto.ColorSecundario!.ToUpperInvariant();

            if (identidad is null)
            {
                identidad = new IdentidadVisual(Guid.NewGuid(), tenant.OperadorId, colorPrimario, colorSecundario,
                    dto.EmailContacto ?? string.Empty, dto.TelefonoContacto ?? string.Empty, dto.LogoUrl);
                await db.IdentidadesVisuales.AddAsync(identidad, ct);
            }
            else
            {
                identidad.ActualizarColores(colorPrimario, colorSecundario);
                identidad.ActualizarContacto(dto.EmailContacto ?? string.Empty, dto.TelefonoContacto ?? string.Empty);
                // Conservar la foto/logo existente cuando el usuario solo cambia nombre o colores sin subir una nueva ni pedir eliminarla
                if (dto.EliminarLogo)
                {
                    identidad.ActualizarLogo(null);
                }
                else if (!string.IsNullOrWhiteSpace(dto.LogoUrl))
                {
                    identidad.ActualizarLogo(dto.LogoUrl.Trim());
                }
            }

            identidad.ActualizarComunicaciones(dto.SitioWeb, dto.MensajeComunicaciones);

            var correlationId = ContextoCorrelacion.NormalizarOGenerar(ContextoCorrelacion.CorrelationIdActual);
            var eventoWorker = new EventoIdentidadVisualActualizada(
                tenant.OperadorId,
                identidad.Id,
                operador.NombreComercial,
                identidad.ColorPrimario,
                identidad.ColorSecundario,
                DateTime.UtcNow,
                correlationId);

            var mensajeOutbox = new OutboxMessage(
                Guid.NewGuid(),
                "IdentidadVisual",
                identidad.Id,
                EventoIdentidadVisualActualizada.TipoMensaje,
                eventoWorker.Serializar());
            await db.OutboxMessages.AddAsync(mensajeOutbox, ct);

            await db.SaveChangesAsync(ct);

            var resultadoDto = Mapear(operador, identidad);
            if (cache is not null)
            {
                var claveCache = ClavesCache.IdentidadVisualPorOperador(tenant.OperadorId);
                await cache.RemoveAsync(claveCache, ct);
                await cache.SetAsync(claveCache, resultadoDto, ClavesCache.ExpiracionIdentidadVisual, ct);
            }

            if (notifier is not null)
            {
                await notifier.NotificarIdentidadVisualActualizadaAsync(tenant.OperadorId, ct);
            }

            return Results.Ok(resultadoDto);
        }))
        .RequireAuthorization(PoliticasApi.Administracion)
        .WithName("ActualizarIdentidadVisual")
        .WithSummary("Modificar la identidad visual del operador");

        grupo.MapPost("/probar-correo", async (
            [FromBody] ProbarCorreoIdentidadRequest request,
            ICurrentTenant tenant,
            UltimaMilla.Infrastructure.Services.RegistroNotificacionDestinatarioService servicioCorreo,
            CancellationToken ct) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.EmailDestino))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Debe indicar una dirección de correo de destino.");

            var estado = UltimaMilla.Domain.Enums.EstadoEnvio.EnTransito;
            if (!string.IsNullOrWhiteSpace(request.EstadoEnvio)
                && Enum.TryParse<UltimaMilla.Domain.Enums.EstadoEnvio>(request.EstadoEnvio, ignoreCase: true, out var estadoParseado))
            {
                estado = estadoParseado;
            }

            var (exitoso, mensaje) = await servicioCorreo.EnviarCorreoPruebaAsync(tenant.OperadorId, request.EmailDestino, estado, ct);
            return exitoso
                ? Results.Ok(new { exitoso = true, mensaje })
                : Results.BadRequest(new { exitoso = false, error = mensaje });
        })
        .RequireAuthorization(PoliticasApi.Administracion)
        .WithName("ProbarCorreoIdentidadVisual")
        .WithSummary("Enviar un correo de prueba al destinatario con la identidad visual del operador");

        return app;
    }

    public record ProbarCorreoIdentidadRequest(string EmailDestino, string? EstadoEnvio = "EnTransito");

    private static async Task<IdentidadVisualDto> ObtenerAsync(
        AppDbContext db,
        Guid operadorId,
        CancellationToken ct,
        IDistributedCacheService? cache = null)
    {
        if (operadorId == Guid.Empty)
        {
            operadorId = await db.Operadores.AsNoTracking()
                .Where(o => o.Estado == "Activo")
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (cache is not null && operadorId != Guid.Empty)
        {
            var enCache = await cache.GetAsync<IdentidadVisualDto>(ClavesCache.IdentidadVisualPorOperador(operadorId), ct);
            if (enCache is not null)
                return enCache;
        }

        var operador = await db.Operadores.AsNoTracking().FirstOrDefaultAsync(o => o.Id == operadorId, ct);
        var identidad = await db.IdentidadesVisuales.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(i => i.OperadorId == operadorId, ct);
        var dto = Mapear(operador, identidad, operadorId);

        if (cache is not null && operador is not null)
        {
            await cache.SetAsync(ClavesCache.IdentidadVisualPorOperador(operadorId), dto, ClavesCache.ExpiracionIdentidadVisual, ct);
        }

        return dto;
    }

    /// <summary>Sin identidad configurada se responden los valores por defecto del DTO, para que las pantallas siempre tengan una marca.</summary>
    private static IdentidadVisualDto Mapear(Operador? operador, IdentidadVisual? identidad, Guid? operadorId = null)
    {
        var porDefecto = new IdentidadVisualDto();
        return new IdentidadVisualDto
        {
            OperadorId = (operador?.Id ?? identidad?.OperadorId ?? operadorId)?.ToString() ?? string.Empty,
            NombreComercial = operador?.NombreComercial ?? porDefecto.NombreComercial,
            ColorPrimario = identidad?.ColorPrimario ?? porDefecto.ColorPrimario,
            ColorSecundario = identidad?.ColorSecundario ?? porDefecto.ColorSecundario,
            LogoUrl = identidad?.LogoUrl,
            EmailContacto = identidad?.EmailContacto,
            TelefonoContacto = identidad?.TelefonoContacto,
            SitioWeb = identidad?.SitioWeb,
            MensajeComunicaciones = identidad?.MensajeComunicaciones
        };
    }
}
