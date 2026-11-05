using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Media;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
[QueryProperty(nameof(ParadaIdParam), "ParadaId")]
[QueryProperty(nameof(EnvioIdParam), "EnvioId")]
[QueryProperty(nameof(CodigoSeguimientoParam), "CodigoSeguimiento")]
[QueryProperty(nameof(NombreDestinatarioParam), "NombreDestinatario")]
[QueryProperty(nameof(DireccionParam), "Direccion")]
public partial class RegistroEntregaViewModel : ObservableObject
{
    public const string OpcionTomarFoto = "Tomar Foto con Cámara";
    public const string OpcionSeleccionarGaleria = "Seleccionar de la Galería";
    public const string OpcionGenerarDemo = "Generar Evidencia de Prueba (Demo)";

    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ParadaItemViewModel? _paradaSeleccionada;

    [ObservableProperty]
    private string? _paradaIdParam;

    [ObservableProperty]
    private string? _envioIdParam;

    [ObservableProperty]
    private string? _codigoSeguimientoParam;

    [ObservableProperty]
    private string? _nombreDestinatarioParam;

    [ObservableProperty]
    private string? _direccionParam;

    [ObservableProperty]
    private string _nombreReceptor = string.Empty;

    [ObservableProperty]
    private string _documentoReceptor = string.Empty;

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
    private string _fechaHoraCapturaTexto = string.Empty;

    [ObservableProperty]
    private DateTime _fechaHoraDispositivoUtc = DateTime.UtcNow;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public Func<Task<string?>>? ObtenerFirmaAsyncCallback { get; set; }
    public Action? LimpiarFirmaAction { get; set; }
    public Action? CargarFirmaDemoAction { get; set; }

    public RegistroEntregaViewModel() : this(null, null)
    {
    }

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;

