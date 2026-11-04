using Microsoft.Extensions.Diagnostics.HealthChecks;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Health;

/// <summary>
/// Verifica que la instancia pueda hablar con PostgreSQL. Se usa en /ready para que el balanceador
/// deje de enviar tráfico a una instancia que perdió la conexión con la base.
/// </summary>
public sealed class BaseDatosHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public BaseDatosHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("No se pudo conectar con PostgreSQL.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error al consultar PostgreSQL.", ex);
        }
    }
}
