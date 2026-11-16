namespace UltimaMilla.Mobile.Services.Media;

/// <summary>Resultado tipado de captura o selección de fotografía sin mostrar diálogos nativos.</summary>
public record CapturaFotoOutcome(
    ImageCompressionResult? Imagen,
    string? ErrorTitulo = null,
    string? ErrorMensaje = null,
    string? MotivoFallbackGaleria = null);

/// <summary>
/// Captura de la fotografía de evidencia (entrega, intento fallido, retiro): cámara o galería,
/// con comprobación de permisos y compresión, devolviendo resultados estructurados para modales corporativos.
/// </summary>
public static class EvidenciaFotografica
{
    public const string OpcionTomarFoto = "Tomar foto con la cámara";
    public const string OpcionGaleria = "Seleccionar de la galería";

    public static async Task<CapturaFotoOutcome> IntentarCapturarDesdeCamaraAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                return new CapturaFotoOutcome(
                    null,
                    MotivoFallbackGaleria: "No hay una cámara disponible en este dispositivo.");
            }

            var permiso = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permiso != PermissionStatus.Granted)
            {
                permiso = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (permiso != PermissionStatus.Granted)
            {
                return new CapturaFotoOutcome(
                    null,
                    MotivoFallbackGaleria: "No se concedió el permiso de la cámara.");
            }

            var foto = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = $"EVIDENCIA_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg"
            });

            return foto is null ? new CapturaFotoOutcome(null) : await ProcesarOutcomeAsync(foto);
        }
        catch (FeatureNotSupportedException)
        {
            return new CapturaFotoOutcome(
                null,
                MotivoFallbackGaleria: "El dispositivo no tiene una cámara compatible.");
        }
        catch (PermissionException)
        {
            return new CapturaFotoOutcome(
                null,
                ErrorTitulo: "Permiso requerido",
                ErrorMensaje: "El sistema bloqueó el acceso a la cámara. Revise los permisos de la aplicación en Ajustes.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al usar la cámara: {ex.Message}");
            return new CapturaFotoOutcome(
                null,
                MotivoFallbackGaleria: "No fue posible iniciar la cámara en este momento.");
        }
    }

    public static async Task<CapturaFotoOutcome> IntentarSeleccionarDesdeGaleriaAsync()
    {
        try
        {
            var foto = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Seleccione la fotografía de evidencia"
            });

            return foto is null ? new CapturaFotoOutcome(null) : await ProcesarOutcomeAsync(foto);
        }
        catch (PermissionException)
        {
            return new CapturaFotoOutcome(
                null,
                ErrorTitulo: "Permiso requerido",
                ErrorMensaje: "No se otorgó permiso para leer las imágenes del dispositivo.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al abrir la galería: {ex.Message}");
            return new CapturaFotoOutcome(
                null,
                ErrorTitulo: "No se pudo abrir la galería",
                ErrorMensaje: "No fue posible cargar la imagen desde el selector de archivos del dispositivo.");
        }
    }

    private static async Task<CapturaFotoOutcome> ProcesarOutcomeAsync(FileResult foto)
    {
        try
        {
            await using var flujo = await foto.OpenReadAsync();
            var resultado = await ImageCompressionService.ComprimirFotoAsync(flujo);
            if (resultado.CompressedBytes.Length == 0)
            {
                return new CapturaFotoOutcome(
                    null,
                    ErrorTitulo: "Archivo vacío",
                    ErrorMensaje: "La fotografía seleccionada no contiene datos válidos.");
            }

            return new CapturaFotoOutcome(resultado);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al procesar la fotografía: {ex.Message}");
            return new CapturaFotoOutcome(
                null,
                ErrorTitulo: "Error de imagen",
                ErrorMensaje: "No fue posible procesar y comprimir la imagen seleccionada.");
        }
    }
}
