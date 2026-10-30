using UltimaMilla.Domain.Entities.Comercio;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de persistencia para la entidad de dominio Comercio.
/// </summary>
public interface IComercioRepository
{
    Task<Comercio?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Comercio?> ObtenerPorDocumentoAsync(string documento, CancellationToken cancellationToken = default);
    Task<bool> ExisteDocumentoAsync(string documento, Guid? excluirComercioId = null, CancellationToken cancellationToken = default);
    Task AgregarAsync(Comercio comercio, CancellationToken cancellationToken = default);
    Task ActualizarAsync(Comercio comercio, CancellationToken cancellationToken = default);
}

