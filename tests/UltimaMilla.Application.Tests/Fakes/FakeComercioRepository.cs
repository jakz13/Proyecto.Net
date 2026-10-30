using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeComercioRepository : IComercioRepository
{
    public List<Comercio> Comercios { get; } = new();

    public Task<Comercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = Comercios.FirstOrDefault(c => c.Id == id);
        return Task.FromResult(item);
    }

    public Task<Comercio?> ObtenerPorDocumentoAsync(string documento, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return Task.FromResult<Comercio?>(null);

        var norm = documento.Trim();
        var item = Comercios.FirstOrDefault(c => c.Documento == norm);
        return Task.FromResult(item);
    }

    public Task<bool> ExisteDocumentoAsync(string documento, Guid? excluirComercioId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return Task.FromResult(false);

        var norm = documento.Trim();
        var existe = Comercios.Any(c => c.Documento == norm && (!excluirComercioId.HasValue || c.Id != excluirComercioId.Value));
        return Task.FromResult(existe);
    }

    public Task AgregarAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        Comercios.Add(comercio);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        var idx = Comercios.FindIndex(c => c.Id == comercio.Id);
        if (idx != -1)
        {
            Comercios[idx] = comercio;
        }
        return Task.CompletedTask;
    }
}

