using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión y versionado del cuadro tarifario del operador (UC-03, RF-03).
/// </summary>
public interface ICuadroTarifarioApi
{
    /// <summary>
    /// Lista el historial completo de versiones del cuadro tarifario (vigente e históricas).
    /// </summary>
    Task<IReadOnlyList<CuadroTarifarioDto>> ListarVersionesAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la versión actualmente vigente del cuadro tarifario del operador.
    /// </summary>
    Task<CuadroTarifarioDto?> ObtenerVersionVigenteAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de una versión específica por su identificador.
    /// </summary>
    Task<CuadroTarifarioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Publica una nueva versión del cuadro tarifario, cerrando la vigencia de la versión anterior.
    /// </summary>
    Task<CuadroTarifarioDto> PublicarNuevaVersionAsync(CrearVersionCuadroTarifarioDto nuevaVersion, CancellationToken cancelacion = default);

    /// <summary>
    /// Simula el cálculo de tarifa para un envío con peso, volumen, zona y modalidad bajo la versión vigente.
    /// </summary>
    Task<CotizacionSimuladaDto> SimularTarifaAsync(SimularTarifaDto datosEnvio, CancellationToken cancelacion = default);
}
