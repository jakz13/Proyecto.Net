#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;

namespace UltimaMilla.Mobile.Platforms.Android;

[Service(
    Name = "com.ultimamilla.mobile.LocationForegroundService",
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation,
    Exported = false)]
public class LocationForegroundService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "ultimamilla_gps_channel";
    private const string ChannelName = "Telemetria de Ruta Activa";

    private PowerManager.WakeLock? _wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        try
        {
            CrearCanalNotificacion();

            var notificationIntent = new Intent(this, typeof(MainActivity));
            notificationIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pendingIntent = PendingIntent.GetActivity(
                this,
                0,
                notificationIntent,
                PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

            // El icono de notificación debe ser un recurso drawable válido y plano del sistema
            // (evitando adaptive icons en mipmap que disparan BadForegroundServiceNotificationException en Android 12-14+)
            int iconResId = global::Android.Resource.Drawable.IcMenuMyLocation;

            Notification notification;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                notification = new Notification.Builder(this, ChannelId)
                    .SetContentTitle("Operacion en Ruta Activa")
                    .SetContentText("Reportando posicion GPS del vehiculo en segundo plano")
                    .SetSmallIcon(iconResId)
                    .SetOngoing(true)
                    .SetContentIntent(pendingIntent)
                    .Build();
            }
            else
            {
                notification = new Notification.Builder(this)
                    .SetContentTitle("Operacion en Ruta Activa")
                    .SetContentText("Reportando posicion GPS del vehiculo en segundo plano")
                    .SetSmallIcon(iconResId)
                    .SetOngoing(true)
                    .SetContentIntent(pendingIntent)
                    .Build();
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }

            AdquirirWakeLock();

            return StartCommandResult.Sticky;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] No se pudo iniciar el servicio en primer plano: {ex.Message}");
            try
            {
                StopSelf();
            }
            catch
            {
                // Fallback defensivo ante parada anticipada
            }
            return StartCommandResult.NotSticky;
        }
    }

    private void CrearCanalNotificacion()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Low)
            {
                Description = "Canal de telemetria para seguimiento continuo en segundo plano"
            };

            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private void AdquirirWakeLock()
    {
        if (_wakeLock is { IsHeld: true })
            return;

        try
        {
            var powerManager = (PowerManager?)GetSystemService(PowerService);
            if (powerManager != null)
            {
                _wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, "UltimaMilla::LocationWakeLock");
                _wakeLock?.Acquire();
            }
        }
        catch
        {
            // Ignorar excepciones si el dispositivo o perfil restringe wake locks
        }
    }

    public override void OnDestroy()
    {
        try
        {
            if (_wakeLock is { IsHeld: true })
            {
                _wakeLock.Release();
                _wakeLock = null;
            }
        }
        catch
        {
        }

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(24))
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
                StopForeground(true);
            }
        }
        catch
        {
        }

        base.OnDestroy();
    }
}
#endif

