using UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class RecepcionarBultosCommandHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentTenant _tenant = new();

    private RecepcionarBultosCommandHandler CrearHandler() => new(_envios, _uow, _tenant);

    private Envio CrearEnvio(int cantidadBultos = 2, Guid? operadorId = null)
    {
        var envio = new Envio(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-RECEP0001",
            operadorId: operadorId ?? _tenant.OperadorId);

        for (var i = 1; i <= cantidadBultos; i++)
            envio.AgregarBulto($"TRK-RECEP0001-B{i:00}", 1m, 10m, 10m, 10m);

        _envios.Envios.Add(envio);
        return envio;
    }

    private static List<string> Codigos(int cantidad) =>
        Enumerable.Range(1, cantidad).Select(i => $"TRK-RECEP0001-B{i:00}").ToList();

    [Fact]
    public async Task EscaneoCompleto_PasaAEnDepositoSinDiscrepancias()
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.False(resultado.TieneDiscrepancias);
        Assert.False(resultado.Idempotente);
        Assert.Equal("EnDeposito", resultado.Estado);
        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);
        Assert.Empty(envio.Discrepancias);
        Assert.All(envio.Bultos, b => Assert.Equal("Recibido", b.EstadoBulto));
        Assert.Equal(1, _uow.Confirmaciones);

        var evento = envio.Eventos.Last();
        Assert.Equal("RecepcionEnDeposito", evento.TipoEvento);
        Assert.Equal(_tenant.UsuarioId, evento.ResponsableUsuarioId);
    }

    [Fact]
    public async Task CodigosEnMinusculasYConEspacios_SeNormalizan()
    {
        var envio = CrearEnvio();
        var codigos = Codigos(2).Select(c => $"  {c.ToLowerInvariant()} ").ToList();

        var resultado = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, codigos), CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.False(resultado.TieneDiscrepancias);
    }

    [Fact]
    public async Task CodigosDuplicadosEnElEscaneo_NoGeneranSobrantes()
    {
        var envio = CrearEnvio();
        var codigos = Codigos(2).Concat(Codigos(2)).ToList();

        var resultado = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, codigos), CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.Empty(resultado.Sobrantes);
    }

    [Fact]
    public async Task Faltante_SinObservaciones_NoRecepcionaYNoModificaNada()
    {
        var envio = CrearEnvio();
        var eventosAntes = envio.Eventos.Count;

        var resultado = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, Codigos(1)), CancellationToken.None);

        Assert.False(resultado.Recepcionado);
        Assert.True(resultado.TieneDiscrepancias);
        Assert.Single(resultado.Faltantes);
        Assert.Equal("TRK-RECEP0001-B02", resultado.Faltantes[0]);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Equal(eventosAntes, envio.Eventos.Count);
        Assert.Empty(envio.Discrepancias);
        Assert.All(envio.Bultos, b => Assert.Equal("Ingresado", b.EstadoBulto));
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Faltante_ConObservaciones_RecepcionaYRegistraDiscrepancia()
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(1), Observaciones: "El comercio confirma que enviará el segundo bulto mañana."),
            CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);
        var discrepancia = Assert.Single(envio.Discrepancias);
        Assert.Equal("Faltante", discrepancia.TipoDiscrepancia);
        Assert.Equal(_tenant.UsuarioId, discrepancia.UsuarioDeposito);
        Assert.Equal("FaltanteEnRecepcion", envio.Bultos.Single(b => b.Identificacion.EndsWith("B02")).EstadoBulto);
        Assert.Equal("Recibido", envio.Bultos.Single(b => b.Identificacion.EndsWith("B01")).EstadoBulto);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Sobrante_ConObservaciones_RegistraDiscrepanciaSinBulto()
    {
        var envio = CrearEnvio();
        var codigos = Codigos(2).Append("OTRO-CODIGO").ToList();

        var resultado = await CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, codigos, Observaciones: "Bulto ajeno apilado junto al envío."),
            CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.Equal("OTRO-CODIGO", Assert.Single(resultado.Sobrantes));
        var discrepancia = Assert.Single(envio.Discrepancias);
        Assert.Equal("Sobrante", discrepancia.TipoDiscrepancia);
        Assert.Null(discrepancia.BultoId);
    }

    [Fact]
    public async Task BultoDanado_ConObservaciones_SeMarcaYSeDocumenta()
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(2), new[] { "TRK-RECEP0001-B01" }, "Caja golpeada, contenido intacto."),
            CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.Equal("Danado", envio.Bultos.Single(b => b.Identificacion.EndsWith("B01")).EstadoBulto);
        Assert.Equal("Danado", envio.Discrepancias.Single().TipoDiscrepancia);
        Assert.Single(resultado.Danados);
        Assert.Single(resultado.Correctos);
    }

    [Fact]
    public async Task BultoDanado_SinObservaciones_NoRecepciona()
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(2), new[] { "TRK-RECEP0001-B01" }),
            CancellationToken.None);

        Assert.False(resultado.Recepcionado);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ObservacionesVaciasONulas_NoDocumentanLaDiscrepancia(string? observaciones)
    {
        var envio = CrearEnvio();

        var resultado = await CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(1), Observaciones: observaciones),
            CancellationToken.None);

        Assert.False(resultado.Recepcionado);
    }

    [Fact]
    public async Task DanadoNoEscaneado_LanzaDomainValidationException()
    {
        var envio = CrearEnvio();

        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(1), new[] { "TRK-RECEP0001-B02" }, "Dañado"),
            CancellationToken.None));
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task DanadoAjenoAlEnvio_LanzaDomainValidationException()
    {
        var envio = CrearEnvio();

        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(
            new RecepcionarBultosCommand(envio.Id, Codigos(2), new[] { "NO-EXISTE" }, "Dañado"),
            CancellationToken.None));
    }

    [Fact]
    public async Task ListaEscaneadaVaciaONula_SeTrataComoTodosFaltantes()
    {
        var envio = CrearEnvio();

        var vacia = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, new List<string>()), CancellationToken.None);
        var nula = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, null!), CancellationToken.None);

        Assert.False(vacia.Recepcionado);
        Assert.Equal(2, vacia.Faltantes.Count);
        Assert.False(nula.Recepcionado);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
    }

    [Fact]
    public async Task EntradasConValoresVacios_SeIgnoran()
    {
        var envio = CrearEnvio();
        var codigos = Codigos(2).Concat(new[] { "", "   " }).ToList();

        var resultado = await CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, codigos), CancellationToken.None);

        Assert.True(resultado.Recepcionado);
        Assert.Empty(resultado.Sobrantes);
    }

    [Fact]
    public async Task Reintento_SobreEnvioYaRecepcionado_EsIdempotente()
    {
        var envio = CrearEnvio();
        var handler = CrearHandler();
        await handler.Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None);
        var eventos = envio.Eventos.Count;

        var segundo = await handler.Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None);

        Assert.True(segundo.Recepcionado);
        Assert.True(segundo.Idempotente);
        Assert.Equal(eventos, envio.Eventos.Count);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task EnvioEnOtroEstado_LanzaDomainValidationException()
    {
        var envio = CrearEnvio();
        envio.TransicionarEstado(EstadoEnvio.EnDeposito, "Recepcion");
        envio.AsignarRuta(Guid.NewGuid());

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None));
    }

    [Fact]
    public async Task EnvioInexistente_LanzaNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(Guid.NewGuid(), Codigos(1)), CancellationToken.None));
    }

    [Fact]
    public async Task IdentificadorVacio_LanzaDomainValidationException()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(Guid.Empty, Codigos(1)), CancellationToken.None));
    }

    [Fact]
    public async Task ComandoNulo_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task EnvioDeOtroOperador_LanzaUnauthorizedAccessSinModificarNada()
    {
        var envio = CrearEnvio(operadorId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None));
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task UsuarioNoIdentificado_LanzaUnauthorizedAccess()
    {
        var envio = CrearEnvio();
        _tenant.UsuarioId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, Codigos(2)), CancellationToken.None));
    }

    [Fact]
    public async Task EnvioSinBultosDeclarados_LanzaDomainValidationException()
    {
        var envio = CrearEnvio(cantidadBultos: 0);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(new RecepcionarBultosCommand(envio.Id, new[] { "X" }), CancellationToken.None));
    }
}
