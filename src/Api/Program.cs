using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using UltimaMilla.Api.Auth;
using UltimaMilla.Api.Backoffice;
using UltimaMilla.Api.Endpoints;
using UltimaMilla.Api.Health;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Api.Services;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Application.Queries.Mobile.CheckMobileConnectivity;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using UltimaMilla.Api.Observabilidad;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logs estructurados en JSON compacto con Serilog (UC-44, §6.12)
builder.Host.UseSerilog((contexto, servicios, configuracion) =>
{
    configuracion
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With(new EnriquecedorObservabilidad("ultimamilla-api"))
        .WriteTo.Console(new CompactJsonFormatter());
});

// Métricas y trazas distribuidas con OpenTelemetry (UC-44, §6.12)
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(recurso => recurso.AddService("ultimamilla-api"))
    .WithMetrics(metricas =>
    {
        metricas
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(MetricasNegocio.NombreMeter)
            .AddPrometheusExporter();
    })
    .WithTracing(trazas =>
    {
        trazas
            .AddSource(ContextoCorrelacion.FuenteActividades.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            trazas.AddOtlpExporter();
        }
    });

// El validador de tokens usa la misma clave configurada (Jwt:SecretKey) con la que se firman al iniciar sesión
UltimaMilla.Infrastructure.Services.TokenJwtService.InicializarClaveGlobal(builder.Configuration);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

