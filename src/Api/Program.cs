using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using UltimaMilla.Application;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Persistence;

using UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Última Milla · API de Envíos",
        Version = "v1",
        Description = "Servicio de alta y gestión operativa de órdenes de envío de última milla.",
        Contact = new OpenApiContact
        {
            Name = "Plataforma Última Milla"
        }
    });
});

// Register Clean Architecture layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS restringido a los orígenes de las aplicaciones web (configurable: Cors:AllowedOrigins)
var origenesPermitidos = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5002", "http://localhost:5003" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
// Swagger enabled in both Development and Production for intermediate milestone testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Última Milla v1");
    c.DocumentTitle = "Última Milla · API Docs";
    c.DocExpansion(DocExpansion.List);
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.InjectStylesheet("/swagger-custom.css");
});

app.UseCors();

// Auto-migrate database on startup with resilience retry
ApplyMigrations(app);

// Custom CSS for modern Swagger UI
app.MapGet("/swagger-custom.css", () =>
{
    var css = """
        body, .swagger-ui {
            font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif !important;
            color: #2b3035;
        }

        .swagger-ui .topbar {
            background-color: #212529 !important;
            padding: 12px 0 !important;
            border-bottom: 2px solid #343a40;
        }

        .swagger-ui .topbar a {
            max-width: none;
            display: flex;
            align-items: center;
            text-decoration: none;
        }

        .swagger-ui .topbar-wrapper img {
            display: none !important;
        }

        .swagger-ui .topbar-wrapper span {
            display: none !important;
        }

        .swagger-ui .topbar-wrapper a::before {
            content: "ÚLTIMA MILLA · API";
            color: #ffffff;
            font-weight: 700;
            font-size: 1.05rem;
            letter-spacing: 0.04em;
        }

        .swagger-ui .info {
            margin: 24px 0 18px 0 !important;
        }

        .swagger-ui .info .title {
            color: #1a1e21;
            font-size: 1.85rem;
            font-weight: 700;
        }

        .swagger-ui .info p {
            color: #555f6d;
            font-size: 0.95rem;
            margin-top: 4px;
        }

        .swagger-ui .opblock {
            border-radius: 8px !important;
            border-width: 1px !important;
            box-shadow: 0 1px 3px rgba(0,0,0,0.04) !important;
            margin-bottom: 12px !important;
        }

        .swagger-ui .opblock .opblock-summary {
            padding: 10px 16px !important;
        }

        .swagger-ui .opblock .opblock-summary-method {
            border-radius: 4px !important;
            font-weight: 700 !important;
            padding: 5px 12px !important;
            font-size: 0.85rem !important;
        }

        .swagger-ui .btn.try-out__btn {
            border-radius: 6px !important;
            font-weight: 600 !important;
            border: 1px solid #ced4da !important;
            background: #f8f9fa !important;
            color: #495057 !important;
        }

        .swagger-ui .btn.execute {
            background-color: #0d6efd !important;
            border-color: #0d6efd !important;
            border-radius: 6px !important;
            color: #fff !important;
            font-weight: 600 !important;
        }

        .swagger-ui .btn.execute:hover {
            background-color: #0b5ed7 !important;
        }

        .swagger-ui .wrapper .filter-wrapper input {
            border-radius: 6px !important;
            border: 1px solid #ced4da !important;
            padding: 8px 12px !important;
        }
        """;
    return Results.Content(css, "text/css");
}).ExcludeFromDescription();

// Minimal API Endpoints
var enviosApi = app.MapGroup("/api/envios")
    .WithTags("Envíos");

enviosApi.MapPost("/", async (CreateEnvioRequest request, IMediator mediator) =>
{
    try
    {
        var comercioId = request.ComercioId.HasValue && request.ComercioId.Value != Guid.Empty
            ? request.ComercioId.Value
            : Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new AltaEnvioCommand(
            comercioId,
            request.DireccionDestino,
            request.NombreDestinatario
        );

        var id = await mediator.Send(command);
        return Results.Created($"/api/envios/{id}", new { id });
    }
    catch (DomainValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("AltaEnvio")
.WithSummary("Registrar nuevo envío")
.WithDescription("Crea una orden de envío en estado Admitido para el comercio solicitante.")
.Produces(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.WithOpenApi();

enviosApi.MapGet("/", async (IMediator mediator) =>
{
    var envios = await mediator.Send(new ListarEnviosQuery());
    return Results.Ok(envios);
})
.WithName("ListarEnvios")
.WithSummary("Listar todos los envíos")
.WithDescription("Obtiene el listado completo de órdenes de envío registradas con su estado actual.")
.Produces<IEnumerable<EnvioDto>>(StatusCodes.Status200OK)
.WithOpenApi();

// Mobile Endpoints (Walking Skeleton & Future Sync)
var mobileApi = app.MapGroup("/api/mobile/v1");

mobileApi.MapGet("/ping", async (IMediator mediator) =>
{
    var status = await mediator.Send(new CheckMobileConnectivityQuery());
    return Results.Ok(status);
})
.WithName("CheckMobileConnectivity")
.WithOpenApi();

app.Run();

static void ApplyMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 10;
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            logger.LogInformation("Aplicando migraciones a la base de datos (intento {Retry}/{Max})...", retry, maxRetries);
            db.Database.Migrate();
            logger.LogInformation("Migraciones aplicadas exitosamente.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al conectar o migrar la base de datos. Reintentando en 3 segundos ({Retry}/{Max})...", retry, maxRetries);
            if (retry == maxRetries)
            {
                logger.LogError(ex, "No se pudo conectar a la base de datos tras {Max} intentos.", maxRetries);
                throw;
            }
            Thread.Sleep(3000);
        }
    }
}

public record CreateEnvioRequest(
    Guid? ComercioId,
    string DireccionDestino,
    string NombreDestinatario
);

public partial class Program { }
