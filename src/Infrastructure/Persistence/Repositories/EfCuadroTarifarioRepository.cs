using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación con Entity Framework Core del repositorio de cuadros tarifarios versionados (UC-03, RF-03).
/// </summary>
public class EfCuadroTarifarioRepository : ICuadroTarifarioRepository
{
    private readonly AppDbContext _contexto;

    public EfCuadroTarifarioRepository(AppDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<CuadroTarifario?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        return await _contexto.CuadrosTarifarios
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.OperadorId == operadorId, cancellationToken);
    }

    public async Task<CuadroTarifario?> ObtenerVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default)
    {
        return await _contexto.CuadrosTarifarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.OperadorId == operadorId && c.VigenteDesde <= fechaUtc && (c.VigenteHasta == null || c.VigenteHasta >= fechaUtc))
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default) =>
        ObtenerVigenteAsync(operadorId, fechaUtc, cancellationToken);

    public async Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var lista = await _contexto.CuadrosTarifarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.OperadorId == operadorId)
            .OrderByDescending(c => c.Version)
            .ToListAsync(cancellationToken);

        return lista;
    }

    public async Task<int> ObtenerUltimaVersionAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var maxVersion = await _contexto.CuadrosTarifarios
            .IgnoreQueryFilters()
            .Where(c => c.OperadorId == operadorId)
            .MaxAsync(c => (int?)c.Version, cancellationToken);

        return maxVersion ?? 0;
    }

    public async Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAbiertasAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var abiertas = await _contexto.CuadrosTarifarios
            .IgnoreQueryFilters()
            .Where(c => c.OperadorId == operadorId && c.VigenteHasta == null)
            .ToListAsync(cancellationToken);

        return abiertas;
    }

    public async Task AgregarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default)
    {
        await _contexto.CuadrosTarifarios.AddAsync(cuadro, cancellationToken);
    }

    public Task ActualizarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default)
    {
        _contexto.CuadrosTarifarios.Update(cuadro);
        return Task.CompletedTask;
    }
}
