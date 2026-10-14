using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión del catálogo de motivos de no entrega (UC-05, RF-04, RF-21).
/// Permite al Administrador dar de alta, editar y activar/desactivar motivos,
/// impactando en los motivos que descarga la aplicación del repartidor en su próxima sincronización.
/// </summary>
public interface IMotivosNoEntregaApi
{
    /// <summary>
    /// Lista los motivos de no entrega según los filtros indicados.
    /// </summary>
    Task<IReadOnlyList<MotivoNoEntregaDto>> ListarMotivosAsync(FiltroMotivosNoEntregaDto? filtro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el detalle de un motivo por su identificador único.
    /// </summary>
    Task<MotivoNoEntregaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un nuevo motivo de no entrega en el catálogo (UC-05).
    /// </summary>
    Task<MotivoNoEntregaDto> CrearMotivoAsync(CrearMotivoNoEntregaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifica la descripción y/o estado de un motivo existente.
    /// </summary>
    Task<MotivoNoEntregaDto> ActualizarMotivoAsync(ActualizarMotivoNoEntregaDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conmuta o establece el estado activo/inactivo de un motivo.
    /// Al desactivarlo, el sistema lo retira de la próxima sincronización de la app del repartidor (RF-21).
    /// </summary>
    Task CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las métricas de resumen del catálogo y estado de sincronización móvil.
    /// </summary>
    Task<ResumenMotivosNoEntregaDto> ObtenerResumenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene exclusivamente los motivos activos que se descargan en la base de datos local SQLite de la app del repartidor.
    /// </summary>
    Task<IReadOnlyList<MotivoNoEntregaDto>> ObtenerMotivosParaSincronizacionMovilAsync(CancellationToken cancellationToken = default);
}
