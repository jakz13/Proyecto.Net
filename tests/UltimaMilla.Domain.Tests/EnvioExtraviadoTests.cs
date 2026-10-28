using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class EnvioExtraviadoTests
{
    private static Envio NuevoEnvio() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-EXTRA0001");

    [Fact]
    public void MarcarExtraviado_DesdeAdmitido_RegistraEventoConMotivoYResponsable()
    {
        var envio = NuevoEnvio();
        var usuarioId = Guid.NewGuid();

        envio.MarcarExtraviado("  No apareció en el depósito  ", usuarioId);

        Assert.Equal(EstadoEnvio.Extraviado, envio.EstadoActual);
        var evento = envio.Eventos.Last();
        Assert.Equal("EnvioExtraviado", evento.TipoEvento);
        Assert.Equal(usuarioId, evento.ResponsableUsuarioId);
        Assert.Equal("No apareció en el depósito", evento.Payload);
    }

    [Fact]
    public void MarcarExtraviado_SinMotivo_LanzaDomainValidationYNoCambiaElEstado()
    {
        var envio = NuevoEnvio();

        Assert.Throws<DomainValidationException>(() => envio.MarcarExtraviado("  ", Guid.NewGuid()));

        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
    }

    [Fact]
    public void MarcarExtraviado_ConMotivoDemasiadoLargo_LanzaDomainValidation()
    {
        Assert.Throws<DomainValidationException>(() => NuevoEnvio().MarcarExtraviado(new string('x', 501), Guid.NewGuid()));
    }

    [Fact]
    public void MarcarExtraviado_UnaVezExtraviado_NoAdmiteOtraTransicion()
    {
        var envio = NuevoEnvio();
        envio.MarcarExtraviado("Perdido", Guid.NewGuid());

        Assert.Throws<DomainValidationException>(() => envio.MarcarExtraviado("Otra vez", Guid.NewGuid()));
        Assert.Throws<DomainValidationException>(() => envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion"));
    }
}
