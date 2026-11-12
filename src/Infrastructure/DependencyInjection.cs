using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Messaging;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Repositories;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("PostgresConnection")
            ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no fue encontrada en la configuración.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddHttpContextAccessor();

        // Puertos de persistencia y dominio
        services.AddScoped<IEnvioRepository, EfEnvioRepository>();
        services.AddScoped<IRetiroRepository, EfRetiroRepository>();
        services.AddScoped<IFuncionalidadesService, FuncionalidadesService>();
        services.AddScoped<IRutaRepository, EfRutaRepository>();
        services.AddScoped<IRendicionRepository, EfRendicionRepository>();
        services.AddScoped<IDevolucionRepository, EfDevolucionRepository>();
        services.AddScoped<IRepartidorRepository, EfRepartidorRepository>();
        services.AddScoped<IPosicionVehiculoRepository, EfPosicionVehiculoRepository>();
        services.AddScoped<IVehiculoRepository, EfVehiculoRepository>();
        services.AddScoped<IFranjaHorariaRepository, EfFranjaHorariaRepository>();
        services.AddScoped<IZonaCoberturaRepository, EfZonaCoberturaRepository>();
        services.AddScoped<ICuadroTarifarioRepository, EfCuadroTarifarioRepository>();
        services.AddScoped<IDestinatarioRepository, EfDestinatarioRepository>();
        services.AddScoped<IDireccionRepository, EfDireccionRepository>();
        services.AddScoped<IComercioRepository, EfComercioRepository>();
        services.AddScoped<IComercioOperadorRepository, EfComercioOperadorRepository>();
        services.AddScoped<ILoteImportacionRepository, EfLoteImportacionRepository>();
        services.AddScoped<ISolicitudReprogramacionRepository, EfSolicitudReprogramacionRepository>();
        services.AddScoped<IOperadorRepository, EfOperadorRepository>();
        services.AddScoped<ISuscripcionAvisoRepository, EfSuscripcionAvisoRepository>();
        services.AddScoped<IAvisoComercioConsultaRepository, EfAvisoComercioConsultaRepository>();
        services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();
        services.AddScoped<IUsuarioRepository, EfUsuarioRepository>();
        services.AddScoped<IReglaOperativaRepository, EfReglaOperativaRepository>();
        services.AddScoped<IMotivoNoEntregaRepository, EfMotivoNoEntregaRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IOutboxStorage, EfOutboxStorage>();

        // Mensajería asíncrona y procesos en segundo plano (los consume el Worker; la API solo escribe en el outbox)
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        services.AddScoped<IIdempotenciaConsumidorRepository, EfIdempotenciaConsumidorRepository>();
        services.AddScoped<IMensajeFallidoRepository, EfMensajeFallidoRepository>();
        services.AddScoped<INotificacionRepository, EfNotificacionRepository>();
        services.AddScoped<IAvisoComercioRepository, EfAvisoComercioRepository>();
        services.AddScoped<IIndicadorCumplimientoRepository, EfIndicadorCumplimientoRepository>();
        services.AddScoped<ICierreDiarioRepository, EfCierreDiarioRepository>();
        services.AddScoped<IProcesosProgramadosRepository, EfProcesosProgramadosRepository>();

        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.Seccion));
        services.Configure<NotificacionesCorreoOptions>(configuration.GetSection(NotificacionesCorreoOptions.Seccion));
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddScoped<RegistroNotificacionDestinatarioService>();
        services.AddScoped<INotificacionDestinatarioService>(sp => sp.GetRequiredService<RegistroNotificacionDestinatarioService>());
        // Verificación de cobertura: la dirección de cada envío se ubica con Nominatim y debe caer en una zona del operador
        services.AddSingleton(new UltimaMilla.Application.Services.OpcionesCobertura { Validar = configuration.GetValue("Cobertura:Validar", true) });
        services.AddMemoryCache();
        services.AddHttpClient<IGeocodificador, NominatimGeocodificador>(cliente =>
        {
            cliente.BaseAddress = new Uri(configuration["Cobertura:UrlGeocodificador"] ?? "https://nominatim.openstreetmap.org/");
            cliente.Timeout = TimeSpan.FromSeconds(15);
            cliente.DefaultRequestHeaders.UserAgent.ParseAdd("UltimaMilla-Geocodificador/1.0");
        });
        services.AddHttpClient<IWebhookSender, HttpWebhookSender>(cliente => cliente.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                // Anti-SSRF: los avisos solo salen hacia Internet público (ver DestinoWebhookSeguro). Las excepciones son explícitas.
                var permitirInternos = configuration.GetValue<bool>("Avisos:PermitirDestinosInternos");
                var hosts = (configuration["Avisos:HostsPermitidos"] ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = DestinoWebhookSeguro.CrearConexion(permitirInternos, hosts)
                };
            });

        // Observabilidad: métricas de negocio expuestas a Prometheus
        services.AddSingleton<IMetricasNegocio, MetricasNegocio>();

        // Servicios transversales y caché distribuido (Redis en despliegue, memoria compartida en pruebas sin Redis)
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(opciones =>
            {
                var configRedis = StackExchange.Redis.ConfigurationOptions.Parse(redisConnection);
                configRedis.AbortOnConnectFail = false;
                configRedis.ConnectTimeout = 1000;
                configRedis.SyncTimeout = 1000;
                configRedis.AsyncTimeout = 1000;
                configRedis.ConnectRetry = 1;
                opciones.ConfigurationOptions = configRedis;
                opciones.InstanceName = "UltimaMilla:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddSingleton<CortacircuitosCache>();
        services.AddScoped<IDistributedCacheService, DistributedCacheService>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IClavesApiPruebas, ClavesApiPruebas>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITokenJwtService, TokenJwtService>();

        return services;
    }
}
