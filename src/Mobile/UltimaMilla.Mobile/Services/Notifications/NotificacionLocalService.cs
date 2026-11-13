using System.Security;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;

namespace UltimaMilla.Mobile.Services.Notifications;

/// <summary>
/// Implementacion multiplataforma de notificaciones operativas para el repartidor.
/// Android: NotificationChannel de alta prioridad + NotificationCompat + PendingIntent de restauracion.
/// Windows / fallback: AppNotificationManager (best-effort) + Banner corporativo en primer plano.
/// </summary>
public sealed class NotificacionLocalService : INotificacionLocalService
{
    public const string CanalAlertasId = "ultimamilla_alertas_operativas";
    public const string CanalAlertasNombre = "Alertas Operativas de Ruta";

    private readonly ILogger<NotificacionLocalService>? _logger;
    private int _secuenciaId = 2000;
    private readonly SemaphoreSlim _bannerLock = new(1, 1);

    public event EventHandler<NotificacionOperativaEventArgs>? NotificacionEmitida;

    public NotificacionLocalService(ILogger<NotificacionLocalService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> SolicitarPermisosNotificacionAsync()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            AsegurarCanalAndroid(context);

            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                return true;
            }

            var estadoActual = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context,
                global::Android.Manifest.Permission.PostNotifications);

            if (estadoActual == global::Android.Content.PM.Permission.Granted)
            {
                return true;
            }

            var status = await MainThread.InvokeOnMainThreadAsync(async () =>
                await Permissions.RequestAsync<Permissions.PostNotifications>());

            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "No se pudo completar la solicitud de permisos de notificaciones en Android.");
            return false;
        }
#else
        return await Task.FromResult(true);
#endif
    }

    public async Task MostrarNotificacionOperativaAsync(
        string titulo,
        string mensaje,
        string categoria = "OPERACIONES",
        int? idNotificacion = null)
    {
        var tituloSeguro = string.IsNullOrWhiteSpace(titulo)
            ? "Alerta Operativa"
            : titulo.Trim();
        var mensajeSeguro = string.IsNullOrWhiteSpace(mensaje)
            ? "Actualizacion disponible en tu jornada."
            : mensaje.Trim();
        var categoriaSegura = string.IsNullOrWhiteSpace(categoria)
            ? "OPERACIONES"
            : categoria.Trim().ToUpperInvariant();

        var notificacionId = idNotificacion ?? Interlocked.Increment(ref _secuenciaId);

        NotificacionEmitida?.Invoke(
            this,
            new NotificacionOperativaEventArgs(
                tituloSeguro,
                mensajeSeguro,
                categoriaSegura,
                notificacionId,
                DateTimeOffset.UtcNow));

#if ANDROID
        try
        {
            MostrarNotificacionAndroid(tituloSeguro, mensajeSeguro, notificacionId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Fallo al mostrar notificacion nativa en Android.");
        }
#elif WINDOWS
        IntentarMostrarAppNotificationWindows(tituloSeguro, mensajeSeguro);
        await MostrarBannerCorporativoUiAsync(tituloSeguro, mensajeSeguro, categoriaSegura);
#else
        await MostrarBannerCorporativoUiAsync(tituloSeguro, mensajeSeguro, categoriaSegura);
#endif

        await Task.CompletedTask;
    }

    public void MostrarAlertaOperativa(string titulo, string mensaje)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await MostrarNotificacionOperativaAsync(titulo, mensaje, "OPERACIONES");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error en wrapper sincrono MostrarAlertaOperativa.");
            }
        });
    }

