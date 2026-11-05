namespace UltimaMilla.Application.Ports;

/// <summary>
/// Puerto de observabilidad para registrar métricas de negocio y operativas de la plataforma (UC-44, §6.12).
/// Mantiene la capa de aplicación desacoplada de OpenTelemetry y System.Diagnostics.Metrics.
/// </summary>
public interface IMetricasNegocio
{
    /// <summary>Incrementa el contador de aciertos de lectura en el caché distribuido (<c>um_cache_aciertos_total</c>).</summary>
    void RegistrarAciertoCache();

    /// <summary>Incrementa el contador de fallos de lectura en el caché distribuido (<c>um_cache_fallos_total</c>).</summary>
    void RegistrarFalloCache();

    /// <summary>Registra el resultado de un intento de entrega de aviso a un comercio (<c>um_avisos_comercio_intentos_total</c>).</summary>
    void RegistrarIntentoAviso(bool exito);

    /// <summary>Incrementa el contador de eventos recibidos desde los endpoints de sincronización móvil (<c>um_sync_eventos_recibidos_total</c>).</summary>
    void RegistrarEventoSincronizacion(int cantidad = 1);
}

