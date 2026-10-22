using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

public interface IMotivoNoEntregaRepository
{
    Task AddAsync(MotivoNoEntrega motivo, CancellationToken cancellationToken = default);
    Task<MotivoNoEntrega?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Indica si el operador ya tiene un motivo con ese código (comparado en mayúsculas).</summary>
    Task<bool> ExisteCodigoAsync(Guid operadorId, string codigo, CancellationToken cancellationToken = default);
    /// <summary>Lista los motivos del operador; si <paramref name="soloParaRepartidor"/> es true devuelve solo los activos no internos.</summary>
    Task<List<MotivoNoEntrega>> ListarAsync(Guid operadorId, bool soloParaRepartidor, CancellationToken cancellationToken = default);
}