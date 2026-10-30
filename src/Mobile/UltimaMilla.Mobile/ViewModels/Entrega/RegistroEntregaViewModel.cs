using System.IO.Compression;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
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
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    public RegistroEntregaViewModel() : this(null, null)
    {
    }

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;

        _nombreReceptor = "Lucía Fernández";
        _documentoReceptor = "4.567.890-1";
    }

    [RelayCommand]
    private async Task AbrirCamaraAsync()
    {
        if (IsBusy)
            return;

        if (Shell.Current is null)
        {
            AplicarEvidenciaDemo();
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
                AplicarEvidenciaDemo();
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
                    AplicarEvidenciaDemo();
                }
            }
        }
    }

    private async Task OfrecerAlternativaSinCamaraAsync(string motivo)
    {
        if (Shell.Current is null)
        {
            AplicarEvidenciaDemo();
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
            AplicarEvidenciaDemo();
        }
    }

    private async Task ProcesarArchivoFotoAsync(FileResult photo)
    {
        await using var stream = await photo.OpenReadAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Archivo vacío",
                    "La fotografía seleccionada no contiene datos válidos.",
                    "Entendido");
            }
            return;
        }

        string mimeType = DeterminarMimeType(photo.ContentType, photo.FileName);
        FotoEvidenciaBase64 = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(bytes));
        TieneFoto = true;

        double sizeKb = Math.Max(0.1, Math.Round(bytes.Length / 1024.0, 1));
        FotoStatusText = $"Fotografía adjuntada correctamente ({sizeKb:0.#} KB)";
    }

    private void AplicarEvidenciaDemo()
    {
        var base64DataUri = GenerarPngDemoBase64(out var pngBytes);
        FotoEvidenciaBase64 = base64DataUri;
        FotoPreviewSource = ImageSource.FromStream(() => new MemoryStream(pngBytes));
        TieneFoto = true;

        double sizeKb = Math.Max(0.1, Math.Round(pngBytes.Length / 1024.0, 1));
        FotoStatusText = $"Fotografía adjuntada correctamente ({sizeKb:0.#} KB)";
    }

    private static string DeterminarMimeType(string? contentType, string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return contentType.ToLowerInvariant();

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// Genera una imagen PNG válida en memoria (240x150 px) con estética corporativa
    /// que representa un comprobante de entrega (POD) para pruebas rápidas en Windows/Demo.
    /// </summary>
    private static string GenerarPngDemoBase64(out byte[] pngBytes)
    {
        const int width = 240;
        const int height = 150;

        // Construcción de scanlines RGB (filtro 0 por fila + 3 bytes R,G,B por píxel)
        var rawScanlines = new byte[height * (1 + width * 3)];
        int offset = 0;

        for (int y = 0; y < height; y++)
        {
            rawScanlines[offset++] = 0; // Filter type: None
            for (int x = 0; x < width; x++)
            {
                // Fondo general azul pizarra suave (#EFF6FF)
                byte r = 239, g = 246, b = 255;

                // Borde exterior corporativo (#2563EB)
                if (x < 4 || x >= width - 4 || y < 4 || y >= height - 4)
                {
                    r = 37; g = 99; b = 235;
                }
                // Tarjeta interior blanca de comprobante
                else if (x >= 20 && x < width - 20 && y >= 18 && y < height - 18)
                {
                    r = 255; g = 255; b = 255;

                    // Franja superior azul en la tarjeta
                    if (y < 36)
                    {
                        r = 37; g = 99; b = 235;
                    }
                    // Sello circular verde de verificación a la derecha
                    else
                    {
                        int cx = width - 62;
                        int cy = 84;
                        int dx = x - cx;
                        int dy = y - cy;
                        if (dx * dx + dy * dy <= 22 * 22)
                        {
                            r = 22; g = 163; b = 74; // Verde éxito (#16A34A)
                        }
                        // Líneas simulando datos del remito a la izquierda
                        else if (x >= 34 && x <= 135)
                        {
                            if ((y >= 52 && y <= 58) ||
                                (y >= 70 && y <= 76) ||
                                (y >= 88 && y <= 94) ||
                                (y >= 106 && y <= 112 && x <= 105))
                            {
                                r = 148; g = 163; b = 184; // Slate-400 (#94A3B8)
                            }
                        }
                    }
                }

                rawScanlines[offset++] = r;
                rawScanlines[offset++] = g;
                rawScanlines[offset++] = b;
            }
        }

        using var ms = new MemoryStream();
        // Firma estándar PNG
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        // Chunk IHDR (width, height, bitDepth=8, colorType=2 RGB)
        var ihdr = new byte[13];
        EscribirUInt32BigEndian(ihdr, 0, width);
        EscribirUInt32BigEndian(ihdr, 4, height);
        ihdr[8] = 8; // Bit depth
        ihdr[9] = 2; // Color type: Truecolor RGB
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        EscribirChunkPng(ms, "IHDR"u8, ihdr);

        // Chunk IDAT (comprimido con ZLib)
        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(rawScanlines, 0, rawScanlines.Length);
            }
            EscribirChunkPng(ms, "IDAT"u8, compressedStream.ToArray());
        }

        // Chunk IEND
        EscribirChunkPng(ms, "IEND"u8, []);

        pngBytes = ms.ToArray();
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    private static void EscribirChunkPng(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        EscribirUInt32BigEndian(lengthBytes, 0, (uint)data.Length);
        output.Write(lengthBytes);
        output.Write(type);
        if (!data.IsEmpty)
        {
            output.Write(data);
        }

        uint crc = CalcularCrc32(type, data);
        Span<byte> crcBytes = stackalloc byte[4];
        EscribirUInt32BigEndian(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static void EscribirUInt32BigEndian(Span<byte> buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint CalcularCrc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in type)
        {
            crc = ActualizarCrc32(crc, b);
        }
        foreach (byte b in data)
        {
            crc = ActualizarCrc32(crc, b);
        }
        return ~crc;
    }

    private static uint ActualizarCrc32(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }

    public Func<string?>? ObtenerFirmaCallback { get; set; }
    public Action? LimpiarFirmaAction { get; set; }

    [RelayCommand]
    private void SimularFirma()
    {
        TieneFirma = true;
        FirmaStatusText = "Firma digital capturada";
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
                    "Campos requeridos",
                    "Por favor, complete el nombre y documento del receptor.",
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

            // Obtener firma dibujada real (vectorial en SVG Base64) o fallback
            string? firmaBase64 = ObtenerFirmaCallback?.Invoke();
            if (string.IsNullOrWhiteSpace(firmaBase64) && TieneFirma)
            {
                firmaBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            }

            // Obtener fotografía real capturada/seleccionada o fallback PNG válido si se activó TieneFoto
            string? fotoBase64 = null;
            if (TieneFoto)
            {
                fotoBase64 = !string.IsNullOrWhiteSpace(FotoEvidenciaBase64)
                    ? FotoEvidenciaBase64
                    : GenerarPngDemoBase64(out _);
            }

            // Contrato idéntico a RegistrarEntregaExitosaCommand (UC-20)
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
                Latitud = (parada?.Latitud != null && parada.Latitud != 0) ? parada.Latitud : -34.9124,
                Longitud = (parada?.Longitud != null && parada.Longitud != 0) ? parada.Longitud : -56.1589,
                FechaHoraDispositivoUtc = DateTime.UtcNow
            };

            // 1. Guardar en cola offline cifrada y actualizar estado local SQLite
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.EntregaExitosa,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(paradaId, "Entregado");
            }

            // 2. Actualizar estado en modelo en memoria
            if (parada is not null)
            {
                parada.Estado = "Entregado";
            }

            // 3. Si la app está Online, disparar sincronización en background
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
                    "Confirmar Entrega",
                    $"La entrega a {NombreReceptor.Trim()} fue registrada exitosamente.\n\n{infoRed}",
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
