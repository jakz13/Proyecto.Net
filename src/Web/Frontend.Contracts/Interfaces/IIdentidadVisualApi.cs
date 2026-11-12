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
    /// Operadores con los que trabaja el comercio de la sesión (mismo RUT vinculado a varios operadores) y el de la sesión actual.
    /// Requiere sesión de comercio: sin ella la API responde 401/403.
    /// </summary>
    Task<OperadoresDelComercioDto> ObtenerOperadoresDelComercioAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Abre sesión con otro de los operadores del comercio y devuelve el token nuevo, acotado a ese operador.
    /// </summary>
    Task<SesionOperadorDto> AbrirSesionConOperadorAsync(string operadorId, CancellationToken cancelacion = default);

    /// <summary>
    /// Actualiza la identidad visual (marca, colores, contacto) del operador actual.
    /// </summary>
    Task<IdentidadVisualDto> ActualizarIdentidadVisualAsync(ActualizarIdentidadVisualDto dto, CancellationToken cancelacion = default);

    /// <summary>
    /// Envía un correo electrónico de prueba al destinatario con la identidad visual del operador actual.
    /// </summary>
    Task<(bool Exitoso, string Mensaje)> ProbarCorreoIdentidadAsync(
        string emailDestino,
        string estadoEnvio = "EnTransito",
        CancellationToken cancelacion = default)
        => Task.FromResult((true, $"Correo de prueba simulado hacia {emailDestino}."));
}
