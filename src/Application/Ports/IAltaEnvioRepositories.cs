using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;

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

/// <summary>Relación comercio-operador, usada para autenticar la integración por clave de API.</summary>
public interface IComercioOperadorRepository
{
    Task<ComercioOperador?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Busca la relación por la huella SHA-256 de su clave de API.</summary>
    Task<ComercioOperador?> GetByHuellaClaveApiAsync(string huella, CancellationToken cancellationToken = default);
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
}
