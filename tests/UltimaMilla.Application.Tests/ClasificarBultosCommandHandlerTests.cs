using UltimaMilla.Application.Commands.Deposito.ClasificarBultos;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ClasificarBultosCommandHandlerTests
{
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();

    private ClasificarBultosCommandHandler CrearHandler() => new(_envios, _uow, _tenant, _outbox);

    private Envio CrearEnvioEnDeposito(int bultos = 2, Guid? operadorId = null, bool recepcionar = true)
    {
        var envio = new Envio(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-CLASIF001",
            operadorId: operadorId ?? _tenant.OperadorId);

        for (var i = 1; i <= bultos; i++)
            envio.AgregarBulto($"TRK-CLASIF001-B{i:00}", 1m, 10m, 10m, 10m);

        if (recepcionar)
            envio.RecepcionarEnDeposito(Codigos(bultos), null, null, Guid.NewGuid());

        _envios.Envios.Add(envio);
        return envio;
    }

    private static List<string> Codigos(int cantidad) =>
        Enumerable.Range(1, cantidad).Select(i => $"TRK-CLASIF001-B{i:00}").ToList();

    private static ClasificacionBultoDto Dto(int n, string prioridad = "Normal", Guid? zona = null, string? tipo = null) =>
        new($"TRK-CLASIF001-B{n:00}", prioridad, zona, tipo);

    private Task<ClasificacionResultDto> Ejecutar(Guid envioId, params ClasificacionBultoDto[] bultos) =>
        CrearHandler().Handle(new ClasificarBultosCommand(envioId, bultos), CancellationToken.None);

    [Fact]
    public async Task ClasificarTodosLosBultos_DejaElEnvioListoParaHojaDeRuta()
    {
        var envio = CrearEnvioEnDeposito();
        var zona = Guid.NewGuid();

        var resultado = await Ejecutar(envio.Id, Dto(1, "Alta", zona, "Fragil"), Dto(2, "Baja"));

        Assert.True(resultado.ListoParaHojaDeRuta);
        Assert.Equal(2, resultado.TotalClasificados);
        Assert.Empty(resultado.BultosPendientes);
        Assert.True(envio.EstaClasificado);
        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);

        var primero = envio.Bultos.Single(b => b.Identificacion.EndsWith("B01"));
        Assert.Equal(PrioridadBulto.Alta, primero.Prioridad);
        Assert.Equal(zona, primero.ZonaClasificadaId);
        Assert.Equal("Fragil", primero.TipoClasificacion);
        Assert.NotNull(primero.FechaClasificacion);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ClasificarSoloAlgunosBultos_InformaPendientes()
    {
        var envio = CrearEnvioEnDeposito();

        var resultado = await Ejecutar(envio.Id, Dto(1));

        Assert.False(resultado.ListoParaHojaDeRuta);
        Assert.Equal(1, resultado.TotalClasificados);
        Assert.Equal("TRK-CLASIF001-B02", Assert.Single(resultado.BultosPendientes));
        Assert.False(envio.EstaClasificado);
    }

    [Fact]
    public async Task Clasificar_RegistraEventoSinCambiarElEstado()
    {
        var envio = CrearEnvioEnDeposito();
        var eventosAntes = envio.Eventos.Count;

        await Ejecutar(envio.Id, Dto(1), Dto(2));

        Assert.Equal(eventosAntes + 1, envio.Eventos.Count);
        var evento = envio.Eventos.Last();
        Assert.Equal("ClasificacionBultos", evento.TipoEvento);
        Assert.Equal(EstadoEnvio.EnDeposito, evento.EstadoAnterior);
        Assert.Equal(EstadoEnvio.EnDeposito, evento.EstadoNuevo);
        Assert.Equal(_tenant.UsuarioId, evento.ResponsableUsuarioId);
    }

    [Fact]
    public async Task Reclasificar_ActualizaLosValoresDelBulto()
    {
        var envio = CrearEnvioEnDeposito(bultos: 1);
        await Ejecutar(envio.Id, Dto(1, "Baja", tipo: "Documento"));

        await Ejecutar(envio.Id, Dto(1, "Urgente", tipo: "  "));

        var bulto = envio.Bultos.Single();
        Assert.Equal(PrioridadBulto.Urgente, bulto.Prioridad);
        Assert.Null(bulto.TipoClasificacion);
    }

    [Theory]
    [InlineData("urgente", PrioridadBulto.Urgente)]
    [InlineData("  ALTA ", PrioridadBulto.Alta)]
    public async Task Prioridad_AceptaCualquierCapitalizacionYEspacios(string texto, PrioridadBulto esperado)
    {
        var envio = CrearEnvioEnDeposito(bultos: 1);

        await Ejecutar(envio.Id, Dto(1, texto));

        Assert.Equal(esperado, envio.Bultos.Single().Prioridad);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Critica")]
    [InlineData("3")]
    [InlineData("99")]
    public async Task PrioridadInvalida_LanzaDomainValidationExceptionSinGuardar(string? prioridad)
    {
        var envio = CrearEnvioEnDeposito(bultos: 1);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, Dto(1, prioridad!)));

        Assert.False(envio.Bultos.Single().EstaClasificado);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task UnBultoInvalidoEnElLote_NoClasificaNingunoDeLosDemas()
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(envio.Id, Dto(1, "Alta"), new ClasificacionBultoDto("NO-EXISTE", "Normal")));

        Assert.All(envio.Bultos, b => Assert.False(b.EstaClasificado));
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task TipoDemasiadoLargoEnUnBulto_NoClasificaNingunoDelLote()
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(envio.Id, Dto(1, "Alta"), Dto(2, "Alta", tipo: new string('x', 51))));

        Assert.All(envio.Bultos, b => Assert.False(b.EstaClasificado));
    }

    [Fact]
    public async Task BultoRepetidoEnElLote_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(envio.Id, Dto(1, "Alta"), Dto(1, "Baja")));
    }

    [Fact]
    public async Task ZonaVacia_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDeposito(bultos: 1);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, Dto(1, "Alta", Guid.Empty)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CodigoDeBultoVacio_LanzaDomainValidationException(string? codigo)
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Ejecutar(envio.Id, new ClasificacionBultoDto(codigo!, "Alta")));
    }

    [Fact]
    public async Task BultoFaltanteEnRecepcion_NoSePuedeClasificar()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-CLASIF001", operadorId: _tenant.OperadorId);
        envio.AgregarBulto("TRK-CLASIF001-B01", 1m, 1m, 1m, 1m);
        envio.AgregarBulto("TRK-CLASIF001-B02", 1m, 1m, 1m, 1m);
        envio.RecepcionarEnDeposito(new[] { "TRK-CLASIF001-B01" }, null, "Falta un bulto", Guid.NewGuid());
        _envios.Envios.Add(envio);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, Dto(2)));

        var resultado = await Ejecutar(envio.Id, Dto(1));
        Assert.True(resultado.ListoParaHojaDeRuta);
        Assert.Equal(1, resultado.TotalBultosRecibidos);
    }

    [Fact]
    public async Task BultoDanado_PuedeClasificarse()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-CLASIF001", operadorId: _tenant.OperadorId);
        envio.AgregarBulto("TRK-CLASIF001-B01", 1m, 1m, 1m, 1m);
        envio.RecepcionarEnDeposito(new[] { "TRK-CLASIF001-B01" }, new[] { "TRK-CLASIF001-B01" }, "Golpeado", Guid.NewGuid());
        _envios.Envios.Add(envio);

        var resultado = await Ejecutar(envio.Id, Dto(1));

        Assert.True(resultado.ListoParaHojaDeRuta);
    }

    [Fact]
    public async Task EnvioSinRecepcionar_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDeposito(recepcionar: false);

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, Dto(1)));
    }

    [Fact]
    public async Task EnvioYaAsignadoARuta_NoSePuedeReclasificar()
    {
        var envio = CrearEnvioEnDeposito();
        envio.AsignarRuta(Guid.NewGuid());

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, Dto(1)));
    }

    [Fact]
    public async Task ListaDeBultosVaciaONula_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new ClasificarBultosCommand(envio.Id, null!), CancellationToken.None));
    }

    [Fact]
    public async Task ElementoNuloEnLaLista_LanzaDomainValidationException()
    {
        var envio = CrearEnvioEnDeposito();

        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(envio.Id, new ClasificacionBultoDto[] { null! }));
    }

    [Fact]
    public async Task EnvioInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Ejecutar(Guid.NewGuid(), Dto(1)));
    }

    [Fact]
    public async Task IdentificadorVacio_LanzaDomainValidationException()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => Ejecutar(Guid.Empty, Dto(1)));
    }

    [Fact]
    public async Task ComandoNulo_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task EnvioDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var envio = CrearEnvioEnDeposito(operadorId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(envio.Id, Dto(1)));
        Assert.All(envio.Bultos, b => Assert.False(b.EstaClasificado));
    }

    [Fact]
    public async Task UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        var envio = CrearEnvioEnDeposito();
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ejecutar(envio.Id, Dto(1)));
    }
}
