using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeComercioOperadorRepository : IComercioOperadorRepository
{
    public List<ComercioOperador> Relaciones { get; } = new();

    public Task<ComercioOperador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.FirstOrDefault(c => c.Id == id));

    public Task<ComercioOperador?> GetByHuellaClaveApiAsync(string huella, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.FirstOrDefault(c => c.ClaveApi == huella));
}

public class FakeLoteImportacionRepository : ILoteImportacionRepository
{
    public List<LoteImportacion> Lotes { get; } = new();

    public Task<LoteImportacion?> GetByClaveAsync(Guid comercioOperadorId, string claveIdempotencia, CancellationToken cancellationToken = default) =>
        Task.FromResult(Lotes.FirstOrDefault(l => l.ComercioOperadorId == comercioOperadorId && l.ClaveIdempotencia == claveIdempotencia));

    public Task AddAsync(LoteImportacion lote, CancellationToken cancellationToken = default)
    {
        Lotes.Add(lote);
        return Task.CompletedTask;
    }
}
