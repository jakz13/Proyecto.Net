using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests.Fakes;

/// <summary>Repositorio de envíos en memoria para tests de handlers.</summary>
public class FakeEnvioRepository : IEnvioRepository
{
    public List<Envio> Envios { get; } = new();
    public ReglaOperativa? ReglaOperativa { get; set; }
    public List<MotivoNoEntrega> Motivos { get; } = new();

    /// <summary>Cantidad de consultas de código de seguimiento que responderán "ya existe" antes de aceptar uno.</summary>
    public int ColisionesDeCodigo { get; set; }

    public Task AddAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        Envios.Add(envio);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        if (ColisionesDeCodigo > 0)
        {
            ColisionesDeCodigo--;
            return Task.FromResult(true);
        }

        return Task.FromResult(Envios.Any(e => e.CodigoSeguimientoPublico == codigoSeguimiento));
    }

    public Task<List<Envio>> ListarPorEstadoAsync(Guid operadorId, EstadoEnvio estado, CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.Where(e => e.OperadorId == operadorId && e.EstadoActual == estado).ToList());

    public Task<(List<Envio> Items, int Total)> BuscarPorComercioAsync(Guid operadorId, Guid comercioOperadorId, FiltroEnviosComercio filtro, CancellationToken cancellationToken = default)
    {
        var consulta = Envios.Where(e => e.OperadorId == operadorId && e.ComercioOperadorId == comercioOperadorId);

        if (filtro.Estado.HasValue)
            consulta = consulta.Where(e => e.EstadoActual == filtro.Estado.Value);
        if (filtro.DesdeUtc.HasValue)
            consulta = consulta.Where(e => e.FechaAlta >= filtro.DesdeUtc.Value);
        if (filtro.HastaUtcExclusivo.HasValue)
            consulta = consulta.Where(e => e.FechaAlta < filtro.HastaUtcExclusivo.Value);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.ToLowerInvariant();
            consulta = consulta.Where(e => e.NombreDestinatario.ToLowerInvariant().Contains(texto)
                                           || e.CodigoSeguimientoPublico.ToLowerInvariant().Contains(texto));
        }

        var ordenados = consulta.OrderByDescending(e => e.FechaAlta).ToList();
        var items = ordenados.Skip((filtro.Pagina - 1) * filtro.TamanoPagina).Take(filtro.TamanoPagina).ToList();
        return Task.FromResult((items, ordenados.Count));
    }

    public Task<Envio?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.FirstOrDefault(e => e.ComercioOperadorId == comercioOperadorId && e.ReferenciaExterna == referenciaExterna));

    public Task<Envio?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.FirstOrDefault(e => e.CodigoSeguimientoPublico == codigoSeguimiento.Trim().ToUpperInvariant()));

    public Task<List<Envio>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.ToList());

    public Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.FirstOrDefault(e => e.Id == id));

    public Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Envios.Where(e => e.HojaDeRutaId == hojaDeRutaId).ToList());

    public Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Envios.Where(e => set.Contains(e.Id)).ToList());
    }

    public Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReglaOperativa);

    public Task<List<MotivoNoEntrega>> ListarMotivosNoEntregaActivosAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Motivos.Where(m => m.OperadorId == operadorId && m.Activo && !m.EsInterno).ToList());

    public Task<MotivoNoEntrega?> GetMotivoNoEntregaAsync(Guid operadorId, Guid? motivoId, string? codigo = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Motivos.FirstOrDefault(m => (motivoId.HasValue && m.Id == motivoId) || (codigo != null && m.Codigo == codigo)));
}

public class FakeDestinatarioRepository : IDestinatarioRepository
{
    public List<Destinatario> Destinatarios { get; } = new();

    public Task AddAsync(Destinatario destinatario, CancellationToken cancellationToken = default)
    {
        Destinatarios.Add(destinatario);
        return Task.CompletedTask;
    }

    public Task<List<Destinatario>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Destinatarios.Where(d => set.Contains(d.Id)).ToList());
    }
}

public class FakeDireccionRepository : IDireccionRepository
{
    public List<Direccion> Direcciones { get; } = new();

    public Task AddAsync(Direccion direccion, CancellationToken cancellationToken = default)
    {
        Direcciones.Add(direccion);
        return Task.CompletedTask;
    }

    public Task<List<Direccion>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var set = ids.ToHashSet();
        return Task.FromResult(Direcciones.Where(d => set.Contains(d.Id)).ToList());
    }
}

public class FakeCuadroTarifarioRepository : ICuadroTarifarioRepository
{
    public List<CuadroTarifario> Cuadros { get; } = new();

    public Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(Cuadros
            .Where(c => c.OperadorId == operadorId && c.EstaVigenteEn(fechaUtc))
            .OrderByDescending(c => c.Version)
            .FirstOrDefault());
}

public class FakeRendicionRepository : IRendicionRepository
{
    public List<UltimaMilla.Domain.Entities.Ejecucion.RendicionDeposito> Rendiciones { get; } = new();

    public Task AddAsync(UltimaMilla.Domain.Entities.Ejecucion.RendicionDeposito rendicion, CancellationToken cancellationToken = default)
    {
        Rendiciones.Add(rendicion);
        return Task.CompletedTask;
    }

    public Task<UltimaMilla.Domain.Entities.Ejecucion.RendicionDeposito?> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Rendiciones.FirstOrDefault(r => r.HojaDeRutaId == hojaDeRutaId));
}

public class FakeDevolucionRepository : IDevolucionRepository
{
    public List<UltimaMilla.Domain.Entities.Ejecucion.Devolucion> Devoluciones { get; } = new();

    public Task AddAsync(UltimaMilla.Domain.Entities.Ejecucion.Devolucion devolucion, CancellationToken cancellationToken = default)
    {
        Devoluciones.Add(devolucion);
        return Task.CompletedTask;
    }

    public Task<UltimaMilla.Domain.Entities.Ejecucion.Devolucion?> GetByEnvioIdAsync(Guid envioId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Devoluciones.FirstOrDefault(d => d.EnvioId == envioId));

    public Task<List<UltimaMilla.Domain.Entities.Ejecucion.Devolucion>> GetByEnviosIdsAsync(IEnumerable<Guid> enviosIds, CancellationToken cancellationToken = default)
    {
        var ids = enviosIds.ToHashSet();
        return Task.FromResult(Devoluciones.Where(d => ids.Contains(d.EnvioId)).ToList());
    }
}

public class FakeOutboxStorage : IOutboxStorage
{
    public List<UltimaMilla.Domain.Entities.Mensajeria.OutboxMessage> Mensajes { get; } = new();

    public Task SaveAsync(UltimaMilla.Domain.Entities.Mensajeria.OutboxMessage message, CancellationToken cancellationToken = default)
    {
        Mensajes.Add(message);
        return Task.CompletedTask;
    }

    public Task SaveRangeAsync(IEnumerable<UltimaMilla.Domain.Entities.Mensajeria.OutboxMessage> messages, CancellationToken cancellationToken = default)
    {
        Mensajes.AddRange(messages);
        return Task.CompletedTask;
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int Confirmaciones { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Confirmaciones++;
        return Task.FromResult(1);
    }
}

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
}

public class FakeCurrentTenant : ICurrentTenant
{
    public Guid OperadorId { get; set; } = Guid.NewGuid();
    public Guid? UsuarioId { get; set; } = Guid.NewGuid();
    public Guid? ComercioOperadorId { get; set; } = Guid.NewGuid();
    public bool IsAuthenticated { get; set; } = true;
}
