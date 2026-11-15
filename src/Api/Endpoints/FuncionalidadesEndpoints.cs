using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Api.Auth;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Banderas de funcionalidad por operador. Cualquier cliente autenticado consulta las de su operador; solo el administrador de la
/// plataforma (quien ofrece el servicio) las cambia, y cada cambio queda registrado con quién y cuándo.
/// </summary>
public static class FuncionalidadesEndpoints
{
    /// <summary>Evento del hub de tablero: una bandera de funcionalidad del operador cambió.</summary>
    public const string EventoFuncionalidadesCambiaron = "FuncionalidadesCambiaron";

    public static IEndpointRouteBuilder MapFuncionalidadesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/funcionalidades", async (
            [FromServices] IFuncionalidadesService funcionalidades,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) => Results.Ok(await funcionalidades.ObtenerDelOperadorAsync(tenant.OperadorId, ct)))
        .RequireAuthorization()
        .WithTags("Funcionalidades por operador")
        .WithName("ObtenerFuncionalidadesDelOperador")
        .WithSummary("Funcionalidades habilitadas para el operador del usuario (la app móvil y las web ocultan lo deshabilitado)")
        .WithOpenApi();

        var plataforma = app.MapGroup("/api/plataforma/operadores")
            .WithTags("Plataforma · Funcionalidades por operador")
            .RequireAuthorization(PoliticasApi.Plataforma);

        plataforma.MapGet("/", async (AppDbContext db, [FromServices] IFuncionalidadesService funcionalidades, CancellationToken ct) =>
        {
            var operadores = await db.Operadores.IgnoreQueryFilters().AsNoTracking().OrderBy(o => o.NombreComercial).ToListAsync(ct);
            var filas = await db.BanderasFuncionalidad.AsNoTracking().ToListAsync(ct);

            return Results.Ok(operadores.Select(o => new
            {
                operadorId = o.Id,
                nombre = o.NombreComercial,
                funcionalidades = Funcionalidad.Catalogo.Select(c =>
                {
                    var fila = filas.FirstOrDefault(f => f.OperadorId == o.Id && f.Clave == c.Key);
                    return new
                    {
                        clave = c.Key,
                        nombre = c.Value,
                        habilitada = fila?.Habilitada ?? true,
                        modificadaPor = fila?.ModificadaPorNombre,
                        modificadaEn = fila?.ModificadaEn
                    };
                })
            }));
        })
        .WithName("ListarFuncionalidadesPorOperador")
        .WithOpenApi();

        plataforma.MapPut("/{operadorId:guid}/funcionalidades/{clave}", async (
            [FromRoute] Guid operadorId,
            [FromRoute] string clave,
            [FromBody] CambiarFuncionalidadRequest request,
            HttpContext http,
            [FromServices] IFuncionalidadesService funcionalidades,
            [FromServices] ICurrentTenant tenant,
            [FromServices] IHubContext<TableroHub> hub,
            CancellationToken ct) =>
        {
            try
            {
                var nombre = http.User.FindFirstValue(ClaimTypes.Name) ?? http.User.FindFirstValue("email");
                var cambio = await funcionalidades.EstablecerAsync(operadorId, clave, request.Habilitada, tenant.UsuarioId, nombre, ct);

                // Las sesiones abiertas del operador afectado (personal y comercios) se enteran en el momento: aviso visible y pantalla bloqueada o liberada
                if (cambio)
                {
                    try
                    {
                        await hub.Clients.Group(TableroHub.GrupoIdentidadOperador(operadorId)).SendAsync(
                            EventoFuncionalidadesCambiaron,
                            new
                            {
                                clave,
                                nombre = Funcionalidad.Catalogo.GetValueOrDefault(clave, clave),
                                habilitada = request.Habilitada,
                                timestamp = DateTime.UtcNow
                            },
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Un fallo del transporte no deshace un cambio que ya se guardó: las pantallas lo verán en la próxima consulta
                    }
                }

                return Results.Ok(new { operadorId, clave, habilitada = request.Habilitada, cambio });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CambiarFuncionalidadDelOperador")
        .WithSummary("Activa o desactiva una funcionalidad para un operador, sin reiniciar nada")
        .WithOpenApi();

        plataforma.MapGet("/{operadorId:guid}/funcionalidades/historial", async (
            [FromRoute] Guid operadorId,
            [FromServices] IFuncionalidadesService funcionalidades,
            CancellationToken ct) => Results.Ok(await funcionalidades.HistorialAsync(operadorId, ct)))
        .WithName("HistorialFuncionalidadesDelOperador")
        .WithOpenApi();

        return app;
    }
}

public record CambiarFuncionalidadRequest(bool Habilitada);
