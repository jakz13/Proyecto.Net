using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Identidad visual (marca blanca) del operador: nombre, colores, logo y datos de contacto.</summary>
public static partial class IdentidadVisualEndpoints
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorHexRegex();

    public static IEndpointRouteBuilder MapIdentidadVisualEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/identidad-visual")
            .WithTags("Backoffice · Identidad visual")
            .RequireAuthorization();

        grupo.MapGet("/", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
            Results.Ok(await ObtenerAsync(db, tenant.OperadorId, ct))))
        .WithName("ObtenerIdentidadVisual")
        .WithSummary("Identidad visual del operador en sesión");

        grupo.MapGet("/operadores-demo", (AppDbContext db, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var operadorIds = await db.Operadores.AsNoTracking().OrderBy(o => o.NombreComercial).Select(o => o.Id).ToListAsync(ct);
            var identidades = new List<IdentidadVisualDto>();
            foreach (var id in operadorIds)
                identidades.Add(await ObtenerAsync(db, id, ct));
            return Results.Ok(identidades);
        }))
        .WithName("ListarOperadoresDemo")
        .WithSummary("Identidad visual de todos los operadores (selector de la demo)");

        grupo.MapGet("/operadores-demo/{operadorId}", (string operadorId, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var id = Guid.TryParse(operadorId, out var parsed) ? parsed : tenant.OperadorId;
                return Results.Ok(await ObtenerAsync(db, id, ct));
            }))
        .WithName("ObtenerIdentidadOperadorDemo")
        .WithSummary("Identidad visual de un operador puntual");

        grupo.MapPut("/", (
            [FromBody] ActualizarIdentidadVisualDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var nombre = (dto.NombreComercial ?? string.Empty).Trim();
            if (nombre.Length == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El nombre comercial es obligatorio.");

            if (!ColorHexRegex().IsMatch(dto.ColorPrimario ?? string.Empty) || !ColorHexRegex().IsMatch(dto.ColorSecundario ?? string.Empty))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Los colores deben tener formato hexadecimal #RRGGBB.");

            var operador = await db.Operadores.FirstOrDefaultAsync(o => o.Id == tenant.OperadorId, ct);
            if (operador is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el operador en sesión.");

            operador.ActualizarDatos(operador.RazonSocial, nombre);

            var identidad = await db.IdentidadesVisuales.FirstOrDefaultAsync(i => i.OperadorId == tenant.OperadorId, ct);
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
                identidad.ActualizarLogo(dto.LogoUrl);
            }

            identidad.ActualizarComunicaciones(dto.SitioWeb, dto.MensajeComunicaciones);
            await db.SaveChangesAsync(ct);

            return Results.Ok(Mapear(operador, identidad));
        }))
        .WithName("ActualizarIdentidadVisual")
        .WithSummary("Modificar la identidad visual del operador");

        return app;
    }

    private static async Task<IdentidadVisualDto> ObtenerAsync(AppDbContext db, Guid operadorId, CancellationToken ct)
    {
        var operador = await db.Operadores.AsNoTracking().FirstOrDefaultAsync(o => o.Id == operadorId, ct);
        var identidad = await db.IdentidadesVisuales.AsNoTracking().FirstOrDefaultAsync(i => i.OperadorId == operadorId, ct);
        return Mapear(operador, identidad, operadorId);
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
