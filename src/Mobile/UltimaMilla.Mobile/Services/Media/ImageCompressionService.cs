using System.IO.Compression;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace UltimaMilla.Mobile.Services.Media;

public class ImageCompressionResult
{
    public byte[] CompressedBytes { get; init; } = Array.Empty<byte>();
    public string Base64DataUri { get; init; } = string.Empty;
    public long OriginalSizeBytes { get; init; }
    public long CompressedSizeBytes => CompressedBytes.Length;
    public string MimeType { get; init; } = "image/jpeg";
    public string ResumenCompresion { get; init; } = string.Empty;
}

/// <summary>
/// Servicio de compresión y redimensionamiento de fotografías de evidencia (POD).
/// Optimiza imágenes a JPEG (máx. 1024px, 70% de calidad, ~80 KB) para transferencia liviana.
/// </summary>
public static class ImageCompressionService
{
    public const int MaxDimensionPixels = 1024;
    public const float CalidadJpeg = 0.70f;

    public static async Task<ImageCompressionResult> ComprimirFotoAsync(
        Stream originalStream,
        int maxDimension = MaxDimensionPixels,
        float calidad = CalidadJpeg)
    {
        using var memoryOriginal = new MemoryStream();
        await originalStream.CopyToAsync(memoryOriginal);
        var originalBytes = memoryOriginal.ToArray();
        long originalSize = originalBytes.Length;

        byte[] compressedBytes;

        try
        {
            using var seekableStream = new MemoryStream(originalBytes);
            var image = PlatformImage.FromStream(seekableStream);
            if (image != null)
            {
                var downsized = image.Downsize(maxDimension, disposeOriginal: true);
                using var outStream = downsized.AsStream(ImageFormat.Jpeg, calidad);
                using var msCompressed = new MemoryStream();
                await outStream.CopyToAsync(msCompressed);
                compressedBytes = msCompressed.ToArray();
            }
            else
            {
                compressedBytes = originalBytes;
            }

            // Si tras la compresión inicial el payload supera los 100 KB, realizar una segunda pasada (800px, 50% calidad)
            const int maxBytesObjetivo = 100 * 1024;
            if (compressedBytes.Length > maxBytesObjetivo)
            {
                try
                {
                    using var secondStream = new MemoryStream(originalBytes);
                    var secondImage = PlatformImage.FromStream(secondStream);
                    if (secondImage != null)
                    {
                        var downsizedSecond = secondImage.Downsize(800, disposeOriginal: true);
                        using var outStreamSecond = downsizedSecond.AsStream(ImageFormat.Jpeg, 0.50f);
                        using var msSecond = new MemoryStream();
                        await outStreamSecond.CopyToAsync(msSecond);
                        var secondBytes = msSecond.ToArray();
                        if (secondBytes.Length < compressedBytes.Length)
                        {
                            compressedBytes = secondBytes;
                        }
                    }
                }
                catch (Exception exSecond)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageCompressionService] Advertencia en segunda pasada: {exSecond.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageCompressionService] Advertencia al comprimir: {ex.Message}");
            compressedBytes = originalBytes;
        }

        var base64Data = $"data:image/jpeg;base64,{Convert.ToBase64String(compressedBytes)}";
        double originalKb = Math.Max(0.1, Math.Round(originalSize / 1024.0, 1));
        double compressedKb = Math.Max(0.1, Math.Round(compressedBytes.Length / 1024.0, 1));

        return new ImageCompressionResult
        {
            CompressedBytes = compressedBytes,
            Base64DataUri = base64Data,
            OriginalSizeBytes = originalSize,
            MimeType = "image/jpeg",
            ResumenCompresion = $"Fotografía comprimida adjuntada: {compressedKb:0.#} KB (original: {originalKb:0.#} KB)"
        };
    }
}
