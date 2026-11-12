using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

public static class MobileOperadorEndpoints
{
    public static IEndpointRouteBuilder MapMobileOperadorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/mobile/v1/operador")
            .WithTags("Mobile - Identidad Visual y Operador")
            .RequireAuthorization(PoliticasApi.PersonalDelOperador);

        group.MapGet("/identidad-visual", async (
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            var operadorId = tenant.OperadorId;
            if (operadorId == Guid.Empty)
                return Results.Unauthorized();

            var operador = await db.Operadores.AsNoTracking().FirstOrDefaultAsync(o => o.Id == operadorId, ct);
            var identidad = await db.IdentidadesVisuales.AsNoTracking().FirstOrDefaultAsync(i => i.OperadorId == operadorId, ct);

            var nombreComercial = !string.IsNullOrWhiteSpace(operador?.NombreComercial)
                ? operador.NombreComercial
                : "Última Milla";

            var colorPrimarioHex = !string.IsNullOrWhiteSpace(identidad?.ColorPrimario)
                ? identidad.ColorPrimario.Trim()
                : "#2563EB";

            var colorSecundarioHex = !string.IsNullOrWhiteSpace(identidad?.ColorSecundario)
                ? identidad.ColorSecundario.Trim()
                : "#64748B";

            var logoUrl = identidad?.LogoUrl;
            var telefonoContacto = identidad?.TelefonoContacto;
            var emailContacto = identidad?.EmailContacto;

            var resultado = new MobileIdentidadVisualDto(
                OperadorId: operadorId,
                NombreComercial: nombreComercial,
                ColorPrimarioHex: colorPrimarioHex,
                ColorSecundarioHex: colorSecundarioHex,
                ColorPrimario: colorPrimarioHex,
                ColorSecundario: colorSecundarioHex,
                LogoUrl: logoUrl,
                TelefonoContacto: telefonoContacto,
                EmailContacto: emailContacto
            );

            return Results.Ok(resultado);
        })
        .WithName("ObtenerIdentidadVisualMobile")
        .WithSummary("Obtener la identidad visual y marca blanca del operador del usuario autenticado")
        .Produces<MobileIdentidadVisualDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        return app;
    }
}

public record MobileIdentidadVisualDto(
    Guid OperadorId,
    string NombreComercial,
    string ColorPrimarioHex,
    string ColorSecundarioHex,
    string ColorPrimario,
    string ColorSecundario,
    string? LogoUrl,
    string? TelefonoContacto,
    string? EmailContacto);

