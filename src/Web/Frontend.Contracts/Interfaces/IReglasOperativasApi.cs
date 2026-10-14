using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión y versionado de las reglas operativas del operador logístico (UC-04, RF-04).
/// </summary>
public interface IReglasOperativasApi
{
    /// <summary>
    /// Lista el historial completo de versiones de reglas operativas (vigente e históricas).
    /// </summary>
    Task<IReadOnlyList<ReglaOperativaDto>> ListarVersionesAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la versión de reglas operativas actualmente activa y vigente.
    /// </summary>
    Task<ReglaOperativaDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle inmutable de una versión histórica por su identificador único.
    /// </summary>
    Task<ReglaOperativaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Publica una nueva versión de reglas operativas, archivando la anterior con su fecha de cierre de vigencia.
    /// </summary>
    Task<ReglaOperativaDto> PublicarNuevaVersionAsync(CrearReglaOperativaDto nuevaVersion, CancellationToken cancelacion = default);

    /// <summary>
    /// Evalúa y simula el cumplimiento de las reglas operativas vigentes para un envío.
    /// </summary>
    Task<ResultadoSimulacionReglasDto> SimularReglasAsync(SimularReglaOperativaDto datosEnvio, CancellationToken cancelacion = default);
}