#if ANDROID
    private static void AsegurarCanalAndroid(global::Android.Content.Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        if (context.GetSystemService(global::Android.Content.Context.NotificationService)
            is not global::Android.App.NotificationManager manager)
        {
            return;
        }

        if (manager.GetNotificationChannel(CanalAlertasId) is not null)
        {
            return;
        }

        var canal = new global::Android.App.NotificationChannel(
            CanalAlertasId,
            CanalAlertasNombre,
            global::Android.App.NotificationImportance.High)
        {
            Description = "Alertas criticas de hoja de ruta, geocercas, sincronizacion y bateria del repartidor.",
            LockscreenVisibility = global::Android.App.NotificationVisibility.Public
        };
        canal.EnableVibration(true);
        canal.SetVibrationPattern(new long[] { 0, 250, 150, 250 });

        manager.CreateNotificationChannel(canal);
    }

    private void MostrarNotificacionAndroid(string titulo, string mensaje, int notificacionId)
    {
        var context = global::Android.App.Application.Context;
        AsegurarCanalAndroid(context);

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var permiso = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context,
                global::Android.Manifest.Permission.PostNotifications);

            if (permiso != global::Android.Content.PM.Permission.Granted)
            {
                _logger?.LogDebug("Permiso POST_NOTIFICATIONS no concedido; se omite notificacion #{Id}.", notificacionId);
                return;
            }
        }

        var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        if (launchIntent is null)
        {
            var activityType = Platform.CurrentActivity?.GetType();
            if (activityType is not null)
            {
                launchIntent = new global::Android.Content.Intent(context, activityType);
            }
        }

        global::Android.App.PendingIntent? pendingIntent = null;
        if (launchIntent is not null)
        {
            launchIntent.AddFlags(
                global::Android.Content.ActivityFlags.SingleTop |
                global::Android.Content.ActivityFlags.ClearTop);

            pendingIntent = global::Android.App.PendingIntent.GetActivity(
                context,
                notificacionId,
                launchIntent,
                global::Android.App.PendingIntentFlags.UpdateCurrent |
                global::Android.App.PendingIntentFlags.Immutable);
        }

        var builder = new AndroidX.Core.App.NotificationCompat.Builder(context, CanalAlertasId);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle(titulo);
        builder.SetContentText(mensaje);
        builder.SetStyle(new AndroidX.Core.App.NotificationCompat.BigTextStyle().BigText(mensaje));
        builder.SetPriority(AndroidX.Core.App.NotificationCompat.PriorityHigh);
        builder.SetVisibility(AndroidX.Core.App.NotificationCompat.VisibilityPublic);
        builder.SetDefaults(AndroidX.Core.App.NotificationCompat.DefaultAll);
        builder.SetAutoCancel(true);

        if (pendingIntent is not null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        var notification = builder.Build();
        if (notification is null)
        {
            return;
        }

        var notificationManager = AndroidX.Core.App.NotificationManagerCompat.From(context);
        notificationManager?.Notify(notificacionId, notification);
    }
#endif

#if WINDOWS
    private void IntentarMostrarAppNotificationWindows(string titulo, string mensaje)
    {
        try
        {
            var xmlPayload =
                $"<toast><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(titulo)}</text>" +
                $"<text>{SecurityElement.Escape(mensaje)}</text>" +
                $"</binding></visual></toast>";

            var notification = new Microsoft.Windows.AppNotifications.AppNotification(xmlPayload);
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
        }
        catch (Exception ex)
        {
            // En modo unpackaged (WindowsPackageType=None) AppNotificationManager puede no estar registrado; el banner UI cubre la visibilidad.
            _logger?.LogDebug(ex, "AppNotificationManager no disponible en este entorno Windows; usando banner corporativo.");
        }
    }
