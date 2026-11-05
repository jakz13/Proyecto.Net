using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace UltimaMilla.Mobile.Views.Carga;

public partial class EscanerCodigoPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new();
    private int _procesandoLectura;

    public Task<string?> ResultadoEscaneoTask => _tcs.Task;

    public EscanerCodigoPage()
    {
        InitializeComponent();

        BarcodeReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.All,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Interlocked.Exchange(ref _procesandoLectura, 0);
        BarcodeReader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        BarcodeReader.IsTorchOn = false;
        _tcs.TrySetResult(null);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        if (Interlocked.Exchange(ref _procesandoLectura, 1) != 0)
            return;

        var primerCodigo = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(primerCodigo))
        {
            Interlocked.Exchange(ref _procesandoLectura, 0);
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                BarcodeReader.IsDetecting = false;
                BarcodeReader.IsTorchOn = false;

                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(100));
                }
                catch
                {
                    // Silente si la plataforma no soporta motor háptico
                }

                _tcs.TrySetResult(primerCodigo.Trim());
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EscanerCodigoPage] Error al procesar escaneo: {ex.Message}");
                _tcs.TrySetResult(primerCodigo.Trim());
            }
        });
    }

    private void OnAlternarLinternaClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsTorchOn = !BarcodeReader.IsTorchOn;
        BtnLinterna.Text = BarcodeReader.IsTorchOn ? "Apagar Linterna" : "Alternar Linterna";
    }

    private async void OnCancelarClicked(object? sender, EventArgs e)
    {
        BarcodeReader.IsDetecting = false;
        BarcodeReader.IsTorchOn = false;
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }
}

