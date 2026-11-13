using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

public class BajaComercioDominioTests
{
    private static Envio CrearEnvio() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Estandar",
        $"UM-{Guid.NewGuid().ToString("N")[..8]}",
        direccionEntregaId: Guid.NewGuid(),
        direccionDestino: "Av. 18 de Julio 1234",
        nombreDestinatario: "Destinatario Prueba",
        operadorId: Guid.NewGuid());

    [Fact]
    public void EnvioCancelar_DesdeAdmitido_CambiaACanceladoYRegistraEvento()
    {
        var envio = CrearEnvio();
        var usuarioId = Guid.NewGuid();

        envio.Cancelar("Comercio dado de baja", usuarioId, "Backoffice");

        Assert.Equal(EstadoEnvio.Cancelado, envio.EstadoActual);
        Assert.Null(envio.HojaDeRutaId);
        var evento = Assert.Single(envio.Eventos, e => e.TipoEvento == "EnvioCancelado");
        Assert.Equal(EstadoEnvio.Admitido, evento.EstadoAnterior);
        Assert.Equal(EstadoEnvio.Cancelado, evento.EstadoNuevo);
        Assert.Equal(usuarioId, evento.ResponsableUsuarioId);
        Assert.Equal("Comercio dado de baja", evento.Payload);
    }

    [Fact]
    public void EnvioCancelar_DesdeEnDeposito_CambiaACancelado()
    {
        var envio = CrearEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionDeposito");

        envio.Cancelar("Comercio dado de baja", null, "");

        Assert.Equal(EstadoEnvio.Cancelado, envio.EstadoActual);
        var evento = Assert.Single(envio.Eventos, e => e.TipoEvento == "EnvioCancelado");
        Assert.Equal("Backoffice", evento.Origen);
    }

    [Fact]
    public void EnvioCancelar_YaCancelado_EsIdempotenteYNoDuplicaEvento()
    {
        var envio = CrearEnvio();
        envio.Cancelar("Primera cancelación");
        var cantidadEventos = envio.Eventos.Count;

        envio.Cancelar("Segunda cancelación");

        Assert.Equal(EstadoEnvio.Cancelado, envio.EstadoActual);
        Assert.Equal(cantidadEventos, envio.Eventos.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnvioCancelar_MotivoVacioONulo_LanzaDomainValidationException(string? motivo)
    {
        var envio = CrearEnvio();

        Assert.Throws<DomainValidationException>(() => envio.Cancelar(motivo!));
    }

    [Fact]
    public void EnvioCancelar_MotivoMayorA500Caracteres_LanzaDomainValidationException()
    {
        var envio = CrearEnvio();
        var motivoLargo = new string('x', 501);

        Assert.Throws<DomainValidationException>(() => envio.Cancelar(motivoLargo));
    }

    [Theory]
    [InlineData(EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.EnTransito)]
    [InlineData(EstadoEnvio.Entregado)]
    [InlineData(EstadoEnvio.Devuelto)]
    [InlineData(EstadoEnvio.Extraviado)]
    public void EnvioCancelar_EstadosNoPermitidos_LanzaDomainValidationException(EstadoEnvio estadoActual)
    {
        var envio = CrearEnvio();
        if (estadoActual != EstadoEnvio.Admitido)
        {
            envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Test");
            if (estadoActual == EstadoEnvio.AsignadoARuta)
                envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "Test");
            else if (estadoActual == EstadoEnvio.EnTransito)
            {
                envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "Test");
                envio.TransicionarEstado(EstadoEnvio.EnTransito, "Test");
            }
            else if (estadoActual == EstadoEnvio.Entregado)
            {
                envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "Test");
                envio.TransicionarEstado(EstadoEnvio.EnTransito, "Test");
                envio.TransicionarEstado(EstadoEnvio.Entregado, "Test");
            }
            else if (estadoActual == EstadoEnvio.Devuelto)
            {
                envio.TransicionarEstado(EstadoEnvio.EnDevolucion, "Test");
                envio.TransicionarEstado(EstadoEnvio.Devuelto, "Test");
            }
            else if (estadoActual == EstadoEnvio.Extraviado)
            {
                envio.TransicionarEstado(EstadoEnvio.Extraviado, "Test");
            }
        }

        Assert.Throws<DomainValidationException>(() => envio.Cancelar("Motivo válido"));
    }

    [Theory]
    [InlineData("Borrador")]
    [InlineData("Liberada")]
    [InlineData("EnCarga")]
    public void HojaDeRutaCancelar_EstadosPreviosASalida_CancelaRutaParadasYAsignaciones(string estadoInicial)
    {
        var envioId = Guid.NewGuid();
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), estadoInicial);
        ruta.AgregarParada(Guid.NewGuid(), envioId, 1, Guid.NewGuid());
        ruta.RegistrarAsignacion(envioId, Guid.NewGuid());

        ruta.Cancelar("Sin paradas por baja de comercio");

        Assert.Equal("Cancelada", ruta.Estado);
        Assert.False(ruta.TieneParadasVigentes);
        Assert.All(ruta.Paradas, p => Assert.Equal("Cancelada", p.Estado));
        Assert.All(ruta.Asignaciones, a => Assert.False(a.Vigente));
    }

    [Fact]
    public void HojaDeRutaCancelar_YaCancelada_EsIdempotente()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Cancelada");

        ruta.Cancelar("Reintento");

        Assert.Equal("Cancelada", ruta.Estado);
    }

    [Theory]
    [InlineData("Despachada")]
    [InlineData("EnTransito")]
    [InlineData("Finalizada")]
    public void HojaDeRutaCancelar_YaEnCalleOFinalizada_LanzaDomainValidationException(string estadoEnCalle)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), estadoEnCalle);

        Assert.Throws<DomainValidationException>(() => ruta.Cancelar("Intento inválido"));
    }

    [Fact]
    public void HojaDeRutaRetirarEnvioYRetiro_EnBorrador_CancelaParadaDeEseItemYSostieneLaRuta()
    {
        var envioId = Guid.NewGuid();
        var retiroId = Guid.NewGuid();
        var otroEnvioId = Guid.NewGuid();
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), "Borrador");
        ruta.AgregarParada(Guid.NewGuid(), envioId, 1, Guid.NewGuid());
        ruta.RegistrarAsignacion(envioId, Guid.NewGuid());
        ruta.AgregarParadaRetiro(retiroId, 2, Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), otroEnvioId, 3, Guid.NewGuid());

        ruta.RetirarEnvio(envioId);
        ruta.RetirarRetiro(retiroId);

        Assert.Equal("Borrador", ruta.Estado);
        Assert.True(ruta.TieneParadasVigentes);
        Assert.Equal("Pendiente", Assert.Single(ruta.Paradas, p => p.EnvioId == otroEnvioId).Estado);
    }
}
