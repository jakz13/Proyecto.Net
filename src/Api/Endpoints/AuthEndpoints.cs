using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Autenticación y Autorización (UC-40)");

        group.MapPost("/login", async (
            [FromBody] IniciarSesionRequest request,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new IniciarSesionCommand(request.Email, request.Password);
                var resultado = await mediator.Send(command, ct);
                return Results.Ok(resultado);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (CredencialesInvalidasException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (CuentaBloqueadaException ex)
            {
                return Results.Json(new { error = ex.Message, bloqueadoHasta = ex.BloqueadoHasta }, statusCode: StatusCodes.Status423Locked);
            }
            catch (CuentaInactivaException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .AllowAnonymous()
        .WithName("IniciarSesion")
        .WithSummary("Iniciar sesión y obtener token JWT (UC-40)")
        .WithDescription("Valida las credenciales del usuario, previene fuerza bruta bloqueando temporalmente tras reiterados fallos, y emite el token JWT con su perfil, operador y comercio asignado.")
        .Produces<IniciarSesionResultadoDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status423Locked)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}
