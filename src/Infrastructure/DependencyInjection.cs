using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Ports;
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
        services.AddScoped<IVehiculoRepository, EfVehiculoRepository>();
        services.AddScoped<IFranjaHorariaRepository, EfFranjaHorariaRepository>();
        services.AddScoped<ICuadroTarifarioRepository, EfCuadroTarifarioRepository>();
        services.AddScoped<IDestinatarioRepository, EfDestinatarioRepository>();
        services.AddScoped<IDireccionRepository, EfDireccionRepository>();
        services.AddScoped<IComercioRepository, EfComercioRepository>();
        services.AddScoped<IComercioOperadorRepository, EfComercioOperadorRepository>();
        services.AddScoped<ILoteImportacionRepository, EfLoteImportacionRepository>();
        services.AddScoped<ISolicitudReprogramacionRepository, EfSolicitudReprogramacionRepository>();
        services.AddScoped<IOperadorRepository, EfOperadorRepository>();
        services.AddScoped<ISuscripcionAvisoRepository, EfSuscripcionAvisoRepository>();
        services.AddScoped<IAvisoComercioRepository, EfAvisoComercioRepository>();
        services.AddScoped<IIncidenciaRepository, EfIncidenciaRepository>();
        services.AddScoped<IUsuarioRepository, EfUsuarioRepository>();
        services.AddScoped<IReglaOperativaRepository, EfReglaOperativaRepository>();
        services.AddScoped<IMotivoNoEntregaRepository, EfMotivoNoEntregaRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IOutboxStorage, EfOutboxStorage>();

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
