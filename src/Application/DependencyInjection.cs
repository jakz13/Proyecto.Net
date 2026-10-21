using Microsoft.Extensions.DependencyInjection;

namespace UltimaMilla.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddScoped<Commands.AltaEnvio.AltaEnvioProcesador>();

        return services;
    }
}
