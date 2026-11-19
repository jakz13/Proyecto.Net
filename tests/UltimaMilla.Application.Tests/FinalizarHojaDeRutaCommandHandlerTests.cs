using UltimaMilla.Application.Commands.Mobile.FinalizarRuta;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>La app solo finaliza la hoja y declara lo que devuelve: la rendición la cierra el operario en el depósito.</summary>
public class FinalizarHojaDeRutaCommandHandlerTests
{
    private static readonly DateOnly Fecha = new(2026, 10, 16);

    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();

    private FinalizarHojaDeRutaCommandHandler CrearHandler() => new(_rutas, _outbox, _uow, _tenant, _clock);

    private HojaDeRuta CrearRuta(string estadoDeseado)
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), _tenant.OperadorId, Fecha);
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        var envioId = Guid.NewGuid();
        ruta.AgregarParada(envioId, 1, Guid.NewGuid());
        ruta.RegistrarAsignacion(envioId, Guid.NewGuid());
        ruta.Liberar();

        if (estadoDeseado is "EnCarga" or "Despachada" or "EnTransito")
            ruta.RegistrarCargaEscaneada(new[] { Guid.NewGuid() });
        if (estadoDeseado is "Despachada" or "EnTransito")
            ruta.Despachar();
        if (estadoDeseado == "EnTransito")
            ruta.IniciarRecorrido();

        _rutas.Rutas.Add(ruta);
        return ruta;
    }

    private Task<FinalizacionResultDto> Finalizar(HojaDeRuta ruta, params string[] codigos) =>
        CrearHandler().Handle(new FinalizarHojaDeRutaCommand(ruta.Id, codigos, "Vuelvo al depósito"), CancellationToken.None);

    [Fact]
    public async Task RutaEnTransito_QuedaFinalizada_ySinRendir()
    {
        var ruta = CrearRuta("EnTransito");

        var resultado = await Finalizar(ruta, "trk-1-b01", " TRK-1-B02 ", "TRK-1-B01");

        Assert.Equal("Finalizada", resultado.EstadoRuta);
        Assert.Equal("Finalizada", ruta.Estado);
        Assert.NotEqual("Rendida", ruta.Estado);
        Assert.False(resultado.Idempotente);
        Assert.Equal(2, resultado.BultosDeclaradosADevolver);
        Assert.Contains(_outbox.Mensajes, m => m.TipoEvento == "RutaFinalizadaIntegrationEvent");
    }

    [Fact]
    public async Task FinalizarDosVeces_EsIdempotente()
    {
        var ruta = CrearRuta("Despachada");
        await Finalizar(ruta);

        var segunda = await Finalizar(ruta);

        Assert.True(segunda.Idempotente);
        Assert.Equal("Finalizada", segunda.EstadoRuta);
        Assert.Single(_outbox.Mensajes, m => m.TipoEvento == "RutaFinalizadaIntegrationEvent");
    }

    [Fact]
    public async Task RutaSinDespachar_NoSePuedeFinalizar()
    {
        var ruta = CrearRuta("EnCarga");

        await Assert.ThrowsAsync<DomainValidationException>(() => Finalizar(ruta));
        Assert.Equal("EnCarga", ruta.Estado);
    }

    [Fact]
    public async Task RutaDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var ruta = new HojaDeRuta(Guid.NewGuid(), Guid.NewGuid(), Fecha);
        _rutas.Rutas.Add(ruta);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Finalizar(ruta));
    }

    [Fact]
    public async Task RutaInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new FinalizarHojaDeRutaCommand(Guid.NewGuid(), Array.Empty<string>()), CancellationToken.None));
    }

    [Fact]
    public async Task IdentificadorVacio_LanzaDomainValidationException()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new FinalizarHojaDeRutaCommand(Guid.Empty, Array.Empty<string>()), CancellationToken.None));
    }
}
