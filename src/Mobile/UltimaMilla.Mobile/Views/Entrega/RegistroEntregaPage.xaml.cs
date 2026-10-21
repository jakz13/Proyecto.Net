using System.Text;
using UltimaMilla.Mobile.ViewModels.Entrega;

namespace UltimaMilla.Mobile.Views.Entrega;

public partial class RegistroEntregaPage : ContentPage
{
    private readonly SignatureDrawable _drawable = new();
    private List<PointF>? _currentLine;

    public RegistroEntregaPage(RegistroEntregaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        SignatureView.Drawable = _drawable;

        viewModel.LimpiarFirmaAction = () =>
        {
            _drawable.Clear();
            SignatureView.Invalidate();
            PlaceholderLabel.IsVisible = true;
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
        var point = e.Touches.FirstOrDefault();
        _currentLine = new List<PointF> { point };
        _drawable.Lines.Add(_currentLine);
        PlaceholderLabel.IsVisible = false;

        if (BindingContext is RegistroEntregaViewModel vm)
        {
            vm.TieneFirma = true;
            vm.FirmaStatusText = "Firma digital en progreso";
        }

        SignatureView.Invalidate();
    }

    private void OnSignatureDragInteraction(object? sender, TouchEventArgs e)
    {
        if (_currentLine == null)
            return;

        var point = e.Touches.FirstOrDefault();
        _currentLine.Add(point);
        SignatureView.Invalidate();
    }

    private void OnSignatureEndInteraction(object? sender, TouchEventArgs e)
    {
        _currentLine = null;
        SignatureView.Invalidate();

        if (BindingContext is RegistroEntregaViewModel vm)
        {
            var totalPoints = _drawable.Lines.Sum(l => l.Count);
            vm.TieneFirma = _drawable.HasStrokes;
            vm.FirmaStatusText = _drawable.HasStrokes
                ? $"Firma digital capturada ({totalPoints} puntos)"
                : "Toque o dibuje para firmar";

            PlaceholderLabel.IsVisible = !_drawable.HasStrokes;
        }
    }
}

/// <summary>
/// Drawable para renderizar trazos vectoriales de firma digital sobre GraphicsView (UC-20).
/// </summary>
public class SignatureDrawable : IDrawable
{
    public List<List<PointF>> Lines { get; } = new();
    public Color StrokeColor { get; set; } = Color.FromArgb("#1E293B");
    public float StrokeWidth { get; set; } = 3.0f;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.StrokeColor = StrokeColor;
        canvas.StrokeSize = StrokeWidth;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;

        foreach (var line in Lines)
        {
            if (line.Count == 0)
                continue;

            if (line.Count == 1)
            {
                canvas.FillCircle(line[0], StrokeWidth / 2f);
                continue;
            }

            var path = new PathF();
            path.MoveTo(line[0]);
            for (int i = 1; i < line.Count; i++)
            {
                path.LineTo(line[i]);
            }
            canvas.DrawPath(path);
        }
    }

    public void Clear()
    {
        Lines.Clear();
    }

    public bool HasStrokes => Lines.Any(l => l.Count > 0);

    public string ToSvgBase64(float width = 350, float height = 160)
    {
        if (!HasStrokes)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {width:0} {height:0}\" width=\"{width:0}\" height=\"{height:0}\">");
        sb.Append("<g fill=\"none\" stroke=\"#1E293B\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");

        foreach (var line in Lines)
        {
            if (line.Count == 0) continue;
            sb.Append("<path d=\"");
            sb.Append($"M {line[0].X:F1} {line[0].Y:F1} ");
            for (int i = 1; i < line.Count; i++)
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
