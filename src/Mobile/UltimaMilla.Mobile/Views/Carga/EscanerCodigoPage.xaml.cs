using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

namespace UltimaMilla.Mobile.Views.Carga;

public partial class EscanerCodigoPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _procesandoLectura;
    private string? _codigoDetectado;

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
        _codigoDetectado = null;
        Interlocked.Exchange(ref _procesandoLectura, 0);
        BarcodeReader.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        BarcodeReader.IsDetecting = false;
        BarcodeReader.IsTorchOn = false;

        if (_codigoDetectado is null && Volatile.Read(ref _procesandoLectura) == 0)
        {
            _tcs.TrySetResult(null);
        }
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

        _codigoDetectado = primerCodigo.Trim();

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

                await Navigation.PopModalAsync();
                _tcs.TrySetResult(_codigoDetectado);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EscanerCodigoPage] Error al procesar escaneo: {ex.Message}");
                _tcs.TrySetResult(_codigoDetectado);
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

