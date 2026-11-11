using UltimaMilla.Mobile.Controls;
using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class RegistroEntregaPage : ContentPage
{
    private readonly SignaturePadDrawable _drawable = new();

    public RegistroEntregaPage() : this(
        Application.Current?.Handler?.MauiContext?.Services.GetService<RegistroEntregaViewModel>()
        ?? new RegistroEntregaViewModel())
    {
    }

    public RegistroEntregaPage(RegistroEntregaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        FirmaGraphicsView.Drawable = _drawable;

        FirmaGraphicsView.Loaded += (s, e) =>
        {
#if ANDROID
            ConfigurarTouchNativoFirma();
#endif
        };

        viewModel.LimpiarFirmaAction = () =>
        {
            _drawable.Limpiar();
            FirmaGraphicsView.Invalidate();
        };

        viewModel.ObtenerFirmaAsyncCallback = async () =>
        {
            if (!_drawable.TieneFirma)
                return null;

            try
            {
                var screenshot = await FirmaGraphicsView.CaptureAsync();
                if (screenshot is not null)
                {
                    await using var stream = await screenshot.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    if (bytes.Length > 0)
                    {
                        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RegistroEntregaPage] CaptureAsync no disponible: {ex.Message}");
            }

            // Fallback de rasterizado de alta fidelidad en memoria
            var ancho = (int)(FirmaGraphicsView.Width > 0 ? FirmaGraphicsView.Width : 360);
            var alto = (int)(FirmaGraphicsView.Height > 0 ? FirmaGraphicsView.Height : 180);
            return _drawable.GenerarPngBase64EnMemoria(ancho, alto);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is RegistroEntregaViewModel vm)
        {
            _ = vm.ActualizarUbicacionYFechaAsync();
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if ANDROID
        ConfigurarTouchNativoFirma();
#endif
    }

    private void DeshabilitarScrollPadre()
    {
        FormularioScrollView.Orientation = ScrollOrientation.Neither;
#if ANDROID
        if (FirmaGraphicsView.Handler?.PlatformView is global::Android.Views.View nativeView)
        {
            nativeView.Parent?.RequestDisallowInterceptTouchEvent(true);
        }
#endif
    }

    private void HabilitarScrollPadre()
    {
        FormularioScrollView.Orientation = ScrollOrientation.Vertical;
#if ANDROID
        if (FirmaGraphicsView.Handler?.PlatformView is global::Android.Views.View nativeView)
        {
            nativeView.Parent?.RequestDisallowInterceptTouchEvent(false);
        }
#endif
    }

#if ANDROID
    private void ConfigurarTouchNativoFirma()
    {
        if (FirmaGraphicsView.Handler?.PlatformView is global::Android.Views.View nativeView)
        {
            nativeView.Touch -= OnNativeFirmaTouch;
            nativeView.Touch += OnNativeFirmaTouch;
        }
    }

    private void OnNativeFirmaTouch(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        if (sender is global::Android.Views.View nativeView && e.Event != null)
        {
            switch (e.Event.ActionMasked)
            {
                case global::Android.Views.MotionEventActions.Down:
                case global::Android.Views.MotionEventActions.Move:
                    nativeView.Parent?.RequestDisallowInterceptTouchEvent(true);
                    FormularioScrollView.Orientation = ScrollOrientation.Neither;
                    break;
                case global::Android.Views.MotionEventActions.Up:
                case global::Android.Views.MotionEventActions.Cancel:
                    nativeView.Parent?.RequestDisallowInterceptTouchEvent(false);
                    FormularioScrollView.Orientation = ScrollOrientation.Vertical;
                    break;
            }
        }
        e.Handled = false;
    }
#endif

    private void OnSignatureStartInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
            DeshabilitarScrollPadre();

            if (e.Touches == null || e.Touches.Length == 0)
                return;

            var point = e.Touches[0];
            if (float.IsNaN(point.X) || float.IsNaN(point.Y))
                return;

            _drawable.IniciarTrazo(point);

            if (BindingContext is RegistroEntregaViewModel vm)
            {
                vm.TieneFirma = true;
                vm.FirmaStatusText = "Firma digital en curso...";
            }

            FirmaGraphicsView.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaPage] Error en StartInteraction: {ex.Message}");
        }
    }

    private void OnSignatureDragInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
#if ANDROID
            if (FirmaGraphicsView.Handler?.PlatformView is global::Android.Views.View nativeView)
            {
                nativeView.Parent?.RequestDisallowInterceptTouchEvent(true);
            }
#endif
            if (e.Touches == null || e.Touches.Length == 0)
                return;

            var point = e.Touches[0];
            if (float.IsNaN(point.X) || float.IsNaN(point.Y))
                return;

            _drawable.AgregarPunto(point);
            FirmaGraphicsView.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaPage] Error en DragInteraction: {ex.Message}");
        }
    }

    private void OnSignatureEndInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
            _drawable.FinalizarTrazo();
            FirmaGraphicsView.Invalidate();

            if (BindingContext is RegistroEntregaViewModel vm)
            {
                vm.TieneFirma = _drawable.TieneFirma;
                int puntos = _drawable.Trazos.Sum(t => t.Count);
                vm.FirmaStatusText = _drawable.TieneFirma
                    ? $"Firma digital capturada ({puntos} puntos)"
                    : "Toque o dibuje para firmar";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaPage] Error en EndInteraction: {ex.Message}");
        }
        finally
        {
            HabilitarScrollPadre();
        }
    }
}
