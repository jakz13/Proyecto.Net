namespace UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

/// <summary>
/// Parámetros de búsqueda y filtrado para el catálogo de motivos de no entrega (UC-05).
/// </summary>
public class FiltroMotivosNoEntregaDto
{
    /// <summary>
    /// Texto de coincidencia parcial sobre código o descripción.
    /// </summary>
    public string? Busqueda { get; set; }

    /// <summary>
    /// Filtrar por estado: true (solo activos), false (solo inactivos), null (todos).
    /// </summary>
    public bool? SoloActivos { get; set; }
}
