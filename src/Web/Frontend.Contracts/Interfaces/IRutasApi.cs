using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de API para la planificación, validación y liberación de hojas de ruta por el despachador.
/// </summary>
public interface IRutasApi
{
    /// <summary>
    /// Consulta el listado de hojas de ruta aplicando filtros de fecha, repartidor o estado.
    /// </summary>
    Task<IReadOnlyList<HojaDeRutaDto>> ListarHojasDeRutaAsync(FiltroRutasDto? filtro = null, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle completo de una hoja de ruta, incluyendo sus paradas ordenadas.
    /// </summary>
    Task<HojaDeRutaDto?> ObtenerHojaDeRutaPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene los envíos en depósito que se encuentran pendientes de asignar a una ruta.
    /// </summary>
    Task<IReadOnlyList<EnvioPendienteRutaDto>> ListarEnviosPendientesAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Valida las restricciones de capacidad, paradas máximas, compatibilidad de franjas y colisiones antes de confirmar.
    /// </summary>
    Task<ResultadoArmadoRutaDto> ValidarArmadoRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default);

    /// <summary>
    /// Arma la hoja de ruta, asigna los envíos garantizando unicidad, ordena las paradas y la libera a los repartidores.
    /// </summary>
    Task<ResultadoArmadoRutaDto> ArmarYConfirmarRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default);

    /// <summary>
    /// Libera formalmente una hoja de ruta existente para habilitar su descarga en la app móvil.
    /// </summary>
    Task<bool> LiberarHojaDeRutaAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Retira, reasigna a otra ruta, reprograma o marca como no entregado un envío en ruta (UC-10, RF-13, RF-15).
    /// </summary>
    Task<ResultadoReasignacionDto> ReasignarOReprogramarEnvioAsync(ReasignarEnvioDto dto, string despachadorUsuario, CancellationToken cancelacion = default);

    /// <summary>
    /// Consulta el registro histórico de reasignaciones e incidencias de envíos (RF-12, RF-15).
    /// </summary>
    Task<IReadOnlyList<EventoReasignacionDto>> ListarEventosReasignacionAsync(Guid? hojaDeRutaId = null, CancellationToken cancelacion = default);
}
