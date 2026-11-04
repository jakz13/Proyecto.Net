using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Tests.Fakes;

/// <summary>
/// Repositorio falso en memoria para pruebas unitarias de cuadros tarifarios y versionado (UC-03).
/// </summary>
public class FakeCuadroTarifarioRepository : ICuadroTarifarioRepository
{
    public List<CuadroTarifario> Cuadros { get; } = new();

    public Task<CuadroTarifario?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        var cuadro = Cuadros.FirstOrDefault(c => c.Id == id && c.OperadorId == operadorId);
        return Task.FromResult(cuadro);
    }

    public Task<CuadroTarifario?> ObtenerVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default)
    {
        var vigente = Cuadros
            .Where(c => c.OperadorId == operadorId && c.EstaVigenteEn(fechaUtc))
            .OrderByDescending(c => c.Version)
            .FirstOrDefault();

        return Task.FromResult(vigente);
    }

    public Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default) =>
        ObtenerVigenteAsync(operadorId, fechaUtc, cancellationToken);

    public Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CuadroTarifario> lista = Cuadros
            .Where(c => c.OperadorId == operadorId)
            .OrderByDescending(c => c.Version)
            .ToList();

        return Task.FromResult(lista);
    }

    public Task<int> ObtenerUltimaVersionAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var ultima = Cuadros
            .Where(c => c.OperadorId == operadorId)
            .Select(c => (int?)c.Version)
            .Max() ?? 0;

        return Task.FromResult(ultima);
    }

    public Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAbiertasAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CuadroTarifario> abiertas = Cuadros
            .Where(c => c.OperadorId == operadorId && c.VigenteHasta == null)
            .ToList();

        return Task.FromResult(abiertas);
    }

    public Task AgregarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default)
    {
        Cuadros.Add(cuadro);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default)
    {
        var indice = Cuadros.FindIndex(c => c.Id == cuadro.Id);
        if (indice >= 0)
        {
            Cuadros[indice] = cuadro;
        }
        return Task.CompletedTask;
    }
}

