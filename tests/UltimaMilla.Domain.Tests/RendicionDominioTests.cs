using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class RendicionDominioTests
{
    private static MotivoNoEntrega MotivoInterno(bool esInterno = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "RETORNO_SIN_INTENTO", "Retorno sin intento", esInterno);

    private static Envio EnvioEnTransito()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-RENDDOM01");
        envio.AgregarBulto("TRK-RENDDOM01-B01", 1m, 10m, 10m, 10m);
        envio.RecepcionarEnDeposito(new[] { "TRK-RENDDOM01-B01" }, null, null, Guid.NewGuid());
        envio.AsignarRuta(Guid.NewGuid());
        envio.DespacharEnRuta(envio.HojaDeRutaId!.Value);
        return envio;
    }

    [Fact]
    public void MaquinaDeEstados_PermiteVolverADepositoDesdeNoEntregado()
    {
        Assert.True(MaquinaEstadosEnvio.EsTransicionValida(EstadoEnvio.NoEntregado, EstadoEnvio.EnDeposito));
    }

    [Fact]
    public void RetornoSinIntento_RegistraNoEntregadoYVuelveADepositoSinCrearIntento()
    {
        var envio = EnvioEnTransito();

        envio.RegistrarRetornoSinIntento(MotivoInterno(), Guid.NewGuid(), "Sin tiempo");

        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);
        Assert.Null(envio.HojaDeRutaId);
        Assert.Empty(envio.IntentosEntrega);
        Assert.Equal("Recibido", envio.Bultos.Single().EstadoBulto);
        Assert.Contains(envio.Eventos, e => e.EstadoNuevo == EstadoEnvio.NoEntregado && e.TipoEvento == "RetornoSinIntento");
    }

    [Fact]
    public void RetornoSinIntento_ConMotivoNoInterno_LanzaExcepcionSinCambios()
    {
        var envio = EnvioEnTransito();

        Assert.Throws<DomainValidationException>(() => envio.RegistrarRetornoSinIntento(MotivoInterno(esInterno: false), Guid.NewGuid()));
        Assert.Equal(EstadoEnvio.EnTransito, envio.EstadoActual);
    }

    [Fact]
    public void RetornoSinIntento_ConMotivoNulo_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => EnvioEnTransito().RegistrarRetornoSinIntento(null!, Guid.NewGuid()));
    }

    [Fact]
    public void RetornoSinIntento_FueraDeTransito_LanzaExcepcion()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-RENDDOM02");

        Assert.Throws<DomainValidationException>(() => envio.RegistrarRetornoSinIntento(MotivoInterno(), Guid.NewGuid()));
    }

    [Fact]
    public void ReintegrarADeposito_SoloAplicaAEnviosReprogramados()
    {
        var envio = EnvioEnTransito();

        Assert.Throws<DomainValidationException>(() => envio.ReintegrarADepositoPorRendicion(Guid.NewGuid()));
    }

    [Fact]
    public void Rendir_RutaNoDespachada_LanzaExcepcion()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 16));

        Assert.Throws<DomainValidationException>(() => ruta.Rendir());
    }

    [Fact]
    public void Rendir_DosVeces_LanzaExcepcion()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 16), "Despachada");
        ruta.Rendir();

        Assert.Equal("Rendida", ruta.Estado);
        Assert.Throws<DomainValidationException>(() => ruta.Rendir());
    }

    [Fact]
    public void MotivoNoEntrega_PorDefectoNoEsInterno()
    {
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), Guid.NewGuid(), "AUSENTE", "Ausente");

        Assert.False(motivo.EsInterno);
    }
}
