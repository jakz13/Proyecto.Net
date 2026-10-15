using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para configurar zonas de cobertura geográfica y franjas horarias de entrega (UC-02, RF-02).
/// Consumido por el Backoffice del Operador.
/// </summary>
public interface IZonasCoberturaApi
{
    /// <summary>
    /// Lista las zonas de cobertura del operador aplicando filtros de búsqueda.
    /// </summary>
    Task<IReadOnlyList<ZonaCoberturaDto>> ListarZonasAsync(FiltroZonasDto? filtro = null, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene una zona de cobertura por su identificador único junto con sus franjas horarias.
    /// </summary>
    Task<ZonaCoberturaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Define y registra una nueva zona de cobertura geográfica y asocia sus franjas horarias.
    /// Valida que no existan superposiciones horarias inconsistentes en el mismo día.
    /// </summary>
    Task<ZonaCoberturaDto> CrearZonaAsync(CrearZonaCoberturaDto nuevaZona, CancellationToken cancelacion = default);

    /// <summary>
    /// Actualiza los límites del polígono geográfico y la configuración de franjas horarias de una zona.
    /// Valida que no existan superposiciones horarias inconsistentes.
    /// </summary>
    Task<ZonaCoberturaDto> ActualizarZonaAsync(Guid id, ActualizarZonaCoberturaDto zonaActualizada, CancellationToken cancelacion = default);

    /// <summary>
    /// Modifica el estado activo/inactivo (alta/baja lógica) de una zona de cobertura.
    /// </summary>
    Task CambiarEstadoZonaAsync(Guid id, bool activa, CancellationToken cancelacion = default);

    /// <summary>
    /// Valida si un conjunto de franjas horarias contiene solapamientos o superposiciones inconsistentes.
    /// Retorna false o lanza excepción detallando las franjas en colisión.
    /// </summary>
    Task<(bool EsValido, string? MensajeError)> ValidarSuperposicionFranjasAsync(IEnumerable<CrearFranjaHorariaDto> franjas, CancellationToken cancelacion = default);
}
