namespace UltimaMilla.Mobile.Responsive;

/// <summary>
/// Aplica los tokens de diseño (márgenes, paddings y tamaños de título) según el ancho de la ventana.
/// Los valores por defecto viven en Styles.xaml; en pantallas compactas se sobrescriben en los recursos
/// de la aplicación y las vistas los toman vía <c>DynamicResource</c>.
/// </summary>
public static class TokensResponsive
{
    private static bool? _compactoAplicado;

    public static void Aplicar(Application app, double anchoVentana)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (double.IsNaN(anchoVentana) || double.IsInfinity(anchoVentana) || anchoVentana <= 0)
            return;

        var compacto = ReglasResponsive.EsCompacto(anchoVentana);
        if (_compactoAplicado == compacto)
            return;

        _compactoAplicado = compacto;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var (clave, valor) in ObtenerTokens(compacto))
                app.Resources[clave] = valor;
        });
    }

    private static IEnumerable<(string Clave, object Valor)> ObtenerTokens(bool compacto)
    {
        // Tipografía de títulos
        yield return ("FontSizeH1", compacto ? 21d : 24d);
        yield return ("FontSizeH2", compacto ? 16d : 18d);
        yield return ("FontSizeH3", compacto ? 15d : 16d);
        yield return ("FontSizeLoginTitulo", compacto ? 26d : 32d);
        yield return ("FontSizeBotonChico", compacto ? 12.5d : 13d);

        // Espaciados y paddings
        yield return ("EspaciadoSecciones", compacto ? 12d : 16d);
        yield return ("PaddingTarjeta", compacto ? new Thickness(12) : new Thickness(16));
        yield return ("PaddingPagina", compacto ? new Thickness(12) : new Thickness(20));
        yield return ("PaddingPaginaDetalle", compacto ? new Thickness(10, 10, 10, 12) : new Thickness(16, 12, 16, 16));
        yield return ("PaddingFormulario", compacto ? new Thickness(10, 12, 10, 16) : new Thickness(16, 16, 16, 24));
        yield return ("PaddingLogin", compacto ? new Thickness(16) : new Thickness(30));
        yield return ("PaddingTarjetaLogin", compacto ? new Thickness(16) : new Thickness(24));
        yield return ("PaddingCabeceraRuta", compacto ? new Thickness(12, 12) : new Thickness(20, 16));
        yield return ("MargenListaParadas", compacto ? new Thickness(10, 10, 10, 12) : new Thickness(16, 12, 16, 16));
        yield return ("PaddingBarraAcciones", compacto ? new Thickness(12, 8, 12, 10) : new Thickness(16, 10, 16, 12));
        yield return ("PaddingBotonChico", compacto ? new Thickness(8, 6) : new Thickness(12, 6));
    }
}
