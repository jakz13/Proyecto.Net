namespace UltimaMilla.Mobile.Services.Notifications;

/// <summary>
/// Datos emitidos cada vez que se dispara una notificacion operativa local al repartidor.
/// </summary>
public sealed record NotificacionOperativaEventArgs(
    string Titulo,
    string Mensaje,
    string Categoria,
    int IdNotificacion,
    DateTimeOffset TimestampUtc);

/// <summary>
/// Servicio multiplataforma de notificaciones operativas locales (Android NotificationManager y Windows Toast + Banner corporativo).
/// </summary>
public interface INotificacionLocalService
{
    /// <summary>
    /// Evento disparado en memoria cuando se emite una notificacion operativa.
    /// </summary>
    event EventHandler<NotificacionOperativaEventArgs>? NotificacionEmitida;

    /// <summary>
    /// Solicita permisos de notificaciones en el sistema operativo cuando aplica (Android 13+ API 33+).
    /// </summary>
    Task<bool> SolicitarPermisosNotificacionAsync();

    /// <summary>
    /// Muestra una notificacion operativa de alta prioridad tanto en primer plano como en segundo plano.
    /// </summary>
    Task MostrarNotificacionOperativaAsync(
        string titulo,
        string mensaje,
        string categoria = "OPERACIONES",
        int? idNotificacion = null);

    /// <summary>
    /// Sobrecarga de compatibilidad sincrona para emisores legados.
    /// </summary>
    void MostrarAlertaOperativa(string titulo, string mensaje);
}
