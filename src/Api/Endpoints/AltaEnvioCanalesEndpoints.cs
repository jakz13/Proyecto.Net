using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Commands.Comercio.ImportarEnvios;
using UltimaMilla.Application.Commands.Comercio.RegenerarClaveApi;
using UltimaMilla.Application.Commands.Integracion.AltaEnvioPorApi;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api.Endpoints;

/// <summary>Canales de alta distintos del portal: archivo masivo (UC-25) e integración por API (UC-26).</summary>
public static class AltaEnvioCanalesEndpoints
{
    public const string PoliticaLimiteIntegracion = "integracion-api";
    private const string CabeceraClaveApi = "X-Api-Key";
    private const long TamanoMaximoArchivo = 2 * 1024 * 1024;

    public static IEndpointRouteBuilder MapAltaEnvioCanalesEndpoints(this IEndpointRouteBuilder app)
    {
        // UC-26: autenticado por clave de API del comercio, no por sesión de usuario
        app.MapPost("/api/integracion/v1/envios", async (
            [FromBody] AltaEnvioApiRequest request,
            HttpRequest http,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                var command = new AltaEnvioPorApiCommand(
                    http.Headers[CabeceraClaveApi].FirstOrDefault(),
                    request.ModalidadServicio ?? "Estandar",
                    new DestinatarioAltaDto(request.NombreDestinatario ?? string.Empty, request.Documento, request.Email, request.Telefono),
                    new DireccionAltaDto(
                        request.Calle ?? string.Empty,
                        request.Numero ?? string.Empty,
                        request.Ciudad ?? string.Empty,
                        request.ZonaCoberturaId,
                        request.Latitud,
                        request.Longitud,
                        request.Referencia),
                    request.Bultos?.Select(b => new BultoAltaDto(b.Peso, b.Alto, b.Ancho, b.Profundo)).ToList() ?? new List<BultoAltaDto>(),
                    request.FranjaHorariaId,
                    request.ReferenciaExterna);

                var resultado = await mediator.Send(command, ct);
                var cuerpo = new
                {
                    id = resultado.Id,
                    codigoSeguimiento = resultado.CodigoSeguimiento,
                    estado = resultado.Estado,
                    tarifa = resultado.Tarifa,
                    bultos = resultado.IdentificacionesBultos,
                    duplicado = resultado.Duplicado
                };

                // Un reintento con la misma referencia devuelve el envío ya admitido
                return resultado.Duplicado ? Results.Ok(cuerpo) : Results.Created($"/api/envios/{resultado.Id}", cuerpo);
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ConflictoDeConcurrenciaException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = "Clave de API inválida." }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticaLimiteIntegracion)
        .WithTags("Integración por API (UC-26)")
        .WithName("AltaEnvioPorApi")
        .WithSummary("Alta de un envío desde el sistema del comercio")
        .WithDescription("Se autentica con la cabecera X-Api-Key. Si se informa referenciaExterna, reintentar con la misma referencia devuelve el envío ya admitido (200) en vez de crear otro.")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status429TooManyRequests)
        .WithOpenApi();

        // UC-25: el comercio carga un archivo CSV con muchos envíos
        app.MapPost("/api/comercio/envios/importacion", async (
            IFormFile archivo,
            [FromForm] string? claveIdempotencia,
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                if (archivo is null || archivo.Length == 0)
                    throw new DomainValidationException("Adjunte un archivo CSV con los envíos.");

                if (archivo.Length > TamanoMaximoArchivo)
                    throw new DomainValidationException("El archivo supera el tamaño máximo de 2 MB.");

                using var lector = new StreamReader(archivo.OpenReadStream(), Encoding.UTF8);
                var contenido = await lector.ReadToEndAsync(ct);

                // Sin clave explícita, el lote se identifica por el contenido: reimportar el mismo archivo no duplica
                var clave = string.IsNullOrWhiteSpace(claveIdempotencia)
                    ? "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)))
                    : claveIdempotencia;

                var filas = CsvEnviosParser.Leer(contenido);
                return Results.Ok(await mediator.Send(new ImportarEnviosCommand(clave, filas), ct));
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (ConflictoDeConcurrenciaException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .WithTags("Alta masiva (UC-25)")
        .WithName("ImportarEnvios")
        .WithSummary("Importar envíos desde un archivo CSV")
        .WithDescription("Columnas: referencia, modalidad, destinatario, calle, numero, ciudad, peso, alto, ancho, profundo (opcionales: documento, email, telefono). Informa qué registros se procesaron y cuáles tuvieron error. Reimportar el mismo lote no genera envíos duplicados.")
        .Produces<ResultadoImportacionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        // Credenciales de la integración (UC-26): el comercio genera su clave y ve el valor una sola vez
        app.MapPost("/api/comercio/clave-api", async (
            [FromServices] IMediator mediator,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await mediator.Send(new RegenerarClaveApiCommand(), ct));
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        })
        .RequireAuthorization()
        .WithTags("Integración por API (UC-26)")
        .WithName("RegenerarClaveApi")
        .WithSummary("Generar una nueva clave de API para el comercio")
        .Produces<ClaveApiGeneradaDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status403Forbidden)
        .WithOpenApi();

        return app;
    }
}

public record AltaEnvioApiRequest(
    string? ModalidadServicio,
    string? NombreDestinatario,
    string? Documento,
    string? Email,
    string? Telefono,
    string? Calle,
    string? Numero,
    string? Ciudad,
    Guid? ZonaCoberturaId,
    double? Latitud,
    double? Longitud,
    string? Referencia,
    List<BultoApiRequest>? Bultos,
    Guid? FranjaHorariaId,
    string? ReferenciaExterna);

public record BultoApiRequest(decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);
