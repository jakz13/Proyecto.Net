using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de persistencia para cuadros tarifarios versionados y sus reglas asociadas (UC-03, RF-03).
/// </summary>
public interface ICuadroTarifarioRepository
{
    Task<CuadroTarifario?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default);
    Task<CuadroTarifario?> ObtenerVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default);
    Task<CuadroTarifario?> GetVigenteAsync(Guid operadorId, DateTime fechaUtc, CancellationToken cancellationToken = default) => ObtenerVigenteAsync(operadorId, fechaUtc, cancellationToken);
    Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAsync(Guid operadorId, CancellationToken cancellationToken = default);
    Task<int> ObtenerUltimaVersionAsync(Guid operadorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CuadroTarifario>> ListarVersionesAbiertasAsync(Guid operadorId, CancellationToken cancellationToken = default);
    Task AgregarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default);
    Task ActualizarAsync(CuadroTarifario cuadro, CancellationToken cancellationToken = default);
}
