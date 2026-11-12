using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de persistencia para zonas de cobertura y sus franjas horarias.
/// </summary>
public interface IZonaCoberturaRepository
{
    Task<ZonaCobertura?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default);
    Task<bool> ExisteNombreAsync(string nombre, Guid operadorId, Guid? excluirZonaId = null, CancellationToken cancellationToken = default);
    Task AgregarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default);
    Task ActualizarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ZonaCobertura>> ListarZonasDelOperadorAsync(Guid operadorId, string? busqueda, bool? soloActivas, CancellationToken cancellationToken = default);
    Task ReemplazarFranjasAsync(ZonaCobertura zona, IEnumerable<FranjaHoraria> nuevasFranjas, CancellationToken cancellationToken = default);
}

