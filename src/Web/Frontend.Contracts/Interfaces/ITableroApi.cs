using UltimaMilla.Frontend.Contracts.Modelos.Tablero;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de API para el monitoreo del tablero de operación en vivo de la flota (UC-12, RF-29).
/// </summary>
public interface ITableroApi
{
    /// <summary>
    /// Obtiene las métricas consolidadas, la posición de los vehículos activos y los envíos en riesgo de ventana horaria.
    /// </summary>
    Task<ResumenTableroDto> ObtenerResumenEnVivoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene la telemetría y lista de paradas detalladas de un vehículo u hoja de ruta en curso.
    /// </summary>
    Task<VehiculoEnVivoDto?> ObtenerDetalleVehiculoEnVivoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Envía una instrucción o mensaje prioritario al repartidor en su dispositivo móvil.
    /// </summary>
    Task<bool> EnviarInstruccionRepartidorAsync(Guid repartidorId, string mensaje, CancellationToken cancelacion = default);
}
