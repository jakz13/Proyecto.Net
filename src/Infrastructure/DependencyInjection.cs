using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
using UltimaMilla.Infrastructure.Messaging;
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
        services.AddScoped<IRutaRepository, EfRutaRepository>();
        services.AddScoped<IRendicionRepository, EfRendicionRepository>();
        services.AddScoped<IDevolucionRepository, EfDevolucionRepository>();
        services.AddScoped<IRepartidorRepository, EfRepartidorRepository>();
        services.AddScoped<IPosicionVehiculoRepository, EfPosicionVehiculoRepository>();
        services.AddScoped<IVehiculoRepository, EfVehiculoRepository>();
        services.AddScoped<IFranjaHorariaRepository, EfFranjaHorariaRepository>();
        services.AddScoped<ICuadroTarifarioRepository, EfCuadroTarifarioRepository>();
        services.AddScoped<IDestinatarioRepository, EfDestinatarioRepository>();
        services.AddScoped<IDireccionRepository, EfDireccionRepository>();
        services.AddScoped<IComercioOperadorRepository, EfComercioOperadorRepository>();
        services.AddScoped<ILoteImportacionRepository, EfLoteImportacionRepository>();
        services.AddScoped<ISolicitudReprogramacionRepository, EfSolicitudReprogramacionRepository>();
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
        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();
        services.AddScoped<INotificacionDestinatarioService, RegistroNotificacionDestinatarioService>();
        services.AddHttpClient<IWebhookSender, HttpWebhookSender>(cliente => cliente.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        // Servicios transversales
        services.AddDistributedMemoryCache();
        services.AddScoped<IDistributedCacheService, DistributedCacheService>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<ITokenJwtService, TokenJwtService>();

        return services;
    }
}