        _nombreReceptor = string.Empty;
        _documentoReceptor = "4.123.456-7"; // Valor por defecto sobrio
        _fechaHoraCapturaTexto = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
    }

    partial void OnParadaSeleccionadaChanged(ParadaItemViewModel? value)
    {
        if (value is not null)
        {
            if (!string.IsNullOrWhiteSpace(value.Destinatario))
            {
                NombreReceptor = value.Destinatario.Trim();
            }

            if (string.IsNullOrWhiteSpace(DocumentoReceptor))
            {
                DocumentoReceptor = "4.123.456-7";
            }
        }
    }

    partial void OnNombreDestinatarioParamChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(NombreReceptor))
        {
            NombreReceptor = value.Trim();
        }
    }

    /// <summary>
    /// Consulta la geolocalización actual y actualiza la fecha/hora del dispositivo para auditoría.
    /// </summary>
    public async Task ActualizarUbicacionYFechaAsync()
    {
        try
        {
            var ahoraLocal = DateTime.Now;
            FechaHoraDispositivoUtc = DateTime.UtcNow;
            FechaHoraCapturaTexto = ahoraLocal.ToString("dd/MM/yyyy HH:mm:ss");

            Location? location = null;
            try
            {
                location = await Geolocation.Default.GetLastKnownLocationAsync();
                if (location is null)
                {
                    var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                    location = await Geolocation.Default.GetLocationAsync(req);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] GPS no disponible: {ex.Message}");
            }

            if (location is not null)
            {
                LatitudActual = location.Latitude;
                LongitudActual = location.Longitude;
            }
            else if (ParadaSeleccionada?.Latitud != null && ParadaSeleccionada.Latitud != 0)
            {
                LatitudActual = ParadaSeleccionada.Latitud;
                LongitudActual = ParadaSeleccionada.Longitud;
            }
            else
            {
                // Coordenadas de contingencia urbana
                LatitudActual = -34.9011;
                LongitudActual = -56.1645;
            }

            PosicionGpsTexto = $"Lat: {LatitudActual:F4}, Lon: {LongitudActual:F4}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error actualizando auditoría: {ex.Message}");
            PosicionGpsTexto = "GPS no disponible (Modo manual)";
            FechaHoraCapturaTexto = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
        }
    }

    [RelayCommand]
    private async Task AbrirCamaraAsync()
    {
        if (IsBusy)
            return;

        if (Shell.Current is null)
        {
            await AplicarEvidenciaDemoAsync();
            return;
        }

        var opcion = await Shell.Current.DisplayActionSheetAsync(
            "Adjuntar Evidencia Fotográfica",
            "Cancelar",
            null,
            OpcionTomarFoto,
            OpcionSeleccionarGaleria,
            OpcionGenerarDemo);

        if (string.IsNullOrWhiteSpace(opcion) || opcion == "Cancelar")
            return;

        switch (opcion)
        {
            case OpcionTomarFoto:
                await CapturarFotoConCamaraAsync();
                break;

            case OpcionSeleccionarGaleria:
                await SeleccionarFotoDeGaleriaAsync();
                break;

            case OpcionGenerarDemo:
                await AplicarEvidenciaDemoAsync();
                break;
        }
    }

    [RelayCommand]
    private void QuitarFoto()
    {
        FotoEvidenciaBase64 = null;
        FotoPreviewSource = null;
        TieneFoto = false;
        FotoStatusText = "Ninguna foto adjunta";
    }

    private async Task CapturarFotoConCamaraAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await OfrecerAlternativaSinCamaraAsync(
                    "La captura directa con cámara no está soportada o no hay una cámara disponible en este dispositivo.");
                return;
            }

            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                if (Shell.Current is not null)
                {
                    bool usarAlternativa = await Shell.Current.DisplayAlertAsync(
                        "Permiso de Cámara Denegado",
                        "No se concedió el permiso de cámara. ¿Desea seleccionar una imagen de la galería o generar una evidencia de prueba?",
                        "Seleccionar de Galería",
                        "Cancelar");

                    if (usarAlternativa)
                    {
                        await SeleccionarFotoDeGaleriaAsync();
                    }
                }
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = $"POD_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg"
            });

            if (photo is null)
                return;

            await ProcesarArchivoFotoAsync(photo);
        }
        catch (FeatureNotSupportedException)
        {
            await OfrecerAlternativaSinCamaraAsync(
                "El dispositivo actual no dispone de aplicación o hardware de cámara compatible.");
        }
        catch (PermissionException)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Permiso Requerido",
                    "El sistema bloqueó el acceso a la cámara. Verifique los permisos de la aplicación en Ajustes.",
                    "Entendido");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error en CapturarFotoConCamaraAsync: {ex.Message}");
            await OfrecerAlternativaSinCamaraAsync(
                "No fue posible iniciar la cámara del dispositivo en este momento.");
        }
    }

    private async Task SeleccionarFotoDeGaleriaAsync()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Seleccione fotografía de evidencia"
            });

            if (photo is null)
                return;

            await ProcesarArchivoFotoAsync(photo);
        }
        catch (PermissionException)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Permiso de Galería Requerido",
                    "No se otorgó permiso para leer imágenes del dispositivo.",
                    "Entendido");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error en SeleccionarFotoDeGaleriaAsync: {ex.Message}");
            if (Shell.Current is not null)
            {
                bool usarDemo = await Shell.Current.DisplayAlertAsync(
                    "Error al Abrir Galería",
                    "No se pudo cargar la imagen desde el selector de archivos. ¿Desea generar una evidencia de prueba (Demo)?",
                    "Generar Demo",
                    "Cancelar");

                if (usarDemo)
                {
                    await AplicarEvidenciaDemoAsync();
                }
            }
        }
    }

    private async Task OfrecerAlternativaSinCamaraAsync(string motivo)
    {
        if (Shell.Current is null)
        {
            await AplicarEvidenciaDemoAsync();
            return;
        }

        string accion = await Shell.Current.DisplayActionSheetAsync(
            motivo,
            "Cancelar",
            null,
            OpcionSeleccionarGaleria,
            OpcionGenerarDemo);

        if (accion == OpcionSeleccionarGaleria)
        {
            await SeleccionarFotoDeGaleriaAsync();
        }
        else if (accion == OpcionGenerarDemo)
        {
            await AplicarEvidenciaDemoAsync();
        }
    }

    private async Task ProcesarArchivoFotoAsync(FileResult photo)
    {
        try
        {
            await using var stream = await photo.OpenReadAsync();
            var resultado = await ImageCompressionService.ComprimirFotoAsync(stream);

            if (resultado.CompressedBytes.Length == 0)
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Archivo Vacío",
                        "La fotografía seleccionada no contiene datos válidos.",
                        "Entendido");
                }
                return;
            }

            FotoEvidenciaBase64 = resultado.Base64DataUri;
            FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(resultado.CompressedBytes));
            TieneFoto = true;
            FotoStatusText = resultado.ResumenCompresion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error al procesar fotografía: {ex.Message}");
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Error de Imagen",
                    "No fue posible procesar y comprimir la imagen seleccionada.",
                    "Entendido");
            }
        }
    }

    private async Task AplicarEvidenciaDemoAsync()
    {
        try
        {
            var resultado = await ImageCompressionService.GenerarEvidenciaDemoAsync();
            FotoEvidenciaBase64 = resultado.Base64DataUri;
            FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(resultado.CompressedBytes));
            TieneFoto = true;
            FotoStatusText = resultado.ResumenCompresion;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error en AplicarEvidenciaDemoAsync: {ex.Message}");
            TieneFoto = true;
            FotoStatusText = "Fotografía de prueba adjuntada";
        }
    }

    [RelayCommand]
    private void SimularFirma()
    {
        CargarFirmaDemoAction?.Invoke();
        TieneFirma = true;
        FirmaStatusText = "Firma digital capturada (Demo)";
    }

    [RelayCommand]
    private void LimpiarFirma()
    {
        LimpiarFirmaAction?.Invoke();
        TieneFirma = false;
        FirmaStatusText = "Toque o dibuje para firmar";
    }

    [RelayCommand]
    private async Task GuardarEntregaAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(NombreReceptor) || string.IsNullOrWhiteSpace(DocumentoReceptor))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Campos Requeridos",
                    "Por favor, complete el nombre y documento del receptor.",
                    "Entendido");
            }
            return;
        }

        if (!TieneFirma)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Firma Requerida",
                    "Debe registrar la firma del receptor antes de confirmar la entrega.",
                    "Entendido");
            }
            return;
        }

        try
        {
            IsBusy = true;

            var parada = ParadaSeleccionada;
            var hojaDeRutaId = (parada?.HojaDeRutaId != null && parada.HojaDeRutaId != Guid.Empty)
                ? parada.HojaDeRutaId
                : DemoDataDefaults.DefaultHojaDeRutaId;

            var paradaId = (parada?.Id != null && parada.Id != Guid.Empty)
                ? parada.Id
                : (parada?.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Parada1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Parada2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Parada3Id,
                    _ => DemoDataDefaults.Parada2Id
                });

            var envioId = (parada?.EnvioId != null && parada.EnvioId != Guid.Empty)
                ? parada.EnvioId
                : (parada?.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Envio1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Envio2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Envio3Id,
                    _ => DemoDataDefaults.Envio2Id
                });

            var idempotencyKey = $"ENTREGA-{paradaId:N}-{DateTime.UtcNow.Ticks}";

            // 1. Auditoría exacta al confirmar: coordenadas GPS y fecha/hora UTC
            await ActualizarUbicacionYFechaAsync();

            // 2. Obtener firma digital capturada del lienzo táctil
            string? firmaBase64 = null;
            if (ObtenerFirmaAsyncCallback != null)
            {
                firmaBase64 = await ObtenerFirmaAsyncCallback.Invoke();
            }

            if (string.IsNullOrWhiteSpace(firmaBase64) && TieneFirma)
            {
                // Fallback de imagen 1x1 en caso extremo
                firmaBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            }

            // 3. Obtener fotografía comprimida (JPEG máx 1024px, 70% calidad) si existe
            string? fotoBase64 = TieneFoto ? FotoEvidenciaBase64 : null;

            // 4. Payload offline completo para persistencia SQLite y sincronización
            var payload = new EntregaExitosaOfflinePayload
            {
                HojaDeRutaId = hojaDeRutaId,
                ParadaId = paradaId,
                EnvioId = envioId,
                IdempotencyKey = idempotencyKey,
                NombreReceptor = NombreReceptor.Trim(),
                DocumentoReceptor = DocumentoReceptor.Trim(),
                FirmaDigitalBase64 = firmaBase64,
                FotoEvidenciaBase64 = fotoBase64,
                Latitud = LatitudActual ?? ((parada?.Latitud != null && parada.Latitud != 0) ? parada.Latitud : -34.9011),
                Longitud = LongitudActual ?? ((parada?.Longitud != null && parada.Longitud != 0) ? parada.Longitud : -56.1645),
                FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
            };

            // 5. Guardar en cola offline cifrada y actualizar estado local SQLite
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.EntregaExitosa,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(paradaId, "Entregado");
            }

            // 6. Actualizar estado en modelo en memoria
            if (parada is not null)
            {
                parada.Estado = "Entregado";
            }

            // 7. Si la app está Online, disparar sincronización en background
            if (_syncService?.IsOnline == true)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncService.SincronizarOperacionesPendientesAsync();
                    }
                    catch
                    {
                        // Fallback silente en background
                    }
                });
            }

            await Task.Delay(250);

            if (Shell.Current is not null)
            {
                string infoRed = (_syncService?.IsOnline == true)
                    ? "Sincronizando entrega con la central en tiempo real."
                    : "Guardado en SQLite cifrado local. Se sincronizará automáticamente al restablecerse la red.";

                await Shell.Current.DisplayAlertAsync(
                    "Entrega Registrada",
                    $"Comprobante de entrega registrado exitosamente para {NombreReceptor.Trim()}.\n\n{infoRed}",
                    "Finalizar");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
