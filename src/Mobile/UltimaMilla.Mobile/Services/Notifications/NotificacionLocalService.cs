namespace UltimaMilla.Mobile.Services.Notifications;

public class NotificacionLocalService : INotificacionLocalService
{
    public void MostrarAlertaOperativa(string titulo, string mensaje)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var notificationManager = (Android.App.NotificationManager?)context.GetSystemService(Android.Content.Context.NotificationService);
            if (notificationManager == null)
                return;

            const string canalAlertasId = "ultimamilla_alertas_urgentes";
            const string canalAlertasNombre = "Alertas Operativas Urgentes";

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                var channel = new Android.App.NotificationChannel(
                    canalAlertasId,
                    canalAlertasNombre,
                    Android.App.NotificationImportance.High)
                {
                    Description = "Notificaciones de reasignaciones y modificaciones urgentes en la hoja de ruta"
                };
                channel.EnableVibration(true);
                channel.SetShowBadge(true);
                notificationManager.CreateNotificationChannel(channel);
            }

            var intent = new Android.Content.Intent(context, typeof(UltimaMilla.Mobile.MainActivity));
            intent.AddFlags(Android.Content.ActivityFlags.SingleTop | Android.Content.ActivityFlags.ClearTop);
            var pendingIntent = Android.App.PendingIntent.GetActivity(
                context,
                Random.Shared.Next(1000, 9000),
                intent,
                Android.App.PendingIntentFlags.Immutable | Android.App.PendingIntentFlags.UpdateCurrent);

            int iconResId = global::Android.Resource.Drawable.IcDialogAlert;

            Android.App.Notification notification;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                notification = new Android.App.Notification.Builder(context, canalAlertasId)
                    .SetContentTitle(titulo)
                    .SetContentText(mensaje)
                    .SetSmallIcon(iconResId)
                    .SetAutoCancel(true)
                    .SetContentIntent(pendingIntent)
                    .SetPriority((int)Android.App.NotificationPriority.High)
                    .Build();
            }
            else
            {
                notification = new Android.App.Notification.Builder(context)
                    .SetContentTitle(titulo)
                    .SetContentText(mensaje)
                    .SetSmallIcon(iconResId)
                    .SetAutoCancel(true)
                    .SetContentIntent(pendingIntent)
                    .Build();
            }

            var notificationId = Random.Shared.Next(2000, 9999);
            notificationManager.Notify(notificationId, notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificacionLocalService] Error al emitir notificacion en Android: {ex.Message}");
        }
#else
        System.Diagnostics.Debug.WriteLine($"[NotificacionLocalService] Alerta operativa ({titulo}): {mensaje}");
#endif
    }
}

