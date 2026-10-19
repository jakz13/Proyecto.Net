using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using UltimaMilla.Api.Auth;
using UltimaMilla.Api.Endpoints;
using UltimaMilla.Application;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure;
using UltimaMilla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();

// Configuración de autenticación para desarrollo y demo móvil
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = DevMobileAuthHandler.SchemeName;
    options.DefaultChallengeScheme = DevMobileAuthHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, DevMobileAuthHandler>(DevMobileAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Última Milla · API de Envíos y Operación en Calle",
        Version = "v1",
        Description = "Servicio de alta, gestión operativa y sincronización de última milla para apps móviles y backoffice.",
        Contact = new OpenApiContact
        {
            Name = "Plataforma Última Milla"
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. En desarrollo se acepta cualquier token o se auto-autentica el repartidor demo.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
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
app.UseAuthentication();
app.UseAuthorization();

// Auto-migrate database on startup with resilience retry and idempotent seed data
await ApplyMigrationsAsync(app);

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
            request.ModalidadServicio ?? "Estandar",
            new DestinatarioAltaDto(request.NombreDestinatario, request.Documento, request.Email, request.Telefono),
            new DireccionAltaDto(
                request.Calle,
                request.Numero,
                request.Ciudad,
                request.ZonaCoberturaId,
                request.Latitud,
                request.Longitud,
                request.Referencia),
            request.Bultos?.Select(b => new BultoAltaDto(b.Peso, b.Alto, b.Ancho, b.Profundo)).ToList()
                ?? new List<BultoAltaDto>(),
            request.FranjaHorariaId);

        var resultado = await mediator.Send(command);
        return Results.Created($"/api/envios/{resultado.Id}", new
        {
            id = resultado.Id,
            codigoSeguimiento = resultado.CodigoSeguimiento,
            estado = resultado.Estado,
            tarifa = resultado.Tarifa,
            bultos = resultado.IdentificacionesBultos
        });
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

// Registrar endpoints de operación en calle (UC-18, UC-19, UC-20, UC-21)
app.MapMobileRutasEndpoints();

// Registrar endpoints de depósito (UC-14, UC-15)
app.MapDepositoEndpoints();

// Registrar endpoints de planificación (UC-08, UC-09)
app.MapPlanificacionEndpoints();

// =========================================================================
// Endpoint de Consulta para Seguimiento Público y Backoffice (Apoyo a Frontend)
// =========================================================================
app.MapGet("/api/seguimiento/{codigoSeguimiento}", async (
    string codigoSeguimiento,
    AppDbContext db,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(codigoSeguimiento))
        return Results.BadRequest(new { error = "El código de seguimiento no puede ser vacío." });

    var codigoNormalizado = codigoSeguimiento.Trim().ToUpperInvariant();

    var envio = await db.Envios
        .AsNoTracking()
        .Include(e => e.Bultos)
        .Include(e => e.Eventos)
        .Include(e => e.IntentosEntrega)
            .ThenInclude(i => i.PruebaDeEntrega)
        .FirstOrDefaultAsync(e =>
            e.CodigoSeguimientoPublico == codigoNormalizado ||
            e.Id.ToString() == codigoNormalizado,
            ct);

    if (envio is null)
    {
        return Results.NotFound(new
        {
            error = $"No se encontró ningún envío con el código de seguimiento '{codigoSeguimiento}'."
        });
    }

    var eventosOrdenados = envio.Eventos
        .OrderBy(ev => ev.Timestamp)
        .Select(ev => new
        {
            Fecha = ev.Timestamp,
            Estado = (int)ev.EstadoNuevo,
            EstadoNombre = ev.EstadoNuevo.ToString(),
            TipoEvento = ev.TipoEvento,
            DescripcionPublica = MapearDescripcionPublicaEvento(ev.TipoEvento, ev.EstadoNuevo),
            Origen = ev.Origen,
            Detalle = ev.Payload
        })
        .ToList();

    var intentos = envio.IntentosEntrega
        .OrderBy(i => i.Timestamp)
        .Select(i => new
        {
            Id = i.Id,
            Resultado = i.Resultado,
            Timestamp = i.Timestamp,
            OrigenDatos = i.OrigenDatos,
            TienePruebaEntrega = i.PruebaDeEntrega != null,
            NombreReceptor = i.PruebaDeEntrega?.NombreReceptor,
            DocumentoReceptor = i.PruebaDeEntrega?.DocumentoReceptor,
            TipoPrueba = i.PruebaDeEntrega?.Tipo,
            TieneFirma = !string.IsNullOrWhiteSpace(i.PruebaDeEntrega?.FirmaImagenUrl),
            TieneFoto = !string.IsNullOrWhiteSpace(i.PruebaDeEntrega?.FotoUrl),
            Latitud = i.Latitud,
            Longitud = i.Longitud
        })
        .ToList();

    var ultimoEvento = envio.Eventos.OrderByDescending(ev => ev.Timestamp).FirstOrDefault();

    var response = new
    {
        CodigoSeguimiento = envio.CodigoSeguimientoPublico,
        EnvioId = envio.Id,
        EstadoActual = (int)envio.EstadoActual,
        EstadoNombre = envio.EstadoActual.ToString(),
        FechaUltimaActualizacion = ultimoEvento?.Timestamp ?? envio.FechaAlta,
        DireccionDestino = envio.DireccionDestino,
        NombreDestinatario = envio.NombreDestinatario,
        CantidadBultos = envio.Bultos.Count,
        Bultos = envio.Bultos.Select(b => new { b.Id, b.Identificacion, b.Peso, b.EstadoBulto }).ToList(),
        CantidadIntentos = envio.CantidadIntentos,
        PermiteReprogramacion = envio.EstadoActual is EstadoEnvio.Reprogramado or EstadoEnvio.NoEntregado,
        HistorialEventos = eventosOrdenados,
        IntentosEntrega = intentos
    };

    return Results.Ok(response);
})
.AllowAnonymous()
.WithName("ConsultarSeguimientoPublico")
.WithSummary("Consulta pública y backoffice de seguimiento de envío")
.WithDescription("Permite consultar el estado actual en PostgreSQL, la línea de tiempo de eventos y los intentos/pruebas de entrega en vivo tras sincronización móvil.")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.WithOpenApi();

