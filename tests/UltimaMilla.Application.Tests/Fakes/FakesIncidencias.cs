using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Incidencias;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeIncidenciaRepository : IIncidenciaRepository
{
    public List<Incidencia> Incidencias { get; } = new();

    public Task AddAsync(Incidencia incidencia, CancellationToken cancellationToken = default)
    {
        Incidencias.Add(incidencia);
        return Task.CompletedTask;
    }

    public Task<Incidencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Incidencias.FirstOrDefault(i => i.Id == id));

    public Task<(List<Incidencia> Items, int Total)> ListarAsync(
        Guid operadorId, string? estado, string? tipo, int pagina, int tamanoPagina, CancellationToken cancellationToken = default)
    {
        var consulta = Incidencias.Where(i => i.OperadorId == operadorId);
        if (estado is not null)
            consulta = consulta.Where(i => i.Estado == estado);
        if (tipo is not null)
            consulta = consulta.Where(i => i.Tipo == tipo);

        var ordenadas = consulta.OrderByDescending(i => i.FechaApertura).ToList();
        return Task.FromResult((ordenadas.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList(), ordenadas.Count));
    }
}
