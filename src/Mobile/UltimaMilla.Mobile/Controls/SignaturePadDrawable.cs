using System.IO.Compression;
using System.Text;

namespace UltimaMilla.Mobile.Controls;

/// <summary>
/// Lienzo táctil y ratón para captura nativa de firma digital en pantalla (IDrawable).
/// Diseñado con soporte de dibujo thread-safe, línea guía punteada y rasterizador de respaldo.
/// </summary>
public class SignaturePadDrawable : IDrawable
{
    private readonly object _syncLock = new();
    private List<PointF>? _trazoActual;

    public object SyncLock => _syncLock;

    public List<List<PointF>> Trazos { get; } = new();

    public Color ColorTrazo { get; set; } = Color.FromArgb("#0F172A");
    public float GrosorTrazo { get; set; } = 3.0f;
    public Color ColorGuia { get; set; } = Color.FromArgb("#CBD5E1");
    public Color ColorFondo { get; set; } = Colors.White;

    public bool TieneFirma
    {
        get
        {
            lock (_syncLock)
            {
                return Trazos.Any(t => t != null && t.Count > 1) || (Trazos.Count > 0 && Trazos.Any(t => t != null && t.Count > 0));
            }
        }
    }

    public void IniciarTrazo(PointF punto)
    {
        if (float.IsNaN(punto.X) || float.IsNaN(punto.Y))
            return;

        lock (_syncLock)
        {
            _trazoActual = new List<PointF> { punto };
            Trazos.Add(_trazoActual);
        }
    }

    public void AgregarPunto(PointF punto)
    {
        if (float.IsNaN(punto.X) || float.IsNaN(punto.Y))
            return;

        lock (_syncLock)
        {
            if (_trazoActual != null)
            {
                _trazoActual.Add(punto);
            }
            else
            {
                _trazoActual = new List<PointF> { punto };
                Trazos.Add(_trazoActual);
            }
        }
    }

    public void FinalizarTrazo()
    {
        lock (_syncLock)
        {
            _trazoActual = null;
        }
    }

    public void Limpiar()
    {
        lock (_syncLock)
        {
            _trazoActual = null;
            Trazos.Clear();
        }
    }