// Configuración de autenticación para desarrollo y demo móvil
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = DevMobileAuthHandler.SchemeName;
    options.DefaultChallengeScheme = DevMobileAuthHandler.SchemeName;
})
.AddScheme<AuthenticationSchemeOptions, DevMobileAuthHandler>(DevMobileAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(UltimaMilla.Api.Auth.PoliticasApi.Configurar);

// Límite de tasa para la consulta pública de seguimiento (evita la enumeración de códigos y el abuso)
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opciones.AddPolicy(ConsultasEnvioEndpoints.PoliticaLimiteSeguimiento, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Inicio de sesión: se acota por origen para frenar el barrido de usuarios y contraseñas (el bloqueo por cuenta, aparte, protege a cada
    // cuenta pero permite bloquear cuentas ajenas a propósito). El tope por minuto se puede ajustar con RateLimit:LoginPorMinuto.
    var loginPorMinuto = builder.Configuration.GetValue("RateLimit:LoginPorMinuto", 10);
    opciones.AddPolicy(AuthEndpoints.PoliticaLimiteLogin, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            "login:" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida"),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // La integración por API es de mayor volumen que la consulta pública, pero también se acota por origen
    // Limitación diferenciada: cada clave de producción tiene su propio cupo (120/min), la clave de pruebas uno menor (20/min)
    // y quien no presenta clave se acota por IP (30/min) para frenar el intento de adivinarlas.
    opciones.AddPolicy(AltaEnvioCanalesEndpoints.PoliticaLimiteIntegracion, contexto =>
    {
        var clave = contexto.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(clave))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                "ip:" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida"),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
        }

        var huella = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(clave.Trim())))[..16];
        var esPruebas = clave.StartsWith(UltimaMilla.Application.Ports.IClavesApiPruebas.Prefijo, StringComparison.Ordinal);
        return RateLimitPartition.GetFixedWindowLimiter(
            (esPruebas ? "pruebas:" : "clave:") + huella,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = esPruebas ? 20 : 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

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

    // Documentación navegable de la API pública para comercios (versionada: v1), separada de la API interna
    c.SwaggerDoc("integracion", new OpenApiInfo
    {
        Title = "Última Milla · API pública para comercios",
        Version = "v1",
        Description = "Integración del sistema del comercio con el operador logístico.\n\n" +
                      "**Autenticación:** cabecera `X-Api-Key` con la clave del comercio (se genera en el portal del comercio y se muestra una sola vez).\n\n" +
                      "**Ambiente de pruebas:** la clave de pruebas (`GET /api/comercio/clave-api-pruebas`) valida el pedido y calcula la tarifa real, pero no registra ni publica nada; la respuesta trae `sandbox=true`.\n\n" +
                      "**Limitación de tasa (por clave):** 120 solicitudes por minuto con la clave de producción, 20 con la de pruebas y 30 por IP sin clave. Al superarla se responde 429.\n\n" +
                      "**Idempotencia:** con `referenciaExterna`, reintentar devuelve el envío ya admitido (200) en lugar de crear otro.\n\n" +
                      "**Versionado:** la ruta incluye la versión (`/api/integracion/v1/...`); los cambios que rompen compatibilidad se publican como v2 sin retirar v1."
    });

    c.DocInclusionPredicate((documento, api) =>
        documento != "integracion"
        || (api.RelativePath?.StartsWith("api/integracion/", StringComparison.Ordinal) ?? false)
        || api.RelativePath == "api/comercio/clave-api-pruebas");

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "Clave de API del comercio (o la clave de pruebas) en la cabecera X-Api-Key.",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
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

// SignalR para el tablero en tiempo real con soporte opcional de Redis backplane (Fase 2)
var signalRBuilder = builder.Services.AddSignalR();
var redisBackplane = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(redisBackplane))
{
    signalRBuilder.AddStackExchangeRedis(redisBackplane, opciones =>
    {
        opciones.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("UltimaMilla.Tablero");
        opciones.Configuration.AbortOnConnectFail = false;
        // Con Redis caído, emitir al tablero no debe retener la solicitud: la espera por defecto (5 s por mensaje) se acota a 1 s
        opciones.Configuration.ConnectTimeout = 1000;
        opciones.Configuration.SyncTimeout = 1000;
        opciones.Configuration.AsyncTimeout = 1000;
    });
}

builder.Services.AddScoped<ITableroRealtimeNotifier, SignalRTableroNotifier>();

// Puntos de verificación de salud para el balanceador: /live (proceso), /ready (base de datos) y /health (todo)
builder.Services.AddHealthChecks()
    .AddCheck<BaseDatosHealthCheck>("postgres", tags: new[] { "ready" });

// Apagado controlado: da tiempo a terminar las solicitudes en curso cuando el contenedor recibe SIGTERM
builder.Services.Configure<HostOptions>(opciones => opciones.ShutdownTimeout = TimeSpan.FromSeconds(30));

// CORS restringido a los orígenes de las aplicaciones web (configurable: Cors:AllowedOrigins)
var origenesPermitidos = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5002", "http://localhost:5003", "http://localhost:5004", "https://localhost:7002", "https://localhost:7003", "https://localhost:7004" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Inicializar instrumentos del Meter UltimaMilla para exponer contadores y gauges desde el arranque
_ = app.Services.GetRequiredService<IMetricasNegocio>();

app.UseMiddleware<CorrelationIdMiddleware>();

// Una línea de log por solicitud (método, ruta, código y duración) con el CorrelationId del middleware anterior.
// Las rutas de salud y de métricas se registran en nivel Verbose para no llenar el log con los chequeos periódicos.
// Errores no controlados: nunca se devuelve el detalle de la excepción (rutas, SQL, pila). Si la base de datos no responde se
// contesta 503 con Retry-After (degradación esperada, el cliente reintenta); cualquier otro fallo es un 500 genérico con el
// identificador de traza para buscarlo en los logs.
app.UseExceptionHandler(manejador => manejador.Run(async contexto =>
{
    var error = contexto.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var baseNoDisponible = false;
    for (var e = error; e is not null; e = e.InnerException)
    {
        if (e is Npgsql.NpgsqlException || e is System.Net.Sockets.SocketException || e is TimeoutException)
        {
            baseNoDisponible = true;
            break;
        }
    }

    contexto.Response.ContentType = "application/json";
    if (baseNoDisponible)
    {
        contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        contexto.Response.Headers.RetryAfter = "5";
        await contexto.Response.WriteAsJsonAsync(new { error = "El servicio no está disponible temporalmente. Reintente en unos segundos." });
        return;
    }

    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await contexto.Response.WriteAsJsonAsync(new { error = "Ocurrió un error inesperado.", traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? contexto.TraceIdentifier });
}));

app.UseSerilogRequestLogging(opciones =>
{
    opciones.GetLevel = (contexto, _, excepcion) =>
    {
        var ruta = contexto.Request.Path.Value ?? string.Empty;
        var esRutaTecnica = ruta.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWith("/ready", StringComparison.OrdinalIgnoreCase)
            || ruta.StartsWith("/live", StringComparison.OrdinalIgnoreCase);
        if (esRutaTecnica && excepcion is null && contexto.Response.StatusCode < 500)
            return LogEventLevel.Verbose;
        return contexto.Response.StatusCode >= 500 || excepcion is not null ? LogEventLevel.Error : LogEventLevel.Information;
    };
});

// Configure the HTTP request pipeline
// La documentación pública (API de integración para comercios) se publica siempre; el contrato completo de la API interna
// (todos los endpoints del backoffice, administración y móvil) solo en desarrollo: en un servidor es un mapa para quien ataca.
var documentarApiInterna = app.Environment.IsDevelopment();
if (!documentarApiInterna)
{
    app.Use(async (contexto, siguiente) =>
    {
        if (contexto.Request.Path.StartsWithSegments("/swagger/v1"))
        {
            contexto.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await siguiente();
    });
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/integracion/swagger.json", "API pública para comercios (v1)");
    if (documentarApiInterna)
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API completa (interna)");
    c.DocumentTitle = "Última Milla · API Docs";
    c.DocExpansion(DocExpansion.List);
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.InjectStylesheet("/swagger-custom.css");
});

// Cabeceras HTTP de seguridad OWASP y HSTS (Fase 4)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Configuration.GetValue<bool>("Security:EnforceHttps"))
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    if (!context.Request.Path.StartsWithSegments("/swagger"))
    {
        headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
    }

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Auto-migrate database on startup with resilience retry and idempotent seed data
await ApplyMigrationsAsync(app);

app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = verificacion => verificacion.Tags.Contains("ready") });
app.MapHealthChecks("/health");

