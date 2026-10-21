using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Ports;

public interface IEnvioRepository
{
    Task AddAsync(Envio envio, CancellationToken cancellationToken = default);
    Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);
    /// <summary>Busca los envíos de un comercio con filtros y paginación. Siempre se acota al operador y al comercio indicados.</summary>
    Task<(List<Envio> Items, int Total)> BuscarPorComercioAsync(Guid operadorId, Guid comercioOperadorId, FiltroEnviosComercio filtro, CancellationToken cancellationToken = default);
    /// <summary>Lista los envíos de un operador que se encuentren en el estado indicado.</summary>
    Task<List<Envio>> ListarPorEstadoAsync(Guid operadorId, EstadoEnvio estado, CancellationToken cancellationToken = default);
    /// <summary>Busca un envío del comercio por la referencia que éste le asignó (idempotencia de altas).</summary>
    Task<Envio?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default);
    Task<Envio?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);
    /// <summary>Envíos más recientes del operador (tope fijo), nunca de otros operadores.</summary>
    Task<List<Envio>> ListarRecientesPorOperadorAsync(Guid operadorId, int maximo, CancellationToken cancellationToken = default);
    Task<Envio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetByHojaDeRutaIdAsync(Guid hojaDeRutaId, CancellationToken cancellationToken = default);
    Task<List<Envio>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task UpdateAsync(Envio envio, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IEnumerable<Envio> envios, CancellationToken cancellationToken = default);
    Task<ReglaOperativa?> GetReglaOperativaVigenteAsync(Guid operadorId, CancellationToken cancellationToken = default);
    Task<List<MotivoNoEntrega>> ListarMotivosNoEntregaActivosAsync(Guid operadorId, CancellationToken cancellationToken = default);
    Task<MotivoNoEntrega?> GetMotivoNoEntregaAsync(Guid operadorId, Guid? motivoId, string? codigo = null, CancellationToken cancellationToken = default);
}

/// <summary>Criterios de búsqueda de los envíos de un comercio (UC-28). El texto busca por destinatario o código de seguimiento.</summary>
public record FiltroEnviosComercio(
    EstadoEnvio? Estado,
    DateTime? DesdeUtc,
    DateTime? HastaUtcExclusivo,
    string? Texto,
    int Pagina,
    int TamanoPagina);
