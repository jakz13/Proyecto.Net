using System.Text;
using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class RegistroEntregaPage : ContentPage
{
    private readonly SignatureDrawable _drawable = new();
    private List<PointF>? _currentLine;

    public RegistroEntregaPage() : this(
        Application.Current?.Handler?.MauiContext?.Services.GetService<RegistroEntregaViewModel>() 
        ?? new RegistroEntregaViewModel())
    {
    }

    public RegistroEntregaPage(RegistroEntregaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        SignatureView.Drawable = _drawable;

        viewModel.LimpiarFirmaAction = () =>
        {
            _drawable.Clear();
            SignatureView.Invalidate();
        };

        viewModel.ObtenerFirmaCallback = () =>
        {
            if (!_drawable.HasStrokes)
                return null;

            var w = (float)(SignatureView.Width > 0 ? SignatureView.Width : 350);
            var h = (float)(SignatureView.Height > 0 ? SignatureView.Height : 160);
            return _drawable.ToSvgBase64(w, h);
        };
    }

    private void OnSignatureStartInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
            if (e.Touches == null || e.Touches.Length == 0)
                return;

            var point = e.Touches[0];
            if (float.IsNaN(point.X) || float.IsNaN(point.Y))
                return;

            lock (_drawable.SyncLock)
            {
                _currentLine = new List<PointF> { point };
                _drawable.Lines.Add(_currentLine);
            }

            if (BindingContext is RegistroEntregaViewModel vm)
            {
                vm.TieneFirma = true;
                vm.FirmaStatusText = "Firma digital en progreso...";
            }

            SignatureView.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Signature] Error en StartInteraction: {ex.Message}");
        }
    }

    private void OnSignatureDragInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
            if (_currentLine == null || e.Touches == null || e.Touches.Length == 0)
                return;

            var point = e.Touches[0];
            if (float.IsNaN(point.X) || float.IsNaN(point.Y))
                return;

            lock (_drawable.SyncLock)
            {
                _currentLine.Add(point);
            }

            SignatureView.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Signature] Error en DragInteraction: {ex.Message}");
        }
    }

    private void OnSignatureEndInteraction(object? sender, TouchEventArgs e)
    {
        try
        {
            _currentLine = null;
            SignatureView.Invalidate();

            if (BindingContext is RegistroEntregaViewModel vm)
            {
                int totalPoints;
                bool hasStrokes;
                lock (_drawable.SyncLock)
                {
                    hasStrokes = _drawable.HasStrokes;
                    totalPoints = _drawable.Lines.Sum(l => l.Count);
                }

                vm.TieneFirma = hasStrokes;
                vm.FirmaStatusText = hasStrokes
                    ? $"Firma digital capturada ({totalPoints} puntos)"
                    : "Toque o dibuje para firmar";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Signature] Error en EndInteraction: {ex.Message}");
        }
    }
}

/// <summary>
/// Drawable para renderizar trazos vectoriales de firma digital sobre GraphicsView (UC-20).
/// Completamente thread-safe para evitar colisiones entre el hilo de entrada y el hilo de composición.
/// </summary>
public class SignatureDrawable : IDrawable
{
    public object SyncLock { get; } = new();
    public List<List<PointF>> Lines { get; } = new();
    public Color StrokeColor { get; set; } = Color.FromArgb("#1E293B");
    public float StrokeWidth { get; set; } = 3.0f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        try
        {
            canvas.StrokeColor = StrokeColor;
            canvas.StrokeSize = StrokeWidth;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            List<PointF[]> snapshot;
            lock (SyncLock)
            {
                snapshot = Lines
                    .Where(l => l != null && l.Count > 0)
                    .Select(l => l.ToArray())
                    .ToList();
            }

            foreach (var line in snapshot)
            {
                if (line.Length == 0)
                    continue;

                if (line.Length == 1)
                {
                    canvas.FillCircle(line[0], StrokeWidth / 2f);
                    continue;
                }

                var path = new PathF();
                path.MoveTo(line[0]);
                for (int i = 1; i < line.Length; i++)
                {
                    path.LineTo(line[i]);
                }
                canvas.DrawPath(path);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SignatureDrawable] Error al renderizar: {ex.Message}");
        }
    }

    public void Clear()
    {
        lock (SyncLock)
        {
            Lines.Clear();
        }
    }

    public bool HasStrokes
    {
        get
        {
            lock (SyncLock)
            {
                return Lines.Any(l => l != null && l.Count > 0);
            }
        }
    }

    public string ToSvgBase64(float width = 350, float height = 160)
    {
        List<PointF[]> snapshot;
        lock (SyncLock)
        {
            if (!HasStrokes)
                return string.Empty;

            snapshot = Lines
                .Where(l => l != null && l.Count > 0)
                .Select(l => l.ToArray())
                .ToList();
        }

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width:0} {height:0}\" width=\"{width:0}\" height=\"{height:0}\">");
        sb.Append("<g fill=\"none\" stroke=\"#1E293B\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");

        foreach (var line in snapshot)
        {
            if (line.Length == 0) continue;
            sb.Append("<path d=\"");
            sb.Append($"M {line[0].X:F1} {line[0].Y:F1} ");
            for (int i = 1; i < line.Length; i++)
            {
                sb.Append($"L {line[i].X:F1} {line[i].Y:F1} ");
            }
            sb.Append("\" />");
        }

        sb.Append("</g></svg>");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
    }
}
