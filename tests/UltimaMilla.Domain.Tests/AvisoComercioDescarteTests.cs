using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>
/// Un aviso a comercio con los reintentos agotados espera una decisión del administrador en la cola de fallidos (letra 6.9):
/// reenviarlo o descartarlo con un motivo. Sin descarte quedaría fallido para siempre y el tablero técnico no volvería nunca a verde.
/// </summary>
public class AvisoComercioDescarteTests
{
    private static AvisoComercio NuevoAviso() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{\"evento\":\"x\"}", "firma");

    private static AvisoComercio AvisoAgotado()
    {
        var aviso = NuevoAviso();
        aviso.MarcarAgotado("HTTP 500 Internal Server Error");
        return aviso;
    }

    [Theory]
    [InlineData("Agotado", true)]
    [InlineData("Fallido", true)]
    [InlineData("Reintentando", false)]
    [InlineData("Pendiente", false)]
    [InlineData("Entregado", false)]
    [InlineData("Descartado", false)]
    public void SoloLosAvisosAgotadosOFallidosEstanEnLaColaDeFallidos(string estado, bool enCola) =>
        Assert.Equal(enCola, AvisoComercio.EsEstadoDeColaDeFallidos(estado));

    [Fact]
    public void Descartar_UnAvisoAgotado_QuedaDescartadoConElMotivoYLaUltimaRespuesta()
    {
        var aviso = AvisoAgotado();

        aviso.Descartar("El comercio dio de baja su integración");

        Assert.Equal("Descartado", aviso.Estado);
        Assert.Null(aviso.ProximoReintento);
        Assert.Contains("El comercio dio de baja su integración", aviso.UltimaRespuestaHttp);
        Assert.Contains("HTTP 500 Internal Server Error", aviso.UltimaRespuestaHttp);
        Assert.False(AvisoComercio.EsEstadoDeColaDeFallidos(aviso.Estado));
    }

    [Fact]
    public void Descartar_SinMotivo_Falla()
    {
        var aviso = AvisoAgotado();

        Assert.Throws<DomainValidationException>(() => aviso.Descartar("   "));
        Assert.Equal("Agotado", aviso.Estado);
    }

    [Theory]
    [InlineData("Reintentando")]
    [InlineData("Entregado")]
    [InlineData("Descartado")]
    public void Descartar_UnAvisoQueNoEstaEnLaCola_Falla(string estado)
    {
        var aviso = NuevoAviso();
        switch (estado)
        {
            case "Reintentando": aviso.RegistrarFallo("HTTP 500", DateTime.UtcNow.AddMinutes(1)); break;
            case "Entregado": aviso.MarcarEntregado("HTTP 200 OK"); break;
            case "Descartado": aviso.MarcarAgotado("HTTP 500"); aviso.Descartar("ya no aplica"); break;
        }

        Assert.Throws<DomainValidationException>(() => aviso.Descartar("motivo"));
        Assert.Equal(estado, aviso.Estado);
    }

    [Fact]
    public void Descartar_ConUnMotivoMuyLargo_NoSuperaElLargoDeLaColumna()
    {
        var aviso = AvisoAgotado();

        aviso.Descartar(new string('x', 5000));

        Assert.True(aviso.UltimaRespuestaHttp!.Length <= 1000);
    }
}
