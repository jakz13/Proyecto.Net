using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeReglaOperativaRepository : IReglaOperativaRepository
{
    public List<ReglaOperativa> Reglas { get; } = new();

    public Task AddAsync(ReglaOperativa regla, CancellationToken cancellationToken = default)
    {
        Reglas.Add(regla);
        return Task.CompletedTask;
    }

    public Task<ReglaOperativa?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reglas.FirstOrDefault(r => r.Id == id));

    public Task<ReglaOperativa?> ObtenerVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reglas.Where(r => r.OperadorId == operadorId && r.VigenteHasta == null).OrderByDescending(r => r.Version).FirstOrDefault());

    public Task<List<ReglaOperativa>> ListarAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Reglas.Where(r => r.OperadorId == operadorId).OrderByDescending(r => r.Version).ToList());
}

public class FakeMotivoNoEntregaRepository : IMotivoNoEntregaRepository
{
    public List<MotivoNoEntrega> Motivos { get; } = new();

    public Task AddAsync(MotivoNoEntrega motivo, CancellationToken cancellationToken = default)
    {
        Motivos.Add(motivo);
        return Task.CompletedTask;
    }

    public Task<MotivoNoEntrega?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Motivos.FirstOrDefault(m => m.Id == id));

    public Task<bool> ExisteCodigoAsync(Guid operadorId, string codigo, CancellationToken cancellationToken = default) =>
        Task.FromResult(Motivos.Any(m => m.OperadorId == operadorId && m.Codigo == codigo));

    public Task<List<MotivoNoEntrega>> ListarAsync(Guid operadorId, bool soloParaRepartidor, CancellationToken cancellationToken = default) =>
        Task.FromResult(Motivos.Where(m => m.OperadorId == operadorId && (!soloParaRepartidor || (m.Activo && !m.EsInterno))).OrderBy(m => m.Codigo).ToList());
}