using Microsoft.Maui.Layouts;
using UltimaMilla.Mobile.Responsive;

namespace UltimaMilla.Mobile.Controls;

/// <summary>
/// Fila adaptativa: ubica sus hijos en una sola fila (ocupando todo el ancho) cuando entran con su
/// ancho natural, y los apila verticalmente a ancho completo cuando no entran. Evita textos de botones
/// cortados en pantallas angostas sin alterar el diseño en pantallas normales.
/// </summary>
public class AdaptiveRow : Layout
{
    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing),
        typeof(double),
        typeof(AdaptiveRow),
        8d,
        propertyChanged: (bindable, _, _) => ((AdaptiveRow)bindable).InvalidateMeasure());

    /// <summary>Separación entre elementos, tanto en fila como apilados.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override ILayoutManager CreateLayoutManager() => new AdaptiveRowLayoutManager(this);
}

internal sealed class AdaptiveRowLayoutManager : ILayoutManager
{
    private readonly AdaptiveRow _layout;
    private bool _apilado;
    private double[] _anchosAsignados = [];
    private double[] _altosAsignados = [];

    public AdaptiveRowLayoutManager(AdaptiveRow layout) => _layout = layout;

    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = _layout.Padding;
        var hijos = HijosVisibles();
        var cantidad = hijos.Count;
        if (cantidad == 0)
        {
            _anchosAsignados = [];
            _altosAsignados = [];
            return new Size(padding.HorizontalThickness, padding.VerticalThickness);
        }

        var anchoInterior = Math.Max(0d, widthConstraint - padding.HorizontalThickness);
        var deseados = hijos.Select(h => Math.Max(0d, h.Measure(double.PositiveInfinity, double.PositiveInfinity).Width)).ToList();

        _anchosAsignados = new double[cantidad];
        _altosAsignados = new double[cantidad];

        var cabe = ReglasResponsive.CabeEnFila(anchoInterior, deseados, _layout.Spacing);
        _apilado = !cabe;

        double contenidoAncho;
        double contenidoAlto;

        if (cabe)
        {
            var anchos = ReglasResponsive.DistribuirAnchos(anchoInterior, deseados, _layout.Spacing);
            var altoMax = 0d;

            for (var i = 0; i < cantidad; i++)
            {
                var w = double.IsPositiveInfinity(anchoInterior) ? deseados[i] : Math.Max(0d, anchos[i]);
                _anchosAsignados[i] = w;
                var size = hijos[i].Measure(w, double.PositiveInfinity);
                _altosAsignados[i] = Math.Max(0d, size.Height);
                altoMax = Math.Max(altoMax, _altosAsignados[i]);
            }

            contenidoAncho = double.IsPositiveInfinity(anchoInterior)
                ? deseados.Sum() + Math.Max(0d, _layout.Spacing) * (cantidad - 1)
                : anchoInterior;
            contenidoAlto = altoMax;
        }
        else
        {
            var altoTotal = 0d;

            for (var i = 0; i < cantidad; i++)
            {
                _anchosAsignados[i] = anchoInterior;
                var size = hijos[i].Measure(anchoInterior, double.PositiveInfinity);
                _altosAsignados[i] = Math.Max(0d, size.Height);
                altoTotal += _altosAsignados[i];
            }

            altoTotal += Math.Max(0d, _layout.Spacing) * (cantidad - 1);
            contenidoAncho = anchoInterior;
            contenidoAlto = altoTotal;
        }

        return new Size(
            Math.Max(0d, contenidoAncho + padding.HorizontalThickness),
            Math.Max(0d, contenidoAlto + padding.VerticalThickness));
    }

    /// <summary>
    /// Ubica a los hijos sin llamar a Measure() para evitar ciclos de diseño (LayoutCycleException)
    /// en WinUI al redimensionar la ventana horizontalmente.
    /// </summary>
    public Size ArrangeChildren(Rect bounds)
    {
        var padding = _layout.Padding;
        var hijos = HijosVisibles();
        var cantidad = hijos.Count;
        if (cantidad == 0)
            return bounds.Size;

        var x = bounds.Left + padding.Left;
        var y = bounds.Top + padding.Top;
        var anchoInterior = Math.Max(0d, bounds.Width - padding.HorizontalThickness);
        var altoInterior = Math.Max(0d, bounds.Height - padding.VerticalThickness);

        // Fallback defensivo si Arrange corre antes que Measure
        if (_anchosAsignados.Length != cantidad || _altosAsignados.Length != cantidad)
        {
            _anchosAsignados = new double[cantidad];
            _altosAsignados = new double[cantidad];
            for (var i = 0; i < cantidad; i++)
            {
                _anchosAsignados[i] = _apilado ? anchoInterior : (anchoInterior / cantidad);
                _altosAsignados[i] = Math.Max(0d, hijos[i].DesiredSize.Height);
            }
        }

        if (_apilado)
        {
            for (var i = 0; i < cantidad; i++)
            {
                var h = Math.Max(0d, _altosAsignados[i] > 0 ? _altosAsignados[i] : hijos[i].DesiredSize.Height);
                hijos[i].Arrange(new Rect(x, y, anchoInterior, h));
                y += h + _layout.Spacing;
            }
        }
        else
        {
            for (var i = 0; i < cantidad; i++)
            {
                var w = Math.Max(0d, _anchosAsignados[i]);
                var h = altoInterior > 0 ? altoInterior : Math.Max(0d, _altosAsignados[i]);
                hijos[i].Arrange(new Rect(x, y, w, h));
                x += w + _layout.Spacing;
            }
        }

        return bounds.Size;
    }

    private List<IView> HijosVisibles()
        => ((IEnumerable<IView>)_layout).Where(h => h.Visibility != Visibility.Collapsed).ToList();
}