#endif

    private async Task MostrarBannerCorporativoUiAsync(string titulo, string mensaje, string categoria)
    {
        try
        {
            if (Application.Current is null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var paginaActiva = ResolverPaginaActiva();
                if (paginaActiva is null)
                {
                    return;
                }

                await _bannerLock.WaitAsync();
                try
                {
                    var layoutHost = ResolverContenedorHost(paginaActiva.Content);
                    if (layoutHost is null)
                    {
                        return;
                    }

                    // Elimina banners corporativos previos para no apilar superposiciones
                    var anteriores = layoutHost.Children
                        .OfType<Border>()
                        .Where(b => Equals(b.StyleId, "CorporateNotificationBanner"))
                        .ToList();
                    foreach (var previo in anteriores)
                    {
                        layoutHost.Children.Remove(previo);
                    }

                    var banner = ConstruirBannerCorporativo(titulo, mensaje, categoria, layoutHost);
                    layoutHost.Children.Add(banner);

                    banner.Opacity = 0;
                    banner.TranslationY = -20;
                    await Task.WhenAll(
                        banner.FadeToAsync(1, 180, Easing.CubicOut),
                        banner.TranslateToAsync(0, 0, 180, Easing.CubicOut));

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(6));
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            if (layoutHost.Children.Contains(banner))
                            {
                                await banner.FadeToAsync(0, 160, Easing.CubicIn);
                                layoutHost.Children.Remove(banner);
                            }
                        });
                    });
                }
                finally
                {
                    _bannerLock.Release();
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "No se pudo renderizar banner corporativo en primer plano.");
        }
    }

    private static ContentPage? ResolverPaginaActiva()
    {
        if (Shell.Current?.CurrentPage is ContentPage shellPage)
        {
            return shellPage;
        }

        var rootPage = Application.Current?.Windows.FirstOrDefault()?.Page;
        return rootPage switch
        {
            ContentPage cp => cp,
            NavigationPage nav when nav.CurrentPage is ContentPage ncp => ncp,
            _ => null
        };
    }

    private static Layout? ResolverContenedorHost(View? content)
    {
        return content switch
        {
            Grid grid => grid,
            ScrollView scroll when scroll.Content is Layout inner => inner,
            Layout layout => layout,
            _ => null
        };
    }

    private static Border ConstruirBannerCorporativo(string titulo, string mensaje, string categoria, Layout layoutHost)
    {
        var accentHex = categoria switch
        {
            "GEOCERCA" => "#0284C7",
            "BATERIA" => "#D97706",
            "SINCRONIZACION" => "#10B981",
            _ => "#2563EB"
        };

        var banner = new Border
        {
            StyleId = "CorporateNotificationBanner",
            BackgroundColor = Color.FromArgb("#0F172A"),
            Stroke = Color.FromArgb(accentHex),
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 12),
            Margin = new Thickness(16, 14, 16, 0),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start,
            ZIndex = 9999,
            MaximumWidthRequest = 560,
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Offset = new Point(0, 6),
                Radius = 14,
                Opacity = 0.35f
            }
        };

        if (layoutHost is Grid grid)
        {
            var rowCount = Math.Max(1, grid.RowDefinitions.Count);
            var colCount = Math.Max(1, grid.ColumnDefinitions.Count);
            Grid.SetRow(banner, 0);
            Grid.SetColumn(banner, 0);
            Grid.SetRowSpan(banner, rowCount);
            Grid.SetColumnSpan(banner, colCount);
        }

        var contenidoGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(4)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var barraEstado = new BoxView
        {
            Color = Color.FromArgb(accentHex),
            WidthRequest = 4,
            CornerRadius = 2,
            VerticalOptions = LayoutOptions.Fill
        };
        Grid.SetColumn(barraEstado, 0);

        var textos = new VerticalStackLayout { Spacing = 3 };
        textos.Children.Add(new Label
        {
            Text = categoria,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.1,
            TextColor = Color.FromArgb(accentHex)
        });
        textos.Children.Add(new Label
        {
            Text = titulo,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        textos.Children.Add(new Label
        {
            Text = mensaje,
            FontSize = 12,
            TextColor = Color.FromArgb("#CBD5E1"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        Grid.SetColumn(textos, 1);

        var btnCerrar = new Button
        {
            Text = "Cerrar",
            FontSize = 11,
            TextColor = Color.FromArgb("#94A3B8"),
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(6, 2),
            VerticalOptions = LayoutOptions.Start
        };
        btnCerrar.Clicked += (_, _) =>
        {
            if (layoutHost.Children.Contains(banner))
            {
                layoutHost.Children.Remove(banner);
            }
        };
        Grid.SetColumn(btnCerrar, 2);

        contenidoGrid.Children.Add(barraEstado);
        contenidoGrid.Children.Add(textos);
        contenidoGrid.Children.Add(btnCerrar);

        banner.Content = contenidoGrid;
        return banner;
    }
}
