using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class MaquinaEstadosEnvioTests
{
    private static Envio NuevoEnvio() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-MAQUINA01");

    [Theory]
    [InlineData(EstadoEnvio.Admitido, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.EnDeposito, EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.AsignadoARuta, EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.AsignadoARuta, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.EnTransito, EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.EnTransito, EstadoEnvio.NoEntregado)]
    [InlineData(EstadoEnvio.NoEntregado, EstadoEnvio.Reprogramado)]
    [InlineData(EstadoEnvio.NoEntregado, EstadoEnvio.EnDevolucion)]
    [InlineData(EstadoEnvio.Reprogramado, EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnDevolucion, EstadoEnvio.Devuelto)]
    [InlineData(EstadoEnvio.EnDevolucion, EstadoEnvio.Extraviado)]
    public void TransicionesDeclaradas_SonValidas(EstadoEnvio origen, EstadoEnvio destino)
    {
        Assert.True(MaquinaEstadosEnvio.EsTransicionValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoEnvio.Admitido, EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.Admitido, EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.EnDeposito, EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.Entregado, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.Devuelto, EstadoEnvio.Admitido)]
    [InlineData(EstadoEnvio.Extraviado, EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.Reprogramado, EstadoEnvio.EnTransito)]
    public void TransicionesNoDeclaradas_SonInvalidas(EstadoEnvio origen, EstadoEnvio destino)
    {
        Assert.False(MaquinaEstadosEnvio.EsTransicionValida(origen, destino));
    }

    [Fact]
    public void MismoEstado_NoEsTransicionValida()
    {
        foreach (var estado in Enum.GetValues<EstadoEnvio>())
            Assert.False(MaquinaEstadosEnvio.EsTransicionValida(estado, estado));
    }

    [Fact]
    public void EstadoFueraDelEnum_NoEsValidoNiTieneDestinos()
    {
        var invalido = (EstadoEnvio)999;
        Assert.False(MaquinaEstadosEnvio.EsTransicionValida(invalido, EstadoEnvio.EnDeposito));
        Assert.False(MaquinaEstadosEnvio.EsTransicionValida(EstadoEnvio.Admitido, invalido));
        Assert.Empty(MaquinaEstadosEnvio.DestinosPermitidos(invalido));
    }

    [Fact]
    public void TodosLosEstados_SonTerminalesOTienenDestinos()
    {
        foreach (var estado in Enum.GetValues<EstadoEnvio>())
            Assert.True(MaquinaEstadosEnvio.EsEstadoTerminal(estado) || MaquinaEstadosEnvio.DestinosPermitidos(estado).Count > 0);
    }

    [Theory]
    [InlineData(EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.Devuelto)]
    [InlineData(EstadoEnvio.Extraviado)]
    public void EstadosTerminales_NoTienenDestinos(EstadoEnvio estado)
    {
        Assert.True(MaquinaEstadosEnvio.EsEstadoTerminal(estado));
        Assert.Empty(MaquinaEstadosEnvio.DestinosPermitidos(estado));
    }

    [Fact]
    public void Envio_TransicionInvalida_LanzaExcepcionYNoAlteraEstadoNiEventos()
    {
        var envio = NuevoEnvio();
        var sello = envio.SelloModificacion;

        var ex = Assert.Throws<DomainValidationException>(() =>
            envio.TransicionarEstado(EstadoEnvio.Entregado, "EntregaIndebida"));

        Assert.Contains("Transición de estado inválida", ex.Message);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Single(envio.Eventos);
        Assert.Equal(sello, envio.SelloModificacion);
    }

    [Fact]
    public void Envio_TransicionRepetida_SeRechaza()
    {
        var envio = NuevoEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion");

        Assert.Throws<DomainValidationException>(() =>
            envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion"));
        Assert.Equal(2, envio.Eventos.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Envio_TipoEventoVacio_LanzaExcepcion(string? tipoEvento)
    {
        var envio = NuevoEnvio();

        Assert.Throws<DomainValidationException>(() =>
            envio.TransicionarEstado(EstadoEnvio.EnDeposito, tipoEvento!));
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
    }

    [Fact]
    public void Envio_TransicionValida_RegistraEventoConResponsableYGeolocalizacion()
    {
        var envio = NuevoEnvio();
        var usuario = Guid.NewGuid();

        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion", usuario, "Backoffice", -34.9, -56.1, "ok");

        var evento = envio.Eventos.Last();
        Assert.Equal(EstadoEnvio.Admitido, evento.EstadoAnterior);
        Assert.Equal(EstadoEnvio.EnDeposito, evento.EstadoNuevo);
        Assert.Equal(usuario, evento.ResponsableUsuarioId);
        Assert.Equal("Backoffice", evento.Origen);
        Assert.Equal(-34.9, evento.Latitud);
    }

    [Fact]
    public void Envio_CicloCompletoHastaEntregado_EsValidoYLuegoQuedaCerrado()
    {
        var envio = NuevoEnvio();
        var ruta = Guid.NewGuid();

        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion");
        envio.AsignarRuta(ruta);
        envio.DespacharEnRuta(ruta);
        envio.TransicionarEstado(EstadoEnvio.Entregado, "Entrega");

        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal(5, envio.Eventos.Count);
        Assert.Throws<DomainValidationException>(() =>
            envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Reapertura"));
    }
}
