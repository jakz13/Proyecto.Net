using UltimaMilla.Application.Commands.Deposito.MarcarEnvioExtraviado;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class MarcarEnvioExtraviadoCommandHandlerTests
{
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();

    private MarcarEnvioExtraviadoCommandHandler CrearHandler() => new(_envios, _rutas, _outbox, _uow, _tenant);

    private Envio CrearEnvio(Guid? operadorId = null)
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-EXTRA0002", operadorId: operadorId ?? _tenant.OperadorId);
        _envios.Envios.Add(envio);
        return envio;
    }

    [Fact]
    public async Task EnvioEnDeposito_QuedaExtraviadoYPublicaElEvento()
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(envio.Id, "Se perdio en el deposito"), default);

        Assert.Equal("Extraviado", resultado.Estado);
        Assert.False(resultado.RetiradoDeRuta);
        Assert.Equal(EstadoEnvio.Extraviado, envio.EstadoActual);
        Assert.Equal(1, _uow.Confirmaciones);
        var mensaje = Assert.Single(_outbox.Mensajes);
        Assert.Equal("EnvioExtraviadoIntegrationEvent", mensaje.TipoEvento);
        Assert.Contains("Se perdio en el deposito", mensaje.Payload);
    }

    [Fact]
    public async Task EnvioAsignadoARuta_SeRetiraDeLaRutaAntesDeMarcarlo()
    {
        var envio = CrearEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(envio.Id, 1, Guid.NewGuid());
        ruta.RegistrarAsignacion(envio.Id, Guid.NewGuid());
        ruta.Liberar();
        envio.AsignarRuta(ruta.Id);
        _rutas.Rutas.Add(ruta);

        var resultado = await CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(envio.Id, "No estaba al cargar"), default);

        Assert.True(resultado.RetiradoDeRuta);
        Assert.All(ruta.Paradas, p => Assert.Equal("Cancelada", p.Estado));
        Assert.Null(envio.HojaDeRutaId);
        Assert.Equal(EstadoEnvio.Extraviado, envio.EstadoActual);
    }

    [Fact]
    public async Task EnvioDeOtroOperador_LanzaUnauthorizedYNoCambiaNada()
    {
        var envio = CrearEnvio(operadorId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(envio.Id, "Intento ajeno"), default));

        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Empty(_outbox.Mensajes);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task EnvioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(Guid.NewGuid(), "Motivo"), default));
    }

    [Fact]
    public async Task SinMotivo_LanzaDomainValidationYNoPublicaNada()
    {
        var envio = CrearEnvio();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(envio.Id, " "), default));

        Assert.Empty(_outbox.Mensajes);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
    }

    [Fact]
    public async Task EnvioYaEntregado_NoSePuedeDeclararExtraviado()
    {
        var envio = CrearEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "RecepcionEnDeposito");
        envio.TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionRuta");
        envio.TransicionarEstado(EstadoEnvio.EnTransito, "Despacho");
        envio.TransicionarEstado(EstadoEnvio.Entregado, "Entrega");

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new MarcarEnvioExtraviadoCommand(envio.Id, "Motivo"), default));
    }
}
