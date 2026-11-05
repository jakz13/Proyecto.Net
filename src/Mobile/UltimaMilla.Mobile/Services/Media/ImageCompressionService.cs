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

    /// <summary>
    /// Genera una imagen de prueba (Demo) válida en formato JPEG comprimido
    /// para demostraciones y pruebas en PC sin cámara física.
    /// </summary>
    public static async Task<ImageCompressionResult> GenerarEvidenciaDemoAsync()
    {
        var rawDemoPng = GenerarPngDemoBytes();
        var resultado = await ComprimirFotoAsync(new MemoryStream(rawDemoPng), 800, CalidadJpeg);

        double compKb = Math.Max(0.1, Math.Round(resultado.CompressedBytes.Length / 1024.0, 1));
        return new ImageCompressionResult
        {
            CompressedBytes = resultado.CompressedBytes,
            Base64DataUri = resultado.Base64DataUri,
            OriginalSizeBytes = rawDemoPng.Length,
            MimeType = "image/jpeg",
            ResumenCompresion = $"Fotografía de prueba (Demo) generada: {compKb:0.#} KB"
        };
    }

    private static byte[] GenerarPngDemoBytes()
    {
        const int width = 320;
        const int height = 200;

        var rawScanlines = new byte[height * (1 + width * 3)];
        int offset = 0;

        for (int y = 0; y < height; y++)
        {
            rawScanlines[offset++] = 0; // Filter: None
            for (int x = 0; x < width; x++)
            {
                byte r = 241, g = 245, b = 249; // Slate-100 fondo

                // Marco exterior
                if (x < 3 || x >= width - 3 || y < 3 || y >= height - 3)
                {
                    r = 30; g = 58; b = 138; // Azul corporativo
                }
                // Tarjeta interior blanca
                else if (x >= 14 && x < width - 14 && y >= 14 && y < height - 14)
                {
                    r = 255; g = 255; b = 255;

                    // Encabezado
                    if (y < 42)
                    {
                        r = 30; g = 58; b = 138;
                    }
                    // Sello verde
                    else
                    {
                        int cx = width - 48;
                        int cy = 90;
                        int dx = x - cx;
                        int dy = y - cy;
                        if (dx * dx + dy * dy <= 20 * 20)
                        {
                            r = 22; g = 163; b = 74; // Verde
                        }
                        // Líneas de remito
                        else if (x >= 28 && x <= 220)
                        {
                            if ((y >= 60 && y <= 66) ||
                                (y >= 80 && y <= 86) ||
                                (y >= 100 && y <= 106) ||
                                (y >= 120 && y <= 126 && x <= 160) ||
                                (y >= 145 && y <= 151 && x <= 120))
                            {
                                r = 100; g = 116; b = 139; // Slate-500
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
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        var ihdr = new byte[13];
        ihdr[0] = (byte)((width >> 24) & 0xFF);
        ihdr[1] = (byte)((width >> 16) & 0xFF);
        ihdr[2] = (byte)((width >> 8) & 0xFF);
        ihdr[3] = (byte)(width & 0xFF);
        ihdr[4] = (byte)((height >> 24) & 0xFF);
        ihdr[5] = (byte)((height >> 16) & 0xFF);
        ihdr[6] = (byte)((height >> 8) & 0xFF);
        ihdr[7] = (byte)(height & 0xFF);
        ihdr[8] = 8;
        ihdr[9] = 2;
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        EscribirChunkPng(ms, "IHDR"u8, ihdr);

        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(rawScanlines, 0, rawScanlines.Length);
            }
            EscribirChunkPng(ms, "IDAT"u8, compressedStream.ToArray());
        }

        EscribirChunkPng(ms, "IEND"u8, []);
        return ms.ToArray();
    }

    private static void EscribirChunkPng(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        lengthBytes[0] = (byte)((data.Length >> 24) & 0xFF);
        lengthBytes[1] = (byte)((data.Length >> 16) & 0xFF);
        lengthBytes[2] = (byte)((data.Length >> 8) & 0xFF);
        lengthBytes[3] = (byte)(data.Length & 0xFF);
        output.Write(lengthBytes);
        output.Write(type);
        if (!data.IsEmpty)
        {
            output.Write(data);
        }

        uint crc = 0xFFFFFFFFu;
        foreach (byte b in type) crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
        foreach (byte b in data) crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
        crc = ~crc;

        Span<byte> crcBytes = stackalloc byte[4];
        crcBytes[0] = (byte)((crc >> 24) & 0xFF);
        crcBytes[1] = (byte)((crc >> 16) & 0xFF);
        crcBytes[2] = (byte)((crc >> 8) & 0xFF);
        crcBytes[3] = (byte)(crc & 0xFF);
        output.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = InicializarCrcTable();

    private static uint[] InicializarCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }
}
