using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Acceso a suscripciones de avisos automáticos y webhooks de los comercios.
/// </summary>
public interface ISuscripcionAvisoRepository
{
    Task<IReadOnlyList<SuscripcionAviso>> ListarPorComercioOperadorAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default);
    Task<SuscripcionAviso?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default);
    Task RemoveRangeAsync(IEnumerable<SuscripcionAviso> suscripciones, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registro, auditoría y reintento de avisos enviados a los webhooks de los comercios.
/// </summary>
public interface IAvisoComercioConsultaRepository
{
    Task<IReadOnlyList<AvisoComercio>> ListarRecientesPorComercioAsync(Guid comercioOperadorId, int limite = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvisoComercio>> ListarHistorialPorComercioAsync(Guid comercioOperadorId, string? estado = null, Guid? envioId = null, CancellationToken cancellationToken = default);
    Task<AvisoComercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default);
    Task UpdateAsync(AvisoComercio aviso, CancellationToken cancellationToken = default);
}
