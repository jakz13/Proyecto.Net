using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de Entity Framework Core para el repositorio de Comercios.
/// </summary>
public class EfComercioRepository : IComercioRepository
{
    private readonly AppDbContext _contexto;

    public EfComercioRepository(AppDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<Comercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _contexto.Comercios.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Comercio?> ObtenerPorDocumentoAsync(string documento, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return null;

        var documentoNormalizado = documento.Trim();
        return await _contexto.Comercios.FirstOrDefaultAsync(c => c.Documento == documentoNormalizado, cancellationToken);
    }

    public async Task<bool> ExisteDocumentoAsync(string documento, Guid? excluirComercioId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documento))
            return false;

        var documentoNormalizado = documento.Trim();
        var consulta = _contexto.Comercios.AsNoTracking().Where(c => c.Documento == documentoNormalizado);
        if (excluirComercioId.HasValue && excluirComercioId.Value != Guid.Empty)
        {
            consulta = consulta.Where(c => c.Id != excluirComercioId.Value);
        }

        return await consulta.AnyAsync(cancellationToken);
    }

    public async Task AgregarAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comercio);
        await _contexto.Comercios.AddAsync(comercio, cancellationToken);
    }

    public Task ActualizarAsync(Comercio comercio, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comercio);
        _contexto.Comercios.Update(comercio);
        return Task.CompletedTask;
    }
}

