using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Controls;

/// <summary>
/// Conecta un lienzo de firma (<see cref="GraphicsView"/> con <see cref="SignaturePadDrawable"/>) con un ViewModel de evidencia:
/// dibuja los trazos, informa si hay firma y entrega la imagen resultante. Las pantallas nuevas lo usan en lugar de repetir el código.
/// </summary>
public static class FirmaPadConector
{
    public static SignaturePadDrawable Conectar(GraphicsView lienzo, EvidenciaViewModelBase vm)
    {
        var dibujo = new SignaturePadDrawable();
        lienzo.Drawable = dibujo;

#if ANDROID
        lienzo.Loaded += (_, _) => ConfigurarTouchNativo(lienzo);
        lienzo.HandlerChanged += (_, _) => ConfigurarTouchNativo(lienzo);
#endif

        vm.LimpiarFirmaAction = () =>
        {
            dibujo.Limpiar();
            lienzo.Invalidate();
        };

        vm.ObtenerFirmaAsyncCallback = async () =>
        {
            if (!dibujo.TieneFirma)
                return null;

            try
            {
                var captura = await lienzo.CaptureAsync();
                if (captura is not null)
                {
                    await using var flujo = await captura.OpenReadAsync();
                    using var memoria = new MemoryStream();
                    await flujo.CopyToAsync(memoria);
                    if (memoria.Length > 0)
                    {
                        return $"data:image/png;base64,{Convert.ToBase64String(memoria.ToArray())}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirmaPadConector] CaptureAsync no disponible: {ex.Message}");
            }

            // Respaldo: se dibuja la firma en memoria con el mismo trazo
            var ancho = (int)(lienzo.Width > 0 ? lienzo.Width : 360);
            var alto = (int)(lienzo.Height > 0 ? lienzo.Height : 180);
            return dibujo.GenerarPngBase64EnMemoria(ancho, alto);
        };

        lienzo.StartInteraction += (_, e) =>
        {
            BloquearScrollPadre(lienzo, true);

            var punto = PrimerPunto(e);
            if (punto is null)
                return;

            dibujo.IniciarTrazo(punto.Value);
            vm.TieneFirma = true;
            vm.FirmaStatusText = "Firma en curso...";
            lienzo.Invalidate();
        };

        lienzo.DragInteraction += (_, e) =>
        {
            BloquearScrollPadre(lienzo, true);

            var punto = PrimerPunto(e);
            if (punto is null)
                return;

            dibujo.AgregarPunto(punto.Value);
            lienzo.Invalidate();
        };

        lienzo.EndInteraction += (_, _) =>
        {
            try
            {
                dibujo.FinalizarTrazo();
                lienzo.Invalidate();
                vm.TieneFirma = dibujo.TieneFirma;
                vm.FirmaStatusText = dibujo.TieneFirma ? "Firma capturada" : "Toque o dibuje para firmar";
            }
            finally
            {
                BloquearScrollPadre(lienzo, false);
            }
        };

        return dibujo;
    }

    private static void BloquearScrollPadre(GraphicsView lienzo, bool bloquear)
    {
#if ANDROID
        if (lienzo.Handler?.PlatformView is global::Android.Views.View nativeView)
        {
            nativeView.Parent?.RequestDisallowInterceptTouchEvent(bloquear);
        }
#endif
        Element? parent = lienzo.Parent;
        while (parent != null)
        {
            if (parent is ScrollView scrollView)
            {
                scrollView.Orientation = bloquear ? ScrollOrientation.Neither : ScrollOrientation.Vertical;
                break;
            }
            parent = parent.Parent;
        }
    }

#if ANDROID
    private static void ConfigurarTouchNativo(GraphicsView lienzo)
    {
        if (lienzo.Handler?.PlatformView is global::Android.Views.View nativeView)
        {
            nativeView.Touch -= OnNativeFirmaTouch;
            nativeView.Touch += OnNativeFirmaTouch;
        }
    }

    private static void OnNativeFirmaTouch(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        if (sender is global::Android.Views.View nativeView && e.Event != null)
        {
            switch (e.Event.ActionMasked)
            {
                case global::Android.Views.MotionEventActions.Down:
                case global::Android.Views.MotionEventActions.Move:
                    nativeView.Parent?.RequestDisallowInterceptTouchEvent(true);
                    break;
                case global::Android.Views.MotionEventActions.Up:
                case global::Android.Views.MotionEventActions.Cancel:
                    nativeView.Parent?.RequestDisallowInterceptTouchEvent(false);
                    break;
            }
        }
        e.Handled = false;
    }
#endif

    private static PointF? PrimerPunto(TouchEventArgs e)
    {
        if (e.Touches is null || e.Touches.Length == 0)
            return null;

        var punto = e.Touches[0];
        return float.IsNaN(punto.X) || float.IsNaN(punto.Y) ? null : punto;
    }
}
