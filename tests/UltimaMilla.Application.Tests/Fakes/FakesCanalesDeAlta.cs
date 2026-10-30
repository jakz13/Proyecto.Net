using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakeComercioOperadorRepository : IComercioOperadorRepository
{
    public List<ComercioOperador> Relaciones { get; } = new();
    public List<UltimaMilla.Domain.Entities.Comercio.Comercio> Comercios { get; } = new();

    public Task<ComercioOperador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.FirstOrDefault(c => c.Id == id));

    public Task<ComercioOperador?> GetByHuellaClaveApiAsync(string huella, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.FirstOrDefault(c => c.ClaveApi == huella));

    public Task<ComercioOperador?> ObtenerPorComercioYOperadorAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.FirstOrDefault(c => c.ComercioId == comercioId && c.OperadorId == operadorId));

    public Task<bool> ExisteRelacionAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Relaciones.Any(c => c.ComercioId == comercioId && c.OperadorId == operadorId));

    public Task AgregarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default)
    {
        Relaciones.Add(relacion);
        return Task.CompletedTask;
    }

    public Task ActualizarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default)
    {
        var idx = Relaciones.FindIndex(r => r.Id == relacion.Id);
        if (idx != -1) Relaciones[idx] = relacion;
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<ComercioConRelacionOperador> Elementos, int Total)> ListarComerciosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        var query = from rel in Relaciones
                    join com in Comercios on rel.ComercioId equals com.Id
                    where rel.OperadorId == operadorId
                    select new ComercioConRelacionOperador(com, rel);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.Trim().ToLowerInvariant();
            query = query.Where(x => x.Comercio.RazonSocial.ToLowerInvariant().Contains(b) ||
                                     x.Comercio.NombreFantasia.ToLowerInvariant().Contains(b) ||
                                     x.Comercio.Documento.ToLowerInvariant().Contains(b) ||
                                     x.Comercio.Email.ToLowerInvariant().Contains(b));
        }

        if (soloActivos.HasValue)
        {
            query = query.Where(x => soloActivos.Value ? x.Relacion.EstaActivo : !x.Relacion.EstaActivo);
        }

        var lista = query.ToList();
        var total = lista.Count;
        var paginados = lista.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
        return Task.FromResult(((IReadOnlyList<ComercioConRelacionOperador>)paginados, total));
    }
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
