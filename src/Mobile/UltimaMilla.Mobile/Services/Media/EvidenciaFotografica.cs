namespace UltimaMilla.Mobile.Services.Media;

/// <summary>
/// Captura de la fotografía de evidencia (entrega, intento fallido, retiro): cámara o galería, con comprobación de permisos
/// y compresión. Devuelve <c>null</c> si el usuario cancela o si no se pudo obtener la imagen (en ese caso ya se le informó).
/// </summary>
public static class EvidenciaFotografica
{
    public const string OpcionTomarFoto = "Tomar foto con la cámara";
    public const string OpcionGaleria = "Seleccionar de la galería";

    public static async Task<ImageCompressionResult?> CapturarAsync(string titulo = "Adjuntar evidencia fotográfica")
    {
        if (MediaPicker.Default.IsCaptureSupported)
        {
            var res = await DesdeCamaraAsync();
            if (res != null)
                return res;
        }

        return await DesdeGaleriaAsync();
    }

    private static async Task<ImageCompressionResult?> DesdeCamaraAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                return await OfrecerGaleriaAsync("No hay una cámara disponible en este dispositivo.");
            }

            var permiso = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permiso != PermissionStatus.Granted)
            {
                permiso = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (permiso != PermissionStatus.Granted)
            {
                return await OfrecerGaleriaAsync("No se concedió el permiso de la cámara.");
            }

            var foto = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = $"EVIDENCIA_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg"
            });

            return foto is null ? null : await ProcesarAsync(foto);
        }
        catch (FeatureNotSupportedException)
        {
            return await OfrecerGaleriaAsync("El dispositivo no tiene una cámara compatible.");
        }
        catch (PermissionException)
        {
            await AvisarAsync("Permiso requerido", "El sistema bloqueó el acceso a la cámara. Revise los permisos de la aplicación en Ajustes.");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al usar la cámara: {ex.Message}");
            return await OfrecerGaleriaAsync("No fue posible iniciar la cámara en este momento.");
        }
    }

    private static async Task<ImageCompressionResult?> DesdeGaleriaAsync()
    {
        try
        {
            var foto = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions { Title = "Seleccione la fotografía de evidencia" });
            return foto is null ? null : await ProcesarAsync(foto);
        }
        catch (PermissionException)
        {
            await AvisarAsync("Permiso requerido", "No se otorgó permiso para leer las imágenes del dispositivo.");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al abrir la galería: {ex.Message}");
            await AvisarAsync("No se pudo abrir la galería", "No fue posible cargar la imagen desde el selector de archivos del dispositivo.");
            return null;
        }
    }

    private static async Task<ImageCompressionResult?> OfrecerGaleriaAsync(string motivo)
    {
        if (Shell.Current is null)
            return null;

        var seleccionar = await Shell.Current.DisplayAlertAsync(
            "Cámara no disponible",
            $"{motivo}\n\n¿Desea seleccionar una imagen de la galería?",
            "Seleccionar de la galería",
            "Cancelar");

        return seleccionar ? await DesdeGaleriaAsync() : null;
    }

    private static async Task<ImageCompressionResult?> ProcesarAsync(FileResult foto)
    {
        try
        {
            await using var flujo = await foto.OpenReadAsync();
            var resultado = await ImageCompressionService.ComprimirFotoAsync(flujo);
            if (resultado.CompressedBytes.Length == 0)
            {
                await AvisarAsync("Archivo vacío", "La fotografía seleccionada no contiene datos válidos.");
                return null;
            }

            return resultado;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EvidenciaFotografica] Error al procesar la fotografía: {ex.Message}");
            await AvisarAsync("Error de imagen", "No fue posible procesar y comprimir la imagen seleccionada.");
            return null;
        }
    }

    private static async Task AvisarAsync(string titulo, string mensaje)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(titulo, mensaje, "Entendido");
        }
    }
}
