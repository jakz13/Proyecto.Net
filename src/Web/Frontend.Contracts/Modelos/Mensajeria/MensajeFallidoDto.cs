namespace UltimaMilla.Frontend.Contracts.Modelos.Mensajeria;

/// <summary>
/// Representa un mensaje retenido en la cola de fallidos (Dead Letter Queue) tras agotar sus reintentos automáticos (UC-42).
/// </summary>
public record MensajeFallidoDto
{
    /// <summary>
    /// Identificador único del mensaje en la cola de fallidos.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Tipo de evento o mensaje de la plataforma (ej: EventoPaqueteDespachado, NotificacionWebhookComercio).
    /// </summary>
    public string TipoMensaje { get; init; } = string.Empty;

    /// <summary>
    /// Nombre de la cola interna de origen en el broker de mensajería asíncrona.
    /// </summary>
    public string ColaOrigen { get; init; } = string.Empty;

    /// <summary>
    /// Contenido JSON del mensaje que originó el procesamiento asíncrono.
    /// </summary>
    public string PayloadOriginal { get; init; } = string.Empty;

    /// <summary>
    /// Resumen del motivo por el cual falló el procesamiento del mensaje.
    /// </summary>
    public string MotivoFallo { get; init; } = string.Empty;

    /// <summary>
    /// Traza técnica de la excepción ocurrida al intentar procesar el mensaje.
    /// </summary>
    public string DetalleExcepcion { get; init; } = string.Empty;

    /// <summary>
    /// Cantidad de intentos de ejecución realizados hasta el momento.
    /// </summary>
    public int Intentos { get; init; }

    /// <summary>
    /// Fecha y hora del primer intento fallido.
    /// </summary>
    public DateTime PrimerFallo { get; init; }

    /// <summary>
    /// Fecha y hora del último fallo registrado.
    /// </summary>
    public DateTime UltimoFallo { get; init; }

    /// <summary>
    /// Estado actual en la cola de fallidos: "Fallido", "Reintentando", "Resuelto" o "Descartado".
    /// </summary>
    public string Estado { get; init; } = "Fallido";

    /// <summary>
    /// Identificador del operador logístico asociado (si aplica).
    /// </summary>
    public string? OperadorId { get; init; }

    /// <summary>
    /// Nombre legible y amigable de la operación para el usuario administrador.
    /// </summary>
    public string TipoLegible => TipoMensaje switch
    {
        "EventoPaqueteDespachado" => "Despacho de Paquete",
        "NotificacionWebhookComercio" => "Aviso a Comercio (Webhook)",
        "SincronizacionOfflineRepartidor" => "Sincronización de Repartidor",
        "GeneracionEtiquetaZPL" => "Impresión de Etiqueta",
        "ActualizacionRutaRepartidor" => "Cálculo de Ruta",
        "FacturacionEnvioComercio" => "Facturación de Envíos",
        _ => TipoMensaje
    };

    /// <summary>
    /// Explicación breve y directa del motivo del fallo para el usuario administrador.
    /// </summary>
    public string MotivoDigerible
    {
        get
        {
            if (string.IsNullOrWhiteSpace(MotivoFallo))
                return "Falla en el procesamiento automático";

            var motivoMin = MotivoFallo.ToLowerInvariant();

            if (motivoMin.Contains("brokerunreachableexception") || motivoMin.Contains("rabbitmq") || motivoMin.Contains("exchange"))
                return "Error al registrar el envío";

            if (motivoMin.Contains("504") || motivoMin.Contains("gateway timeout") || motivoMin.Contains("tiendacentral"))
                return "El comercio no respondió al aviso";

            if (motivoMin.Contains("concurrency") || motivoMin.Contains("conflicto"))
                return "El paquete ya fue modificado";

            if (motivoMin.Contains("zpl") || motivoMin.Contains("timeout") || motivoMin.Contains("etiqueta"))
                return "Error al imprimir la etiqueta";

            if (motivoMin.Contains("osrm") || motivoMin.Contains("socket") || motivoMin.Contains("rechazada"))
                return "Error al calcular la ruta";

            if (motivoMin.Contains("facturacion") || motivoMin.Contains("firma") || motivoMin.Contains("certificado") || motivoMin.Contains("dgi"))
                return "Error al facturar el envío";

            if (motivoMin.Contains("reintento manual fallido") || motivoMin.Contains("flujo alternativo"))
                return "El reintento volvió a fallar";

            return MotivoFallo;
        }
    }
}
