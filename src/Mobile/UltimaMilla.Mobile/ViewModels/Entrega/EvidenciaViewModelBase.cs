using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Services.Media;
using UltimaMilla.Mobile.Views.Components;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

/// <summary>
/// Lo que comparten las pantallas que cierran una parada con prueba: fotografía de evidencia, firma en pantalla,
/// auditoría de ubicación y hora del dispositivo, y modales corporativos de avisos, motivos e ingreso manual.
/// </summary>
public abstract partial class EvidenciaViewModelBase : ObservableObject, ICorporateAvisoModalHost
{
    private TaskCompletionSource<bool>? _avisoTcs;
    private TaskCompletionSource<bool>? _galeriaFallbackTcs;
    private TaskCompletionSource<string?>? _codigoManualTcs;
    private string? _rutaNavegacionAlCerrarAviso;
    private string _motivoTemporalSeleccionado = string.Empty;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneMotivoSeleccionado))]
    [NotifyPropertyChangedFor(nameof(MotivoSelectorTexto))]
    private string _motivoSeleccionado = string.Empty;

    public bool TieneMotivoSeleccionado => !string.IsNullOrWhiteSpace(MotivoSeleccionado);

    public string MotivoSelectorTexto => TieneMotivoSeleccionado
        ? MotivoSeleccionado
        : "Seleccione un motivo...";

    // =========================================================================
    // MODAL CORPORATIVO DE AVISO / RESULTADO
    // =========================================================================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAlgunModalAbierto))]
    private bool _mostrarModalAviso;

    [ObservableProperty]
    private string _tituloModalAviso = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneSubtituloModalAviso))]
    private string _subtituloModalAviso = string.Empty;

    [ObservableProperty]
    private string _mensajeModalAviso = string.Empty;

    [ObservableProperty]
    private string _textoBotonModalAviso = "Entendido";

    public bool TieneSubtituloModalAviso => !string.IsNullOrWhiteSpace(SubtituloModalAviso);

    ICommand ICorporateAvisoModalHost.CerrarModalAvisoCommand => CerrarModalAvisoCommand;

    // =========================================================================
    // MODAL CORPORATIVO SELECTOR DE MOTIVOS
    // =========================================================================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAlgunModalAbierto))]
    private bool _mostrarModalMotivos;

    [ObservableProperty]
    private string _tituloModalMotivos = "Seleccionar motivo";

    [ObservableProperty]
    private string _subtituloModalMotivos = "Elija una opción del catálogo operativo";

    public ObservableCollection<OpcionSeleccionableItem> MotivosModalOpciones { get; } = new();

    // =========================================================================
    // MODAL CORPORATIVO DE GALERÍA ALTERNATIVA (FALLBACK DE CÁMARA)
    // =========================================================================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAlgunModalAbierto))]
    private bool _mostrarModalGaleriaFallback;

    [ObservableProperty]
    private string _mensajeModalGaleriaFallback = string.Empty;

    // =========================================================================
    // MODAL CORPORATIVO DE INGRESO MANUAL DE CÓDIGO
    // =========================================================================
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAlgunModalAbierto))]
    private bool _mostrarModalCodigoManual;

    public bool TieneAlgunModalAbierto =>
        MostrarModalAviso ||
        MostrarModalMotivos ||
        MostrarModalGaleriaFallback ||
        MostrarModalCodigoManual;

    [ObservableProperty]
    private string _tituloModalCodigoManual = "Ingreso manual";

    [ObservableProperty]
    private string _subtituloModalCodigoManual = "Ingrese el código del bulto";

    [ObservableProperty]
    private string _placeholderCodigoManual = "Código del bulto";

    [ObservableProperty]
    private string _codigoManualModalInput = string.Empty;

    /// <summary>La página (que es dueña del lienzo de firma) entrega la imagen de la firma dibujada.</summary>
    public Func<Task<string?>>? ObtenerFirmaAsyncCallback { get; set; }

    public Action? LimpiarFirmaAction { get; set; }

    protected virtual IEnumerable<string> ObtenerOpcionesMotivoModal() => [];

    protected virtual string TituloSelectorMotivo => "Seleccionar motivo";

    protected virtual string SubtituloSelectorMotivo => "Elija una opción del catálogo operativo";

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

    /// <summary>Pide la foto (cámara o galería con modal corporativo) y, si se obtuvo, la deja lista para enviarse.</summary>
    protected async Task CapturarFotoAsync()
    {
        var outcome = await EvidenciaFotografica.IntentarCapturarDesdeCamaraAsync();

        if (!string.IsNullOrWhiteSpace(outcome.MotivoFallbackGaleria))
        {
            var usarGaleria = await ConfirmarGaleriaFallbackAsync(outcome.MotivoFallbackGaleria);
            if (!usarGaleria)
                return;

            outcome = await EvidenciaFotografica.IntentarSeleccionarDesdeGaleriaAsync();
        }

        if (!string.IsNullOrWhiteSpace(outcome.ErrorTitulo))
        {
            await AvisarAsync(
                outcome.ErrorTitulo,
                outcome.ErrorMensaje ?? "No fue posible obtener la fotografía.");
            return;
        }

        if (outcome.Imagen is null)
            return;

        FotoEvidenciaBase64 = outcome.Imagen.Base64DataUri;
        FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(outcome.Imagen.CompressedBytes));
        TieneFoto = true;
        FotoStatusText = outcome.Imagen.ResumenCompresion;
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

    protected void SincronizarOpcionesMotivoTarjetas()
    {
        var opciones = ObtenerOpcionesMotivoModal()
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        MotivosModalOpciones.Clear();
        foreach (var opcion in opciones)
        {
            var esActual = !string.IsNullOrWhiteSpace(MotivoSeleccionado) &&
                           string.Equals(opcion, MotivoSeleccionado, StringComparison.OrdinalIgnoreCase);
            MotivosModalOpciones.Add(new OpcionSeleccionableItem(opcion, esActual));
        }
    }

    [RelayCommand]
    private void SeleccionarMotivoDirecto(OpcionSeleccionableItem? opcion)
    {
        if (opcion is null)
            return;

        MotivoSeleccionado = opcion.Texto;
        _motivoTemporalSeleccionado = opcion.Texto;
        foreach (var item in MotivosModalOpciones)
        {
            item.EsSeleccionada = string.Equals(item.Texto, opcion.Texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void AbrirSelectorMotivo()
    {
        if (IsBusy)
            return;

        var opciones = ObtenerOpcionesMotivoModal()
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (opciones.Count == 0)
            return;

        MotivosModalOpciones.Clear();
        _motivoTemporalSeleccionado = MotivoSeleccionado;

        foreach (var opcion in opciones)
        {
            var esActual = !string.IsNullOrWhiteSpace(_motivoTemporalSeleccionado) &&
                           string.Equals(opcion, _motivoTemporalSeleccionado, StringComparison.OrdinalIgnoreCase);
            MotivosModalOpciones.Add(new OpcionSeleccionableItem(opcion, esActual));
        }

        TituloModalMotivos = TituloSelectorMotivo;
        SubtituloModalMotivos = SubtituloSelectorMotivo;
        MostrarModalMotivos = true;
    }

    [RelayCommand]
    private void SeleccionarMotivoModal(OpcionSeleccionableItem? opcion)
    {
        if (opcion is null)
            return;

        _motivoTemporalSeleccionado = opcion.Texto;
        foreach (var item in MotivosModalOpciones)
        {
            item.EsSeleccionada = string.Equals(item.Texto, opcion.Texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void ConfirmarMotivoModal()
    {
        if (!string.IsNullOrWhiteSpace(_motivoTemporalSeleccionado))
        {
            MotivoSeleccionado = _motivoTemporalSeleccionado;
        }
        else if (MotivosModalOpciones.Count > 0)
        {
            MotivoSeleccionado = MotivosModalOpciones[0].Texto;
        }

        MostrarModalMotivos = false;
    }

    [RelayCommand]
    private void CerrarMotivoModal()
    {
        MostrarModalMotivos = false;
    }

    protected Task AvisarAsync(
        string titulo,
        string mensaje,
        string subtitulo = "",
        string textoBoton = "Entendido",
        string? navegarAlCerrarRuta = null)
    {
        _avisoTcs?.TrySetResult(true);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _avisoTcs = tcs;
        _rutaNavegacionAlCerrarAviso = navegarAlCerrarRuta;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TituloModalAviso = titulo;
            SubtituloModalAviso = subtitulo;
            MensajeModalAviso = mensaje;
            TextoBotonModalAviso = textoBoton;
            MostrarModalAviso = true;
        });

        return tcs.Task;
    }

    [RelayCommand]
    private async Task CerrarModalAvisoAsync()
    {
        MostrarModalAviso = false;
        var ruta = _rutaNavegacionAlCerrarAviso;
        _rutaNavegacionAlCerrarAviso = null;
        _avisoTcs?.TrySetResult(true);

        if (!string.IsNullOrWhiteSpace(ruta) && Shell.Current is not null)
        {
            await Shell.Current.GoToAsync(ruta);
        }
    }

    private Task<bool> ConfirmarGaleriaFallbackAsync(string motivo)
    {
        _galeriaFallbackTcs?.TrySetResult(false);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _galeriaFallbackTcs = tcs;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            MensajeModalGaleriaFallback = $"{motivo}\n\n¿Desea seleccionar una imagen de la galería?";
            MostrarModalGaleriaFallback = true;
        });

        return tcs.Task;
    }

    [RelayCommand]
    private void ConfirmarGaleriaFallback()
    {
        MostrarModalGaleriaFallback = false;
        _galeriaFallbackTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void CancelarGaleriaFallback()
    {
        MostrarModalGaleriaFallback = false;
        _galeriaFallbackTcs?.TrySetResult(false);
    }

    protected Task<string?> SolicitarCodigoManualAsync(
        string titulo = "Ingreso manual",
        string subtitulo = "Ingrese el código del bulto",
        string placeholder = "Código del bulto")
    {
        _codigoManualTcs?.TrySetResult(null);
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _codigoManualTcs = tcs;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TituloModalCodigoManual = titulo;
            SubtituloModalCodigoManual = subtitulo;
            PlaceholderCodigoManual = placeholder;
            CodigoManualModalInput = string.Empty;
            MostrarModalCodigoManual = true;
        });

        return tcs.Task;
    }

    [RelayCommand]
    private void ConfirmarCodigoManualModal()
    {
        MostrarModalCodigoManual = false;
        var valor = CodigoManualModalInput?.Trim();
        _codigoManualTcs?.TrySetResult(string.IsNullOrWhiteSpace(valor) ? null : valor);
    }

    [RelayCommand]
    private void CancelarCodigoManualModal()
    {
        MostrarModalCodigoManual = false;
        _codigoManualTcs?.TrySetResult(null);
    }
}
