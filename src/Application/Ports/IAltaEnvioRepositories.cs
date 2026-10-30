using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using Comercio = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Ports;

/// <summary>Acceso al cuadro tarifario vigente del operador para tarificar envíos.</summary>
public interface ICuadroTarifarioRepository
{
    Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default);
}

/// <summary>Alta de destinatarios. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IDestinatarioRepository
{
    Task AddAsync(Destinatario destinatario, CancellationToken cancellationToken = default);
    Task<List<Destinatario>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>Alta de direcciones de entrega. No confirma cambios: lo hace IUnitOfWork.</summary>
public interface IDireccionRepository
{
    Task AddAsync(Direccion direccion, CancellationToken cancellationToken = default);
    Task<List<Direccion>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>Elemento que vincula un comercio con su relación hacia un operador específico.</summary>
public record ComercioConRelacionOperador(Comercio Comercio, ComercioOperador Relacion);

/// <summary>Relación comercio-operador, usada para autenticar la integración por clave de API y gestión de comercios (UC-01).</summary>
public interface IComercioOperadorRepository
{
    Task<ComercioOperador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Busca la relación por la huella SHA-256 de su clave de API.</summary>
    Task<ComercioOperador?> GetByHuellaClaveApiAsync(string huella, CancellationToken cancellationToken = default);
    Task<ComercioOperador?> ObtenerPorComercioYOperadorAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default);
    Task<bool> ExisteRelacionAsync(Guid comercioId, Guid operadorId, CancellationToken cancellationToken = default);
    Task AgregarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default);
    Task ActualizarAsync(ComercioOperador relacion, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ComercioConRelacionOperador> Elementos, int Total)> ListarComerciosDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        bool? soloActivos,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);
}

/// <summary>Lotes de importación masiva, identificados por comercio y clave de idempotencia.</summary>
public interface ILoteImportacionRepository
{
    Task<LoteImportacion?> GetByClaveAsync(Guid comercioOperadorId, string claveIdempotencia, CancellationToken cancellationToken = default);
    Task AddAsync(LoteImportacion lote, CancellationToken cancellationToken = default);
}

/// <summary>Solicitudes de reprogramación hechas por el destinatario desde el seguimiento público.</summary>
public interface ISolicitudReprogramacionRepository
{
    Task AddAsync(SolicitudReprogramacion solicitud, CancellationToken cancellationToken = default);
    Task<int> ContarAprobadasAsync(Guid envioId, CancellationToken cancellationToken = default);
    Task<int> ContarTodasAsync(Guid envioId, CancellationToken cancellationToken = default);
}

/// <summary>Acceso a datos del operador logístico para emisión de etiquetas y documentos de viaje.</summary>
public interface IOperadorRepository
{
    Task<Operador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
