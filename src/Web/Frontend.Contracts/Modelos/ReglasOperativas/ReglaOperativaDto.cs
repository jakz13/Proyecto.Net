namespace UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

/// <summary>
/// Modelo de transferencia de datos que representa una versión de las reglas operativas del operador logístico (UC-04, RF-04).
/// </summary>
public record ReglaOperativaDto
{
    /// <summary>
    /// Identificador único del registro de reglas operativas.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Identificador del operador logístico (tenant).
    /// </summary>
    public Guid OperadorId { get; init; }

    /// <summary>
    /// Número correlativo e incremental de versión (ej: 1, 2, 3...).
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Fecha y hora a partir de la cual entra en vigencia esta versión.
    /// </summary>
    public DateTime VigenteDesde { get; init; }

    /// <summary>
    /// Fecha y hora en la que caducó la vigencia. Es nulo si la versión es la actualmente activa.
    /// </summary>
    public DateTime? VigenteHasta { get; init; }

    /// <summary>
    /// Indica si esta versión rige actualmente para los envíos nuevos.
    /// </summary>
    public bool EsVigente => VigenteHasta == null;

    /// <summary>
    /// Cantidad máxima permitida de intentos de entrega antes de iniciar la política de devolución.
    /// </summary>
    public int MaxIntentosEntrega { get; init; }

    /// <summary>
    /// Intervalo mínimo de tiempo que debe transcurrir entre un intento de entrega fallido y el siguiente.
    /// </summary>
    public TimeSpan PlazoEntreIntentos { get; init; }

    /// <summary>
    /// Plazo entre intentos expresado en horas para facilitar la visualización.
    /// </summary>
    public int HorasEntreIntentos => (int)PlazoEntreIntentos.TotalHours;

    /// <summary>
    /// Tipo de comprobante de entrega exigido al transportista (ej: Firma digital, Foto comprobante, PIN).
    /// </summary>
    public string TipoPruebaEntregaExigida { get; init; } = string.Empty;

    /// <summary>
    /// Política aplicada cuando se agotan los intentos máximos sin éxito.
    /// </summary>
    public string PoliticaDevolucion { get; init; } = string.Empty;

    /// <summary>
    /// Plazo comprometido máximo para la modalidad Estándar.
    /// </summary>
    public TimeSpan PlazoComprometidoEstandar { get; init; }

    /// <summary>
    /// Plazo estándar expresado en horas.
    /// </summary>
    public int HorasPlazoComprometidoEstandar => (int)PlazoComprometidoEstandar.TotalHours;

    /// <summary>
    /// Plazo comprometido máximo para modalidades urgentes (Express / Mismo Día).
    /// </summary>
    public TimeSpan PlazoComprometidoUrgente { get; init; }

    /// <summary>
    /// Plazo urgente expresado en horas.
    /// </summary>
    public int HorasPlazoComprometidoUrgente => (int)PlazoComprometidoUrgente.TotalHours;

    /// <summary>
    /// Observaciones o justificación del cambio de versión.
    /// </summary>
    public string? Comentario { get; init; }
}
