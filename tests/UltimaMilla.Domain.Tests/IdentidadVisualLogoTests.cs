using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>El logo y el sitio web se muestran en todas las pantallas y correos: no admiten esquemas peligrosos como javascript:.</summary>
public class IdentidadVisualLogoTests
{
    private static IdentidadVisual Nueva() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "#D7301D", "#0CAC17", "contacto@operador.uy", "+598 2900 1122", null);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("ftp://servidor/logo.png")]
    [InlineData("logo-sin-esquema.png")]
    public void ActualizarLogo_ConUnEsquemaNoPermitido_SeRechaza(string url)
    {
        Assert.Throws<DomainValidationException>(() => Nueva().ActualizarLogo(url));
    }

    [Theory]
    [InlineData("https://ejemplo.com/logo.png")]
    [InlineData("http://ejemplo.com/logo.png")]
    [InlineData("/uploads/logos/logo_empresa_abc.jpg")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    public void ActualizarLogo_ConUnaImagenValida_SeAcepta(string url)
    {
        var identidad = Nueva();
        identidad.ActualizarLogo(url);
        Assert.Equal(url, identidad.LogoUrl);
    }

    [Fact]
    public void ActualizarLogo_Vacio_QuitaElLogo()
    {
        var identidad = Nueva();
        identidad.ActualizarLogo("https://ejemplo.com/logo.png");
        identidad.ActualizarLogo("  ");
        Assert.Null(identidad.LogoUrl);
    }

    [Fact]
    public void ActualizarComunicaciones_ConSitioWebJavascript_SeRechaza()
    {
        Assert.Throws<DomainValidationException>(() => Nueva().ActualizarComunicaciones("javascript:alert(1)", null));
    }
}