// Endpoint de métricas para Prometheus (UC-44, §6.12)
app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();

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
    .WithTags("Envíos")
    .RequireAuthorization();

enviosApi.MapPost("/", async (CreateEnvioRequest request, IMediator mediator) =>
{
    try
    {
        // El comercio real se toma de la sesión en el handler; este valor solo se usa si el usuario no pertenece a un comercio
        var comercioId = request.ComercioId.HasValue && request.ComercioId.Value != Guid.Empty
            ? request.ComercioId.Value
            : Guid.Empty;

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
    catch (UltimaMilla.Application.Exceptions.ConflictoDeConcurrenciaException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
})
.WithName("AltaEnvio")
.RequireAuthorization(PoliticasApi.Comercio)
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
.RequireAuthorization()
.WithName("CheckMobileConnectivity")
.WithOpenApi();

// Registrar endpoints de autenticación y autorización por token (UC-40)
app.MapAuthEndpoints();

// Registrar endpoints de operación en calle (UC-18, UC-19, UC-20, UC-21)
app.MapMobileRutasEndpoints();

// Posición del vehículo durante la jornada (RF-23)
app.MapMobilePosicionEndpoints();

// Tablero de operación en vivo con posiciones GPS de la app (RF-23, RF-29)
app.MapTableroEnVivoEndpoints();

// Reportes de gestión (RF-30)
app.MapReportesEndpoints();

// Liquidaciones a comercios y cuenta corriente (RF-30, RF-34)
app.MapLiquidacionesEndpoints();

// Registrar endpoints de depósito (UC-14, UC-15)
app.MapDepositoEndpoints();

// Registrar endpoints de planificación (UC-08, UC-09)
app.MapPlanificacionEndpoints();

// Registrar tablero de operacion en vivo (UC-12)
app.MapTableroEndpoints();

// Registrar consultas de envío: seguimiento público (UC-33) y detalle para Backoffice
app.MapConsultasEnvioEndpoints();

// Registrar gestión de incidencias (UC-11)
app.MapIncidenciasEndpoints();

// Registrar reglas operativas y motivos de no entrega (UC-04, UC-05)
app.MapOperadorConfiguracionEndpoints();

// Retiro en domicilio (cambio de requerimientos 2.2): portal, API por clave, backoffice y repartidor
app.MapRetirosEndpoints();

// Banderas de funcionalidad por operador (activables por el administrador de la plataforma)
app.MapFuncionalidadesEndpoints();

// Registrar canales de alta: archivo masivo (UC-25) y API de integración (UC-26)
app.MapAltaEnvioCanalesEndpoints();
app.MapAvisosComercioEndpoints();

// Administración del operador para el Backoffice (comercios, usuarios, zonas, tarifas, reglas, motivos, identidad, flota y mensajería)
app.MapComerciosBackofficeEndpoints();
app.MapComercioOperadoresEndpoints();
app.MapUsuariosBackofficeEndpoints();
app.MapZonasCoberturaEndpoints();
app.MapCuadroTarifarioEndpoints();
app.MapIdentidadVisualEndpoints();
app.MapFlotaEndpoints();
app.MapMensajeriaEndpoints();
app.MapReasignacionesEndpoints();
app.MapWorkerProcesosEndpoints();
app.MapHub<TableroHub>("/hubs/tablero");

// Tiempo real del seguimiento público (anónimo): comparte el límite de tasa por IP de la consulta pública
app.MapHub<SeguimientoHub>("/hubs/seguimiento").RequireRateLimiting(ConsultasEnvioEndpoints.PoliticaLimiteSeguimiento);

// Hub de SignalR para streaming de telemetría de operaciones y seguimiento de flota (Hito 4 / RNF 6.10)
app.MapHub<OperacionHub>("/hubs/operacion");

await app.RunAsync();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    if (app.Configuration.GetValue<bool>("Database:SkipMigrationsOnStartup"))
    {
        return;
    }

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 10;
    for (int retry = 1; retry <= maxRetries; retry++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
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
