using MediatR;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Clean Architecture layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(builder.Configuration);

// CORS for local development and container networking
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
// Swagger enabled in both Development and Production for intermediate milestone testing
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Auto-migrate database on startup with resilience retry
ApplyMigrations(app);

// Minimal API Endpoints
var enviosApi = app.MapGroup("/api/envios");

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
.WithOpenApi();

enviosApi.MapGet("/", async (IMediator mediator) =>
{
    var envios = await mediator.Send(new ListarEnviosQuery());
    return Results.Ok(envios);
})
.WithName("ListarEnvios")
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
