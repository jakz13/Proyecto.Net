using Xunit;
using System.ComponentModel.DataAnnotations;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Retiros;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Contracts.Validaciones;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Las pantallas (backoffice y portal) muestran el texto rojo de error con estas reglas antes de enviar nada. Son las mismas que aplica
/// el dominio al guardar (ver ContactoValidacionTests): los casos de acá son los mismos para que no se desalineen.
/// </summary>
public class AtributosDeContactoTests
{
    private static List<ValidationResult> Validar(object modelo)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), resultados, validateAllProperties: true);
        return resultados;
    }

    private static string[] Mensajes(object modelo, string campo) =>
        Validar(modelo).Where(r => r.MemberNames.Contains(campo)).Select(r => r.ErrorMessage ?? "").ToArray();

    // ---------- los atributos solos ----------

    [Theory]
    [InlineData("nombre@empresa.com", true)]
    [InlineData("a.b-c+d@sub.empresa.com.uy", true)]
    [InlineData("no-es-email", false)]
    [InlineData("a@@b", false)]
    [InlineData("a@b", false)]
    [InlineData("a@b.c", false)]
    [InlineData(".a@empresa.com", false)]
    [InlineData("a..b@empresa.com", false)]
    [InlineData("a@-empresa.com", false)]
    [InlineData("nom bre@empresa.com", false)]
    public void EmailValido(string valor, bool esperado) => Assert.Equal(esperado, new EmailValidoAttribute().IsValid(valor));

    [Theory]
    [InlineData("099123456", true)]
    [InlineData("099 123 456", true)]
    [InlineData("099-123-456", true)]
    [InlineData("09912345", false)]
    [InlineData("0991234567", false)]
    [InlineData("abc", false)]
    [InlineData("+598 99 111 222", false)]
    [InlineData("(099) 123 456", false)]
    public void TelefonoNueveDigitos(string valor, bool esperado) => Assert.Equal(esperado, new TelefonoNueveDigitosAttribute().IsValid(valor));

    [Theory]
    [InlineData("Av. Italia 3456", true)]
    [InlineData("18 de Julio 1234", true)]
    [InlineData("12345", false)]
    [InlineData("<script>alert(1)</script>", false)]
    [InlineData("Calle\u0000Falsa", false)]
    public void DireccionValida(string valor, bool esperado) => Assert.Equal(esperado, new DireccionValidaAttribute().IsValid(valor));

    [Theory]
    [InlineData("1234", true)]
    [InlineData("1234 bis", true)]
    [InlineData("S/N", true)]
    [InlineData("<1>", false)]
    [InlineData("-5", false)]
    public void NumeroPuertaValido(string valor, bool esperado) => Assert.Equal(esperado, new NumeroPuertaValidoAttribute().IsValid(valor));

    [Theory]
    [InlineData("Montevideo", true)]
    [InlineData("San José de Mayo", true)]
    [InlineData("123", false)]
    [InlineData("M", false)]
    [InlineData("Mvd<b>", false)]
    public void CiudadValida(string valor, bool esperado) => Assert.Equal(esperado, new CiudadValidaAttribute().IsValid(valor));

    [Theory]
    [InlineData("[[-34.8,-56.0],[-34.9,-56.1],[-34.9,-56.0]]", true)]
    [InlineData("esto no es un polígono", false)]
    [InlineData("[[9999,9999],[9999,9998],[9998,9998]]", false)]
    [InlineData("[[-34.8,-56.0],[-34.9,-56.1]]", false)]
    [InlineData("[[NaN,NaN],[1,1],[2,2]]", false)]
    public void PoligonoValido(string valor, bool esperado) => Assert.Equal(esperado, new PoligonoValidoAttribute().IsValid(valor));

    [Theory]
    [InlineData("#2563EB", true)]
    [InlineData("#2563eb", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("#12", false)]
    public void ColorHexValido(string valor, bool esperado) => Assert.Equal(esperado, new ColorHexValidoAttribute().IsValid(valor));

    [Fact]
    public void LosCamposOpcionalesVaciosSonValidos()
    {
        Assert.True(new EmailValidoAttribute().IsValid(null));
        Assert.True(new EmailValidoAttribute().IsValid(""));
        Assert.True(new TelefonoNueveDigitosAttribute().IsValid(null));
        Assert.True(new TelefonoNueveDigitosAttribute().IsValid(""));
    }

    // ---------- los formularios: cada campo da su texto rojo ----------

    [Fact]
    public void CrearComercio_ConDatosBasura_MarcaEmailTelefonoYDireccion()
    {
        var dto = new CrearComercioDto
        {
            RazonSocial = "ZZ <b>x</b>", NombreFantasia = "ZZ", Rut = "123456789012",
            Email = "no-es-email", Telefono = "abc", Direccion = "<script>alert(1)</script>"
        };

        Assert.Contains(EmailValidoAttribute.Mensaje, Mensajes(dto, nameof(dto.Email)));
        Assert.Contains(TelefonoNueveDigitosAttribute.Mensaje, Mensajes(dto, nameof(dto.Telefono)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Direccion)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.RazonSocial)));
    }

    [Fact]
    public void CrearComercio_Valido_NoTieneErrores()
    {
        var dto = new CrearComercioDto
        {
            RazonSocial = "MegaStore S.R.L.", NombreFantasia = "MegaStore", Rut = "219998880011",
            Email = "envios@megastore.com.uy", Telefono = "099 123 456", Direccion = "Av. Italia 3456, Montevideo"
        };
        Assert.Empty(Validar(dto));
    }

    [Fact]
    public void ActualizarComercio_ValidaIgual()
    {
        var dto = new ActualizarComercioDto { RazonSocial = "MegaStore", NombreFantasia = "MegaStore", Email = "a@@b", Telefono = "123", Direccion = "1234" };
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Email)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Telefono)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Direccion)));
    }

    [Fact]
    public void CrearRepartidor_TelefonoYEmailInvalidos()
    {
        var dto = new CrearRepartidorDto { NombreCompleto = "Juan Pérez", Email = "x", Documento = "12345678", Telefono = "12345" };
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Email)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Telefono)));
    }

    [Fact]
    public void CrearEnvio_DatosDelDestinatarioYDireccionInvalidos()
    {
        var dto = new CrearEnvioDto
        {
            NombreDestinatario = "Ana", EmailDestinatario = "no@@@", TelefonoDestinatario = "12345",
            Calle = "12345", Numero = "<1>", Ciudad = "123"
        };
        Assert.NotEmpty(Mensajes(dto, nameof(dto.EmailDestinatario)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.TelefonoDestinatario)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Calle)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Numero)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Ciudad)));
    }

    [Fact]
    public void CrearEnvio_Valido_NoMarcaContactoNiDireccion()
    {
        var dto = new CrearEnvioDto
        {
            NombreDestinatario = "Ana Gómez", EmailDestinatario = "ana@correo.com", TelefonoDestinatario = "099 111 222",
            Calle = "18 de Julio", Numero = "1234", Ciudad = "Montevideo", DireccionDestino = "18 de Julio 1234"
        };
        foreach (var campo in new[] { nameof(dto.EmailDestinatario), nameof(dto.TelefonoDestinatario), nameof(dto.Calle), nameof(dto.Numero), nameof(dto.Ciudad), nameof(dto.DireccionDestino) })
            Assert.Empty(Mensajes(dto, campo));
    }

    [Fact]
    public void EnvioPorApi_DatosInvalidos()
    {
        var dto = new SolicitudAltaEnvioApiDto { NombreDestinatario = "Ana", Email = "a@@b", Telefono = "abc", Calle = "x\u0000", Numero = "", Ciudad = "<b>" };
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Email)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Telefono)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Calle)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Numero)));
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Ciudad)));
    }

    [Fact]
    public void SolicitarRetiro_CadaCampoDaSuError()
    {
        var dto = new SolicitarRetiroDto { NombreContacto = "", TelefonoContacto = "12", EmailContacto = "x", Calle = "", Numero = "", Ciudad = "", Motivo = "" };
        foreach (var campo in new[] { nameof(dto.NombreContacto), nameof(dto.TelefonoContacto), nameof(dto.EmailContacto), nameof(dto.Calle), nameof(dto.Numero), nameof(dto.Ciudad), nameof(dto.Motivo) })
            Assert.NotEmpty(Mensajes(dto, campo));
    }

    [Fact]
    public void CrearZona_PoligonoYColorBasura()
    {
        var dto = new CrearZonaCoberturaDto { Nombre = "Zona <x>", PoligonoGeografico = "esto no es un polígono", ColorHex = "javascript:alert(1)" };
        Assert.NotEmpty(Mensajes(dto, nameof(dto.Nombre)));
        Assert.Contains(PoligonoValidoAttribute.Mensaje, Mensajes(dto, nameof(dto.PoligonoGeografico)));
        Assert.Contains(ColorHexValidoAttribute.Mensaje, Mensajes(dto, nameof(dto.ColorHex)));
    }
}
