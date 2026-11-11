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
#if ANDROID
            compressedBytes = ComprimirFotoAndroid(originalBytes, maxDimension, calidad);
#else
            compressedBytes = ComprimirFotoCrossPlatform(originalBytes, maxDimension, calidad);
#endif
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

    private static byte[] ComprimirFotoCrossPlatform(byte[] originalBytes, int maxDimension, float calidad)
    {
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
                outStream.CopyTo(msCompressed);
                compressedBytes = msCompressed.ToArray();
            }
            else
            {
                compressedBytes = originalBytes;
            }

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
                        outStreamSecond.CopyTo(msSecond);
                        var secondBytes = msSecond.ToArray();
                        if (secondBytes.Length < compressedBytes.Length)
                        {
                            compressedBytes = secondBytes;
                        }
                    }
                }
                catch (Exception exSecond)
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageCompressionService] Advertencia en segunda pasada cross-platform: {exSecond.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageCompressionService] Advertencia al comprimir cross-platform: {ex.Message}");
            compressedBytes = originalBytes;
        }

        return compressedBytes;
    }

#if ANDROID
    private static byte[] ComprimirFotoAndroid(byte[] originalBytes, int maxDimension, float calidad)
    {
        // 1. Leer orientacion EXIF del stream original usando ExifInterface
        int rotacionGrados = 0;
        bool flipHorizontal = false;
        bool flipVertical = false;

        try
        {
            using var exifStream = new MemoryStream(originalBytes);
            var exif = new global::Android.Media.ExifInterface(exifStream);
            int tagOrientation = exif.GetAttributeInt(global::Android.Media.ExifInterface.TagOrientation, 1);

            switch (tagOrientation)
            {
                case 6: // OrientationRotate90 (sentido horario: foto vertical tomada con camara)
                    rotacionGrados = 90;
                    break;
                case 3: // OrientationRotate180
                    rotacionGrados = 180;
                    break;
                case 8: // OrientationRotate270 (270 grados horario o 90 antihorario)
                    rotacionGrados = 270;
                    break;
                case 2: // OrientationFlipHorizontal
                    flipHorizontal = true;
                    break;
                case 4: // OrientationFlipVertical
                    flipVertical = true;
                    break;
                case 5: // OrientationTranspose
                    rotacionGrados = 90;
                    flipHorizontal = true;
                    break;
                case 7: // OrientationTransverse
                    rotacionGrados = 270;
                    flipHorizontal = true;
                    break;
            }
        }
        catch (Exception exExif)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageCompressionService] No se pudo leer orientacion EXIF: {exExif.Message}");
        }

        // 2. Decodificar el Bitmap nativo
        var originalBitmap = global::Android.Graphics.BitmapFactory.DecodeByteArray(originalBytes, 0, originalBytes.Length);
        if (originalBitmap == null)
        {
            return ComprimirFotoCrossPlatform(originalBytes, maxDimension, calidad);
        }

        global::Android.Graphics.Bitmap? bitmapTransformado = null;
        try
        {
            int origW = originalBitmap.Width;
            int origH = originalBitmap.Height;

            // 3. Configurar matriz de transformacion (rotacion EXIF y reescalado proporcional max 1024px)
            var matrix = new global::Android.Graphics.Matrix();

            if (rotacionGrados != 0)
            {
                matrix.PostRotate(rotacionGrados);
            }

            if (flipHorizontal)
            {
                matrix.PostScale(-1, 1);
            }
            else if (flipVertical)
            {
                matrix.PostScale(1, -1);
            }

            int maxOrig = Math.Max(origW, origH);
            if (maxOrig > maxDimension)
            {
                float factorEscala = (float)maxDimension / maxOrig;
                matrix.PostScale(factorEscala, factorEscala);
            }

            bitmapTransformado = global::Android.Graphics.Bitmap.CreateBitmap(
                originalBitmap,
                0,
                0,
                origW,
                origH,
                matrix,
                true);

            // 4. Comprimir a JPEG con calidad solicitada (70%)
            int calidadInt = (int)Math.Clamp(calidad * 100, 1, 100);
            using var outStream = new MemoryStream();
            bitmapTransformado.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, calidadInt, outStream);
            var resultBytes = outStream.ToArray();

            // 5. Ajuste si supera el umbral de 100 KB
            const int maxBytesObjetivo = 100 * 1024;
            if (resultBytes.Length > maxBytesObjetivo)
            {
                using var secondStream = new MemoryStream();
                bitmapTransformado.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, 50, secondStream);
                var secondBytes = secondStream.ToArray();
                if (secondBytes.Length < resultBytes.Length)
                {
                    resultBytes = secondBytes;
                }

                int maxDimensionActual = Math.Max(bitmapTransformado.Width, bitmapTransformado.Height);
                if (resultBytes.Length > maxBytesObjetivo && maxDimensionActual > 800)
                {
                    float factor800 = 800f / maxDimensionActual;
                    int w800 = Math.Max(1, (int)(bitmapTransformado.Width * factor800));
                    int h800 = Math.Max(1, (int)(bitmapTransformado.Height * factor800));
                    var scaled800 = global::Android.Graphics.Bitmap.CreateScaledBitmap(bitmapTransformado, w800, h800, true);
                    if (scaled800 != null)
                    {
                        try
                        {
                            using var thirdStream = new MemoryStream();
                            scaled800.Compress(global::Android.Graphics.Bitmap.CompressFormat.Jpeg!, 50, thirdStream);
                            var thirdBytes = thirdStream.ToArray();
                            if (thirdBytes.Length < resultBytes.Length)
                            {
                                resultBytes = thirdBytes;
                            }
                        }
                        finally
                        {
                            if (scaled800 != bitmapTransformado)
                            {
                                scaled800.Recycle();
                            }
                        }
                    }
                }
            }

            return resultBytes;
        }
        finally
        {
            if (bitmapTransformado != null && bitmapTransformado != originalBitmap)
            {
                bitmapTransformado.Recycle();
            }
            originalBitmap.Recycle();
        }
    }
#endif
}
