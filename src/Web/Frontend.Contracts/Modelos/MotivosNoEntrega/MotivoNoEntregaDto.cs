namespace UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;

/// <summary>
/// Modelo de datos que representa un motivo de no entrega en el catálogo del operador (UC-05, RF-04, RF-21).
/// </summary>
public class MotivoNoEntregaDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Código mnemotécnico único en mayúsculas (ej: DESTINATARIO_AUSENTE).
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Descripción clara y comprensible para el repartidor en campo.
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el motivo está habilitado para descarga y uso en la app móvil del repartidor (RF-21).
    /// </summary>
    public bool Activo { get; set; }

    /// <summary>
    /// Fecha de alta en el sistema.
    /// </summary>
    public DateTime FechaAlta { get; set; }

    /// <summary>
    /// Cantidad acumulada de intentos fallidos registrados con este motivo.
    /// </summary>
    public int CantidadUsos { get; set; }
}
