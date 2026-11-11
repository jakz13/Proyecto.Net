using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Services.Media;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

/// <summary>
/// Lo que comparten las pantallas que cierran una parada con prueba: fotografía de evidencia, firma en pantalla y
/// auditoría de ubicación y hora del dispositivo. Nada de esto se inventa: si no hay GPS, la posición queda vacía.
/// </summary>
public abstract partial class EvidenciaViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _tieneFirma;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoTieneFoto))]
    private bool _tieneFoto;

    public bool NoTieneFoto => !TieneFoto;

    [ObservableProperty]
    private ImageSource? _fotoPreviewSource;

    [ObservableProperty]
    private string? _fotoEvidenciaBase64;

    [ObservableProperty]
    private string _fotoStatusText = "Ninguna foto adjunta";

    [ObservableProperty]
    private string _firmaStatusText = "Toque o dibuje para firmar";

    [ObservableProperty]
    private double? _latitudActual;

    [ObservableProperty]
    private double? _longitudActual;

    [ObservableProperty]
    private string _posicionGpsTexto = "Consultando ubicación GPS...";

    [ObservableProperty]
    private string _fechaHoraCapturaTexto = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

    [ObservableProperty]
    private DateTime _fechaHoraDispositivoUtc = DateTime.UtcNow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    /// <summary>La página (que es dueña del lienzo de firma) entrega la imagen de la firma dibujada.</summary>
    public Func<Task<string?>>? ObtenerFirmaAsyncCallback { get; set; }

    public Action? LimpiarFirmaAction { get; set; }

    /// <summary>Consulta la posición real del dispositivo y fija la fecha y hora de la captura.</summary>
    public async Task ActualizarUbicacionYFechaAsync()
    {
        FechaHoraDispositivoUtc = DateTime.UtcNow;
        FechaHoraCapturaTexto = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        var ubicacion = await UltimaMilla.Mobile.Services.Tracking.UbicacionDispositivo.ObtenerAsync();
        if (ubicacion is null)
        {
            LatitudActual = null;
            LongitudActual = null;
            PosicionGpsTexto = "GPS no disponible: se registrará sin posición";
            return;
        }

        LatitudActual = ubicacion.Latitude;
        LongitudActual = ubicacion.Longitude;
        PosicionGpsTexto = $"Lat: {ubicacion.Latitude:F5}, Lon: {ubicacion.Longitude:F5}";
    }

    [RelayCommand]
    private async Task AbrirCamaraAsync()
    {
        if (IsBusy)
            return;

        await CapturarFotoAsync();
    }

    /// <summary>Pide la foto (cámara o galería) y, si se obtuvo, la deja lista para enviarse.</summary>
    protected async Task CapturarFotoAsync()
    {
        var resultado = await EvidenciaFotografica.CapturarAsync();
        if (resultado is null)
            return;

        FotoEvidenciaBase64 = resultado.Base64DataUri;
        FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(resultado.CompressedBytes));
        TieneFoto = true;
        FotoStatusText = resultado.ResumenCompresion;
    }

    [RelayCommand]
    private void QuitarFoto()
    {
        FotoEvidenciaBase64 = null;
        FotoPreviewSource = null;
        TieneFoto = false;
        FotoStatusText = "Ninguna foto adjunta";
    }

    [RelayCommand]
    private void LimpiarFirma()
    {
        LimpiarFirmaAction?.Invoke();
        TieneFirma = false;
        FirmaStatusText = "Toque o dibuje para firmar";
    }

    /// <summary>Imagen de la firma dibujada, o <c>null</c> si no se firmó o no se pudo capturar (nunca una imagen de relleno).</summary>
    protected async Task<string?> ObtenerFirmaBase64Async()
    {
        if (!TieneFirma || ObtenerFirmaAsyncCallback is null)
            return null;

        var firma = await ObtenerFirmaAsyncCallback.Invoke();
        return string.IsNullOrWhiteSpace(firma) ? null : firma;
    }

    protected static Task AvisarAsync(string titulo, string mensaje) =>
        Shell.Current is null ? Task.CompletedTask : Shell.Current.DisplayAlertAsync(titulo, mensaje, "Entendido");
}
