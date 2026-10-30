using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Tests.Fakes;

/// <summary>
/// Fakes en memoria para repositorios de suscripciones y avisos a comercios.
/// </summary>
public class FakeSuscripcionAvisoRepository : ISuscripcionAvisoRepository
{
    public List<SuscripcionAviso> Suscripciones { get; } = new();

    public Task<IReadOnlyList<SuscripcionAviso>> ListarPorComercioOperadorAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SuscripcionAviso> resultado = Suscripciones
            .Where(s => s.ComercioOperadorId == comercioOperadorId)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task<SuscripcionAviso?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Suscripciones.FirstOrDefault(s => s.Id == id));
    }

    public Task AddAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
    {
        Suscripciones.Add(suscripcion);
        return Task.CompletedTask;
    }

    public Task RemoveRangeAsync(IEnumerable<SuscripcionAviso> suscripciones, CancellationToken cancellationToken = default)
    {
        var idsParaEliminar = suscripciones.Select(s => s.Id).ToHashSet();
        Suscripciones.RemoveAll(s => idsParaEliminar.Contains(s.Id));
        return Task.CompletedTask;
    }
}

public class FakeAvisoComercioRepository : IAvisoComercioRepository
{
    private readonly FakeSuscripcionAvisoRepository? _suscripcionesRepo;
    public List<AvisoComercio> Avisos { get; } = new();

    public FakeAvisoComercioRepository(FakeSuscripcionAvisoRepository? suscripcionesRepo = null)
    {
        _suscripcionesRepo = suscripcionesRepo;
    }

    public Task<IReadOnlyList<AvisoComercio>> ListarRecientesPorComercioAsync(Guid comercioOperadorId, int limite = 10, CancellationToken cancellationToken = default)
    {
        if (_suscripcionesRepo != null)
        {
            var ids = _suscripcionesRepo.Suscripciones
                .Where(s => s.ComercioOperadorId == comercioOperadorId)
                .Select(s => s.Id)
                .ToHashSet();

            IReadOnlyList<AvisoComercio> filtrados = Avisos
                .Where(a => ids.Contains(a.SuscripcionAvisoId))
                .OrderByDescending(a => a.Timestamp)
                .Take(limite)
                .ToList();
            return Task.FromResult(filtrados);
        }

        IReadOnlyList<AvisoComercio> resultado = Avisos
            .OrderByDescending(a => a.Timestamp)
            .Take(limite)
            .ToList();
        return Task.FromResult(resultado);
    }

    public Task<IReadOnlyList<AvisoComercio>> ListarHistorialPorComercioAsync(Guid comercioOperadorId, string? estado = null, Guid? envioId = null, CancellationToken cancellationToken = default)
    {
        var consulta = Avisos.AsEnumerable();

        if (_suscripcionesRepo != null)
        {
            var ids = _suscripcionesRepo.Suscripciones
                .Where(s => s.ComercioOperadorId == comercioOperadorId)
                .Select(s => s.Id)
                .ToHashSet();

            consulta = consulta.Where(a => ids.Contains(a.SuscripcionAvisoId));
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            consulta = consulta.Where(a => a.Estado.Equals(estado.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (envioId.HasValue && envioId.Value != Guid.Empty)
        {
            consulta = consulta.Where(a => a.EnvioId == envioId.Value);
        }

        IReadOnlyList<AvisoComercio> resultado = consulta
            .OrderByDescending(a => a.Timestamp)
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<AvisoComercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Avisos.FirstOrDefault(a => a.Id == id));
    }

    public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        Avisos.Add(aviso);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
    {
        var indice = Avisos.FindIndex(a => a.Id == aviso.Id);
        if (indice >= 0)
        {
            Avisos[indice] = aviso;
        }
        return Task.CompletedTask;
    }
}
