using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>
/// Regresión de la prueba de datos corruptos hecha contra el servidor (16/11): la API aceptaba un comercio con email "no-es-email" y
/// teléfono "abc", zonas con polígonos basura y colores "javascript:...". Cada caso falla sin la validación de dominio.
/// </summary>
public class ContactoValidacionTests
{
    // ---------- Email ----------

    [Theory]
    [InlineData("nombre@empresa.com")]
    [InlineData("a.b-c+d@sub.empresa.com.uy")]
    [InlineData("  MAYUS@Empresa.COM  ")]
    public void Email_Valido_SeAcepta(string email) => Assert.True(ContactoValidacion.EmailEsValido(email));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-email")]
    [InlineData("a@@b")]
    [InlineData("no@@@")]
    [InlineData("a@b")]                 // sin extensión
    [InlineData("a@b.c")]               // extensión de una letra
    [InlineData("@empresa.com")]
    [InlineData("nombre@")]
    [InlineData(".nombre@empresa.com")]
    [InlineData("nombre.@empresa.com")]
    [InlineData("no..mbre@empresa.com")]
    [InlineData("nombre@-empresa.com")]
    [InlineData("nombre@empresa-.com")]
    [InlineData("nombre@empresa..com")]
    [InlineData("nom bre@empresa.com")]
    [InlineData("<script>@empresa.com")]
    public void Email_Invalido_SeRechaza(string email)
    {
        Assert.False(ContactoValidacion.EmailEsValido(email));
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarEmail(email));
    }

    [Fact]
    public void Email_ConMasDe200Caracteres_SeRechaza()
    {
        var largo = new string('a', 60) + "@" + new string('b', 150) + ".com";
        Assert.False(ContactoValidacion.EmailEsValido(largo));
    }

    [Fact]
    public void Email_ValidoSeNormalizaAMinusculas() =>
        Assert.Equal("nombre@empresa.com", ContactoValidacion.ValidarEmail("  Nombre@Empresa.COM "));

    [Fact]
    public void EmailOpcional_VacioEsValidoPeroSiVieneDebeSerCorrecto()
    {
        Assert.Null(ContactoValidacion.ValidarEmailOpcional(null));
        Assert.Null(ContactoValidacion.ValidarEmailOpcional("  "));
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarEmailOpcional("no@@@"));
    }

    // ---------- Teléfono ----------

    [Theory]
    [InlineData("099123456", "099123456")]
    [InlineData("099 123 456", "099123456")]
    [InlineData(" 099-123-456 ", "099123456")]
    [InlineData("091234567", "091234567")]
    public void Telefono_NueveDigitos_SeAceptaYSeNormaliza(string entrada, string esperado) =>
        Assert.Equal(esperado, ContactoValidacion.ValidarTelefono(entrada));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("09912345")]            // 8 dígitos
    [InlineData("0991234567")]          // 10 dígitos
    [InlineData("+598 99 111 222")]     // con código de país: no son 9 dígitos
    [InlineData("099 123 45a")]
    [InlineData("(099) 123 456")]
    [InlineData("099123456'; --")]
    public void Telefono_NoEsDeNueveDigitos_SeRechaza(string telefono)
    {
        Assert.False(ContactoValidacion.TelefonoEsValido(telefono));
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarTelefono(telefono));
    }

    [Fact]
    public void Telefono_FormateaParaMostrar() => Assert.Equal("099 123 456", ContactoValidacion.FormatearTelefono("099123456"));

    // ---------- Direcciones ----------

    [Theory]
    [InlineData("Av. Italia 3456, Montevideo")]
    [InlineData("18 de Julio 1234")]
    public void DireccionTexto_Valida_SeAcepta(string texto) => Assert.Equal(texto, ContactoValidacion.ValidarDireccionTexto(texto, "La dirección"));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]                       // muy corta
    [InlineData("12345 678")]                 // sin letras
    [InlineData("Calle <script>alert(1)</script>")]
    [InlineData("Calle\u0000Falsa 123")]
    public void DireccionTexto_Invalida_SeRechaza(string texto) =>
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarDireccionTexto(texto, "La dirección"));

    [Fact]
    public void DireccionTexto_ConMasDe200Caracteres_SeRechaza() =>
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarDireccionTexto("Calle " + new string('a', 250), "La dirección"));

    [Theory]
    [InlineData("1234")]
    [InlineData("1234 bis")]
    [InlineData("S/N")]
    [InlineData("1234-A")]
    public void NumeroPuerta_Valido(string numero) => Assert.Equal(numero, ContactoValidacion.ValidarNumeroPuerta(numero));

    [Theory]
    [InlineData("")]
    [InlineData("<1>")]
    [InlineData("-5")]
    [InlineData("12\u0000")]
    public void NumeroPuerta_Invalido(string numero) =>
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarNumeroPuerta(numero));

    [Theory]
    [InlineData("Montevideo")]
    [InlineData("San José de Mayo")]
    [InlineData("Ciudad de la Costa")]
    public void Ciudad_Valida(string ciudad) => Assert.Equal(ciudad, ContactoValidacion.ValidarCiudad(ciudad));

    [Theory]
    [InlineData("")]
    [InlineData("M")]
    [InlineData("123")]
    [InlineData("Mvd<script>")]
    public void Ciudad_Invalida(string ciudad) => Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarCiudad(ciudad));

    [Fact]
    public void Direccion_ConCalleOCiudadBasura_NoSeCrea()
    {
        Assert.Throws<DomainValidationException>(() => new Direccion(Guid.NewGuid(), "a\u0000b", "1", "Montevideo"));
        Assert.Throws<DomainValidationException>(() => new Direccion(Guid.NewGuid(), "18 de Julio", "1", "<b>x</b>"));
        Assert.Throws<DomainValidationException>(() => new Direccion(Guid.NewGuid(), "18 de Julio", "1", "Montevideo", latitud: double.NaN));
        var ok = new Direccion(Guid.NewGuid(), "18 de Julio", "1234", "Montevideo");
        Assert.Equal("18 de Julio 1234, Montevideo", ok.ToString());
    }

    // ---------- Zonas ----------

    [Theory]
    [InlineData("esto no es un polígono")]
    [InlineData("[[9999,9999],[9999,9998],[9998,9998],[9999,9999]]")]    // fuera de rango
    [InlineData("[[-34.8,-56.0],[-34.9,-56.1]]")]                        // solo 2 puntos
    [InlineData("[[NaN,NaN],[1,1],[2,2],[NaN,NaN]]")]                    // JSON inválido
    [InlineData("[[1e999,1],[1,1],[2,2]]")]                              // desborda double
    [InlineData("[]")]
    [InlineData("[[0,0]]")]
    [InlineData("[[\"a\",\"b\"],[1,1],[2,2]]")]
    [InlineData("{\"foo\":1}")]
    public void Poligono_Invalido_SeRechaza(string poligono)
    {
        Assert.Throws<DomainValidationException>(() => ContactoValidacion.ValidarPoligono(poligono));
        Assert.Throws<DomainValidationException>(() => new ZonaCobertura(Guid.NewGuid(), Guid.NewGuid(), "Zona", poligono));
    }

    [Theory]
    [InlineData("[[-34.8,-56.0],[-34.9,-56.1],[-34.9,-56.0],[-34.8,-56.0]]")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[-56.20,-34.90],[-56.15,-34.90],[-56.15,-34.92],[-56.20,-34.90]]]}")]
    public void Poligono_Valido_SeAcepta(string poligono) => Assert.Equal(poligono, ContactoValidacion.ValidarPoligono(poligono));

    [Fact]
    public void Zona_ActualizarPoligonoYColor_ValidaIgual()
    {
        var zona = new ZonaCobertura(Guid.NewGuid(), Guid.NewGuid(), "Zona", "[[-34.8,-56.0],[-34.9,-56.1],[-34.9,-56.0]]");
        Assert.Throws<DomainValidationException>(() => zona.ActualizarPoligono("basura"));
        Assert.Throws<DomainValidationException>(() => zona.ActualizarDatos("Zona", "javascript:alert(1)"));
        Assert.Throws<DomainValidationException>(() => zona.ActualizarDatos("<b>Zona</b>", "#000000"));
        zona.ActualizarDatos("Zona nueva", "#16A34A");
        Assert.Equal("#16A34A", zona.ColorHex);
    }

    // ---------- Comercio / Destinatario (lo que aceptaba la API) ----------

    private static Comercio NuevoComercio(string email = "envios@megastore.com.uy", string telefono = "099123456", string? direccion = "Av. Italia 3456")
        => new(Guid.NewGuid(), "MegaStore S.R.L.", "219998880011", email, telefono, "MegaStore", direccion);

    [Fact]
    public void Comercio_Valido_SeNormaliza()
    {
        var c = NuevoComercio("  Envios@MegaStore.com.uy ", "099 123 456");
        Assert.Equal("envios@megastore.com.uy", c.Email);
        Assert.Equal("099123456", c.Telefono);
    }

    [Theory]
    [InlineData("no-es-email", "099123456")]
    [InlineData("a@@b", "099123456")]
    [InlineData("envios@megastore.com.uy", "abc")]
    [InlineData("envios@megastore.com.uy", "'; --")]
    [InlineData("envios@megastore.com.uy", "29001122")]
    public void Comercio_ConContactoInvalido_NoSeCrea(string email, string telefono) =>
        Assert.Throws<DomainValidationException>(() => NuevoComercio(email, telefono));

    [Fact]
    public void Comercio_ConDireccionONombreConMarcado_NoSeCrea()
    {
        Assert.Throws<DomainValidationException>(() => NuevoComercio(direccion: "<script>alert(1)</script>"));
        Assert.Throws<DomainValidationException>(() => new Comercio(Guid.NewGuid(), "ZZ <b>x</b>", "123456789012", "a@empresa.com", "099123456"));
    }

    [Fact]
    public void Comercio_ActualizarContacto_ValidaIgual()
    {
        var c = NuevoComercio();
        Assert.Throws<DomainValidationException>(() => c.ActualizarContacto("a@@b", "099123456"));
        Assert.Throws<DomainValidationException>(() => c.ActualizarContacto("a@empresa.com", "123"));
        Assert.Throws<DomainValidationException>(() => c.ActualizarPerfil("MegaStore", "MegaStore", "a@empresa.com", "099123456", "123"));
        Assert.Equal("envios@megastore.com.uy", c.Email); // el estado no cambió
    }

    [Fact]
    public void Destinatario_ContactoOpcional_PeroSiVieneDebeSerValido()
    {
        var ok = new Destinatario(Guid.NewGuid(), "Ana", null, "ana@correo.com", "099 111 222");
        Assert.Equal("099111222", ok.Telefono);
        Assert.Null(new Destinatario(Guid.NewGuid(), "Ana").Email);
        Assert.Throws<DomainValidationException>(() => new Destinatario(Guid.NewGuid(), "Ana", null, "no@@@", null));
        Assert.Throws<DomainValidationException>(() => new Destinatario(Guid.NewGuid(), "Ana", null, null, "12345"));
        Assert.Throws<DomainValidationException>(() => ok.ActualizarContacto("x", "099111222"));
        Assert.Throws<DomainValidationException>(() => new Destinatario(Guid.NewGuid(), "<script>"));
    }
}