await app.RunAsync();

static async Task ApplyMigrationsAsync(WebApplication app)
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
            await db.Database.MigrateAsync();
            logger.LogInformation("Migraciones aplicadas exitosamente.");

            // Ejecución del seeder de datos idempotente para la demo móvil y web (22/10)
            await DatabaseSeeder.SeedAsync(db, logger);
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fallo al conectar, migrar o sembrar la base de datos. Reintentando en 3 segundos ({Retry}/{Max})...", retry, maxRetries);
            if (retry == maxRetries)
            {
                logger.LogError(ex, "No se pudo conectar a la base de datos tras {Max} intentos.", maxRetries);
                throw;
            }
            await Task.Delay(3000);
        }
    }
}

static string MapearDescripcionPublicaEvento(string tipoEvento, EstadoEnvio estadoNuevo) => tipoEvento switch
{
    "AltaEnvio" => "Envío registrado y admitido en el sistema",
    "AsignacionRuta" => "Envío asignado a hoja de ruta para distribución",
    "DespachoRuta" => "Vehículo despachado. Envío en tránsito hacia el domicilio",
    "EntregaExitosa" => "Envío entregado exitosamente al destinatario",
    "IntentoFallido" => "Intento de entrega no concretado en domicilio",
    "Reprogramacion" => "Envío reprogramado para nuevo intento de entrega",
    "Devolucion" => "Envío derivado a devolución al depósito central",
    _ => $"Actualización de estado: {estadoNuevo}"
};

public record CreateEnvioRequest(
    Guid? ComercioId,
    string NombreDestinatario,
    string Calle,
    string Numero,
    string Ciudad,
    List<CreateBultoRequest>? Bultos,
    string? ModalidadServicio = null,
    string? Documento = null,
    string? Email = null,
    string? Telefono = null,
    Guid? ZonaCoberturaId = null,
    double? Latitud = null,
    double? Longitud = null,
    string? Referencia = null,
    Guid? FranjaHorariaId = null
);

public record CreateBultoRequest(decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);

public partial class Program { }