    /// <summary>
    /// Genera trazos fluidos de firma de demostración para pruebas visuales en pantalla.
    /// </summary>
    public void CargarTrazoDemo()
    {
        lock (_syncLock)
        {
            _trazoActual = null;
            Trazos.Clear();

            // Trazo 1: Rúbrica inicial
            var t1 = new List<PointF>
            {
                new(40f, 120f),
                new(55f, 90f),
                new(70f, 60f),
                new(85f, 115f),
                new(100f, 75f),
                new(115f, 105f),
                new(135f, 90f),
                new(160f, 100f)
            };
            Trazos.Add(t1);

            // Trazo 2: Cruzado central
            var t2 = new List<PointF>
            {
                new(65f, 90f),
                new(95f, 88f),
                new(125f, 92f),
                new(150f, 90f)
            };
            Trazos.Add(t2);

            // Trazo 3: Trazo inferior dinámico
            var t3 = new List<PointF>
            {
                new(165f, 95f),
                new(190f, 80f),
                new(215f, 110f),
                new(245f, 85f),
                new(275f, 105f),
                new(305f, 95f),
                new(330f, 110f)
            };
            Trazos.Add(t3);
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            // 1. Fondo blanco limpio
            canvas.FillColor = ColorFondo;
            canvas.FillRectangle(dirtyRect);

            // 2. Línea guía punteada horizontal en el tercio inferior (~70% de la altura)
            float yGuia = dirtyRect.Height * 0.70f;
            float margenX = 16f;
            canvas.StrokeColor = ColorGuia;
            canvas.StrokeSize = 1f;
            canvas.StrokeDashPattern = new float[] { 4f, 4f };
            canvas.DrawLine(margenX, yGuia, dirtyRect.Width - margenX, yGuia);

            // 3. Trazos de la firma
            canvas.StrokeDashPattern = null;
            canvas.StrokeColor = ColorTrazo;
            canvas.StrokeSize = GrosorTrazo;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            List<PointF[]> snapshot;
            lock (_syncLock)
            {
                snapshot = Trazos
                    .Where(t => t != null && t.Count > 0)
                    .Select(t => t.ToArray())
                    .ToList();
            }

            foreach (var trazo in snapshot)
            {
                if (trazo.Length == 0)
                    continue;

                if (trazo.Length == 1)
                {
                    canvas.FillCircle(trazo[0], GrosorTrazo / 2f);
                    continue;
                }

                var path = new PathF();
                path.MoveTo(trazo[0]);
                for (int i = 1; i < trazo.Length; i++)
                {
                    path.LineTo(trazo[i]);
                }
                canvas.DrawPath(path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SignaturePadDrawable] Error al dibujar firma: {ex.Message}");
        }
    }

    /// <summary>
    /// Genera una imagen PNG rasterizada en memoria (ARGB) a partir de los trazos vectoriales,
    /// garantizando un comprobante de firma reproducible e independiente del hilo visual.
    /// </summary>
    public byte[] GenerarPngEnMemoria(int width = 360, int height = 180)
    {
        if (width <= 0) width = 360;
        if (height <= 0) height = 180;

        List<PointF[]> snapshot;
        lock (_syncLock)
        {
            snapshot = Trazos
                .Where(t => t != null && t.Count > 0)
                .Select(t => t.ToArray())
                .ToList();
        }

        // Buffer de píxeles RGB (fondo blanco 255,255,255)
        byte[] rgb = new byte[width * height * 3];
        Array.Fill(rgb, (byte)255);

        // Línea guía punteada en tercio inferior
        int yGuia = (int)(height * 0.70f);
        int margenX = 16;
        for (int x = margenX; x < width - margenX; x++)
        {
            if ((x / 4) % 2 == 0 && yGuia >= 0 && yGuia < height)
            {
                int p = (yGuia * width + x) * 3;
                rgb[p] = 203;     // #CBD5E1 R
                rgb[p + 1] = 213; // G
                rgb[p + 2] = 225; // B
            }
        }

        // Color del trazo: #0F172A (15, 23, 42)
        byte strokeR = 15, strokeG = 23, strokeB = 42;
        int radio = 2;

        void PintarPunto(int px, int py)
        {
            for (int dy = -radio; dy <= radio; dy++)
            {
                for (int dx = -radio; dx <= radio; dx++)
                {
                    if (dx * dx + dy * dy <= radio * radio)
                    {
                        int x = px + dx;
                        int y = py + dy;
                        if (x >= 0 && x < width && y >= 0 && y < height)
                        {
                            int p = (y * width + x) * 3;
                            rgb[p] = strokeR;
                            rgb[p + 1] = strokeG;
                            rgb[p + 2] = strokeB;
                        }
                    }
                }
            }
        }

        void PintarLinea(PointF p1, PointF p2)
        {
            int x0 = (int)Math.Round(p1.X);
            int y0 = (int)Math.Round(p1.Y);
            int x1 = (int)Math.Round(p2.X);
            int y1 = (int)Math.Round(p2.Y);

            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                PintarPunto(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        foreach (var trazo in snapshot)
        {
            if (trazo.Length == 1)
            {
                PintarPunto((int)Math.Round(trazo[0].X), (int)Math.Round(trazo[0].Y));
            }
            else
            {
                for (int i = 1; i < trazo.Length; i++)
                {
                    PintarLinea(trazo[i - 1], trazo[i]);
                }
            }
        }

        // Codificación PNG
        var rawScanlines = new byte[height * (1 + width * 3)];
        int srcIdx = 0, dstIdx = 0;
        for (int y = 0; y < height; y++)
        {
            rawScanlines[dstIdx++] = 0; // Filter type: None
            Array.Copy(rgb, srcIdx, rawScanlines, dstIdx, width * 3);
            srcIdx += width * 3;
            dstIdx += width * 3;
        }

        using var ms = new MemoryStream();
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]); // Magic header

        var ihdr = new byte[13];
        EscribirUInt32BigEndian(ihdr, 0, (uint)width);
        EscribirUInt32BigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;
        ihdr[9] = 2; // RGB
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

    public string GenerarPngBase64EnMemoria(int width = 360, int height = 180)
    {
        var pngBytes = GenerarPngEnMemoria(width, height);
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

        uint crc = 0xFFFFFFFFu;
        foreach (byte b in type) crc = ActualizarCrc32(crc, b);
        foreach (byte b in data) crc = ActualizarCrc32(crc, b);
        crc = ~crc;

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

    private static uint ActualizarCrc32(uint crc, byte b)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }
}

