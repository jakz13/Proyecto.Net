using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Tests.Fakes;

/// <summary>
/// Repositorio falso en memoria para pruebas unitarias de gestión de zonas de cobertura y franjas horarias.
/// </summary>
public class FakeZonaCoberturaRepository : IZonaCoberturaRepository
{
    public List<ZonaCobertura> Zonas { get; } = new();

    public Task<ZonaCobertura?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        var zona = Zonas.FirstOrDefault(z => z.Id == id && z.OperadorId == operadorId);
        return Task.FromResult(zona);
    }

    public Task<bool> ExisteNombreAsync(string nombre, Guid operadorId, Guid? excluirZonaId = null, CancellationToken cancellationToken = default)
    {
        var existe = Zonas.Any(z =>
            z.OperadorId == operadorId &&
            z.Nombre.Equals(nombre, StringComparison.OrdinalIgnoreCase) &&
            (!excluirZonaId.HasValue || z.Id != excluirZonaId.Value));

        return Task.FromResult(existe);
    }

    public Task AgregarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default)
    {
        Zonas.Add(zona);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default)
    {
        var indice = Zonas.FindIndex(z => z.Id == zona.Id);
        if (indice >= 0)
        {
            Zonas[indice] = zona;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ZonaCobertura>> ListarZonasDelOperadorAsync(Guid operadorId, string? busqueda, bool? soloActivas, CancellationToken cancellationToken = default)
    {
        var consulta = Zonas.Where(z => z.OperadorId == operadorId);

        if (soloActivas.HasValue)
        {
            consulta = consulta.Where(z => z.Activa == soloActivas.Value);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var terminoNormalizado = busqueda.Trim();
            consulta = consulta.Where(z => z.Nombre.Contains(terminoNormalizado, StringComparison.OrdinalIgnoreCase));
        }

        IReadOnlyList<ZonaCobertura> resultado = consulta.OrderBy(z => z.Nombre).ToList();
        return Task.FromResult(resultado);
    }

    public Task ReemplazarFranjasAsync(ZonaCobertura zona, IEnumerable<FranjaHoraria> nuevasFranjas, CancellationToken cancellationToken = default)
    {
        zona.QuitarFranjasHorarias();
        foreach (var franja in nuevasFranjas)
        {
            zona.AgregarFranjaHoraria(franja);
        }
        return Task.CompletedTask;
    }
}
