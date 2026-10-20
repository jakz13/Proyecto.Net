using UltimaMilla.Application.Queries.Comercio.ListarEnviosDelComercio;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ListarEnviosDelComercioQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();
    private int _secuencia;

    private ListarEnviosDelComercioQueryHandler CrearHandler() => new(_envios, _tenant);

    private Envio CrearEnvio(string destinatario = "Ana Gomez", Guid? comercio = null, Guid? operador = null, bool enDeposito = false)
    {
        _secuencia++;
        var envio = new Envio(Guid.NewGuid(), comercio ?? _tenant.ComercioOperadorId!.Value, Guid.NewGuid(), "Estandar",
            $"TRK-COM{_secuencia:0000}", Guid.NewGuid(), null, null, $"Calle {_secuencia}", destinatario, operador ?? _tenant.OperadorId);
        envio.AgregarBulto($"TRK-COM{_secuencia:0000}-B01", 1m, 10m, 10m, 10m);
        if (enDeposito)
            envio.RecepcionarEnDeposito(new[] { $"TRK-COM{_secuencia:0000}-B01" }, null, null, Guid.NewGuid());
        _envios.Envios.Add(envio);
        return envio;
    }

    private Task<PaginaEnviosComercioDto> Listar(ListarEnviosDelComercioQuery? query = null) =>
        CrearHandler().Handle(query ?? new ListarEnviosDelComercioQuery(), default);

    [Fact]
    public async Task Listar_DevuelveSoloLosEnviosDelComercioDelUsuario()
    {
        CrearEnvio();
        CrearEnvio();
        CrearEnvio(comercio: Guid.NewGuid());
        CrearEnvio(operador: Guid.NewGuid());

        var pagina = await Listar();

        Assert.Equal(2, pagina.Total);
        Assert.Equal(2, pagina.Items.Count);
        Assert.Equal(1, pagina.TotalPaginas);
    }

    [Fact]
    public async Task Listar_UsuarioQueNoEsDeUnComercio_LanzaUnauthorizedAccess()
    {
        _tenant.ComercioOperadorId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Listar());
    }

    [Fact]
    public async Task Listar_FiltraPorEstado()
    {
        CrearEnvio();
        CrearEnvio(enDeposito: true);

        var pagina = await Listar(new ListarEnviosDelComercioQuery(Estado: "enDeposito"));

        var item = Assert.Single(pagina.Items);
        Assert.Equal("EnDeposito", item.EstadoNombre);
        Assert.Equal(2, item.Estado);
    }

    [Theory]
    [InlineData("Inexistente")]
    [InlineData("4")]
    [InlineData("99")]
    public async Task Listar_EstadoInvalido_LanzaDomainValidationException(string estado)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => Listar(new ListarEnviosDelComercioQuery(Estado: estado)));
    }

    [Fact]
    public async Task Listar_FiltraPorTextoEnDestinatarioYEnCodigo()
    {
        var ana = CrearEnvio("Ana Gomez");
        CrearEnvio("Luis Perez");

        var porNombre = await Listar(new ListarEnviosDelComercioQuery(Texto: "  GOMEZ "));
        var porCodigo = await Listar(new ListarEnviosDelComercioQuery(Texto: ana.CodigoSeguimientoPublico.ToLowerInvariant()));
        var sinResultados = await Listar(new ListarEnviosDelComercioQuery(Texto: "nadie"));

        Assert.Equal(ana.Id, Assert.Single(porNombre.Items).EnvioId);
        Assert.Equal(ana.Id, Assert.Single(porCodigo.Items).EnvioId);
        Assert.Empty(sinResultados.Items);
    }

    [Fact]
    public async Task Listar_TextoConCaracteresEspecialesNoRompeLaBusqueda()
    {
        CrearEnvio();

        var pagina = await Listar(new ListarEnviosDelComercioQuery(Texto: "'; DROP TABLE Envios;--%_"));

        Assert.Empty(pagina.Items);
    }

    [Fact]
    public async Task Listar_FiltraPorRangoDeFechasIncluyendoElDiaHasta()
    {
        var envio = CrearEnvio();
        var hoy = DateOnly.FromDateTime(envio.FechaAlta);

        var incluido = await Listar(new ListarEnviosDelComercioQuery(Desde: hoy, Hasta: hoy));
        var posterior = await Listar(new ListarEnviosDelComercioQuery(Desde: hoy.AddDays(1)));
        var anterior = await Listar(new ListarEnviosDelComercioQuery(Hasta: hoy.AddDays(-1)));

        Assert.Single(incluido.Items);
        Assert.Empty(posterior.Items);
        Assert.Empty(anterior.Items);
    }

    [Fact]
    public async Task Listar_FechaDesdePosteriorAHasta_LanzaDomainValidationException()
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Listar(new ListarEnviosDelComercioQuery(Desde: hoy.AddDays(2), Hasta: hoy)));
    }

    [Fact]
    public async Task Listar_Pagina_RespetaTamanoYCalculaElTotalDePaginas()
    {
        for (var i = 0; i < 5; i++)
            CrearEnvio();

        var primera = await Listar(new ListarEnviosDelComercioQuery(Pagina: 1, TamanoPagina: 2));
        var ultima = await Listar(new ListarEnviosDelComercioQuery(Pagina: 3, TamanoPagina: 2));
        var fuera = await Listar(new ListarEnviosDelComercioQuery(Pagina: 9, TamanoPagina: 2));

        Assert.Equal(2, primera.Items.Count);
        Assert.Equal(5, primera.Total);
        Assert.Equal(3, primera.TotalPaginas);
        Assert.Single(ultima.Items);
        Assert.Empty(fuera.Items);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Listar_PaginacionFueraDeRango_LanzaDomainValidationException(int pagina, int tamano)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => Listar(new ListarEnviosDelComercioQuery(Pagina: pagina, TamanoPagina: tamano)));
    }

    [Fact]
    public async Task Listar_TextoDemasiadoLargo_LanzaDomainValidationException()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() => Listar(new ListarEnviosDelComercioQuery(Texto: new string('a', 101))));
    }

    [Fact]
    public async Task Listar_OrdenaPorFechaDeAltaDescendente()
    {
        var primero = CrearEnvio();
        await Task.Delay(5);
        var segundo = CrearEnvio();

        var pagina = await Listar();

        Assert.Equal(new[] { segundo.Id, primero.Id }, pagina.Items.Select(i => i.EnvioId));
    }

    [Fact]
    public async Task Listar_ConsultaNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CrearHandler().Handle(null!, default));
    }
}
