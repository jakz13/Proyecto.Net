using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para obtener la identidad visual (colores, logo, contacto) del operador.
/// </summary>
public interface IIdentidadVisualApi
{
    /// <summary>
    /// Obtiene la identidad visual configurada para el operador resuelto en el contexto actual.
    /// </summary>
    Task<IdentidadVisualDto> ObtenerIdentidadActualAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Permite obtener la identidad visual de un operador específico.
    /// Usado exclusivamente en entorno Development para el selector de operadores de la demo.
    /// </summary>
    Task<IdentidadVisualDto> ObtenerPorOperadorDemoAsync(string operadorId, CancellationToken cancelacion = default);

    /// <summary>
    /// Lista los operadores disponibles para la demostración en Development.
    /// </summary>
    Task<IReadOnlyList<IdentidadVisualDto>> ListarOperadoresDemoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Actualiza la identidad visual (marca, colores, contacto) del operador actual (UC-06, RF-05).
    /// </summary>
    Task<IdentidadVisualDto> ActualizarIdentidadVisualAsync(ActualizarIdentidadVisualDto dto, CancellationToken cancelacion = default);
}
