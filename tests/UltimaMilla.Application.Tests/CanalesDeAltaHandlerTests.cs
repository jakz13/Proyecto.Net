using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Commands.Comercio.ImportarEnvios;
using UltimaMilla.Application.Commands.Comercio.RegenerarClaveApi;
using UltimaMilla.Application.Commands.Integracion.AltaEnvioPorApi;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class CanalesDeAltaHandlerTests
{
    private const string ClaveApi = "clave-secreta-del-comercio";

    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeDestinatarioRepository _destinatarios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeCuadroTarifarioRepository _cuadros = new();
    private readonly FakeComercioOperadorRepository _relaciones = new();
    private readonly FakeLoteImportacionRepository _lotes = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly ComercioOperador _relacion;

    public CanalesDeAltaHandlerTests()
    {
        _relacion = new ComercioOperador(
            _tenant.ComercioOperadorId!.Value, Guid.NewGuid(), _tenant.OperadorId, ComercioOperador.CalcularHuellaClaveApi(ClaveApi));
        _relaciones.Relaciones.Add(_relacion);

        var cuadro = new CuadroTarifario(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-30));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estandar", 0, 10m, 0, 1000m, 100m, 0m, 0m, null));
        _cuadros.Cuadros.Add(cuadro);
    }

    private AltaEnvioProcesador Procesador() => new(_envios, _destinatarios, _direcciones, _cuadros, _clock, _outbox);

    private AltaEnvioPorApiCommand ComandoApi(string? clave = ClaveApi, string? referencia = "PED-1") => new(
        clave,
        "Estandar",
        new DestinatarioAltaDto("Ana Gomez", "1.234.567-8", "ana@correo.com", "099123456"),
        new DireccionAltaDto("18 de Julio", "1234", "Montevideo"),
        new List<BultoAltaDto> { new(2m, 10m, 10m, 10m) },
        null,
        referencia);

    private AltaEnvioPorApiCommandHandler HandlerApi() => new(_relaciones, Procesador(), _uow);

    private ImportarEnviosCommandHandler HandlerImportacion() => new(Procesador(), _lotes, _tenant, _clock, _uow);

    private static FilaImportacion Fila(int numero, string? referencia, decimal peso = 2m, string modalidad = "Estandar") =>
        new(numero, new AltaEnvioCommand(
            Guid.Empty,
            modalidad,
            new DestinatarioAltaDto("Ana Gomez"),
            new DireccionAltaDto("18 de Julio", "1234", "Montevideo"),
            new List<BultoAltaDto> { new(peso, 10m, 10m, 10m) },
            null,
            referencia));

    // UC-26

    [Fact]
    public async Task Api_ConClaveValida_AdmiteElEnvioParaElComercioDeLaClave()
    {
        var resultado = await HandlerApi().Handle(ComandoApi(), default);

        var envio = Assert.Single(_envios.Envios);
        Assert.Equal(_relacion.Id, envio.ComercioOperadorId);
        Assert.Equal(_relacion.OperadorId, envio.OperadorId);
        Assert.Equal("PED-1", envio.ReferenciaExterna);
        Assert.Equal("Admitido", resultado.Estado);
        Assert.False(resultado.Duplicado);
        Assert.Equal(100m, resultado.Tarifa);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave-equivocada")]
    public async Task Api_ConClaveInvalida_LanzaUnauthorizedYNoPersiste(string? clave)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerApi().Handle(ComandoApi(clave), default));

        Assert.Empty(_envios.Envios);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Api_ComercioSuspendido_LanzaUnauthorized()
    {
        _relacion.Suspender();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerApi().Handle(ComandoApi(), default));

        Assert.Empty(_envios.Envios);
    }

    [Fact]
    public async Task Api_ReintentoConLaMismaReferencia_DevuelveElEnvioExistenteSinDuplicar()
    {
        var primero = await HandlerApi().Handle(ComandoApi(), default);
        var segundo = await HandlerApi().Handle(ComandoApi(), default);

        Assert.Single(_envios.Envios);
        Assert.True(segundo.Duplicado);
        Assert.Equal(primero.Id, segundo.Id);
        Assert.Equal(primero.CodigoSeguimiento, segundo.CodigoSeguimiento);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Api_MismaReferenciaEnOtroComercio_NoSeConsideraDuplicado()
    {
        await HandlerApi().Handle(ComandoApi(), default);

        var otroComercio = new ComercioOperador(Guid.NewGuid(), Guid.NewGuid(), _tenant.OperadorId, ComercioOperador.CalcularHuellaClaveApi("otra-clave"));
        _relaciones.Relaciones.Add(otroComercio);
        var resultado = await HandlerApi().Handle(ComandoApi("otra-clave"), default);

        Assert.False(resultado.Duplicado);
        Assert.Equal(2, _envios.Envios.Count);
    }

    [Fact]
    public async Task Api_ConDatosInvalidos_LanzaDomainValidation()
    {
        var comando = ComandoApi() with { Bultos = new List<BultoAltaDto>() };

        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerApi().Handle(comando, default));

        Assert.Empty(_envios.Envios);
    }

    // Credenciales

    [Fact]
    public async Task RegenerarClave_GuardaSoloLaHuellaYLaAnteriorDejaDeValer()
    {
        var handler = new RegenerarClaveApiCommandHandler(_relaciones, _tenant, _uow);

        var resultado = await handler.Handle(new RegenerarClaveApiCommand(), default);

        Assert.StartsWith("um_", resultado.ClaveApi);
        Assert.NotEqual(resultado.ClaveApi, _relacion.ClaveApi);
        Assert.Equal(ComercioOperador.CalcularHuellaClaveApi(resultado.ClaveApi), _relacion.ClaveApi);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerApi().Handle(ComandoApi(ClaveApi), default));
        await HandlerApi().Handle(ComandoApi(resultado.ClaveApi), default);
    }

    [Fact]
    public async Task RegenerarClave_UsuarioSinComercio_LanzaUnauthorized()
    {
        _tenant.ComercioOperadorId = null;
        var handler = new RegenerarClaveApiCommandHandler(_relaciones, _tenant, _uow);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new RegenerarClaveApiCommand(), default));
    }

    // UC-25

    [Fact]
    public async Task Importar_MezclaDeFilasValidasEInvalidas_InformaExitosYErrores()
    {
        var filas = new[]
        {
            Fila(2, "A-1"),
            Fila(3, "A-2", peso: 500m),
            new FilaImportacion(4, null, "Peso y medidas deben ser números."),
            Fila(5, "A-3")
        };

        var resultado = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", filas), default);

        Assert.Equal(4, resultado.TotalRegistros);
        Assert.Equal(2, resultado.Creados);
        Assert.Equal(2, resultado.ConError);
        Assert.Equal(2, _envios.Envios.Count);
        Assert.Contains("regla de tarifa", resultado.Filas.Single(f => f.Fila == 3).Error);
        Assert.Equal("Peso y medidas deben ser números.", resultado.Filas.Single(f => f.Fila == 4).Error);

        var lote = Assert.Single(_lotes.Lotes);
        Assert.Equal(2, lote.CantidadExitosos);
        Assert.Equal(2, lote.CantidadConError);
        Assert.All(_envios.Envios, e => Assert.Equal(lote.Id, e.LoteImportacionId));
    }

    [Fact]
    public async Task Importar_MismoLoteDosVeces_NoDuplicaEnvios()
    {
        var filas = new[] { Fila(2, "A-1"), Fila(3, "A-2") };

        var primera = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", filas), default);
        var segunda = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", filas), default);

        Assert.Equal(2, _envios.Envios.Count);
        Assert.False(primera.Reimportacion);
        Assert.True(segunda.Reimportacion);
        Assert.Equal(0, segunda.Creados);
        Assert.Equal(2, segunda.Duplicados);
        Assert.Equal(primera.LoteId, segunda.LoteId);
        Assert.Single(_lotes.Lotes);
    }

    [Fact]
    public async Task Importar_ReimportacionParcialConCorreccion_CreaSoloLosQueFaltaban()
    {
        await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { Fila(2, "A-1"), Fila(3, "A-2", peso: 500m) }), default);

        // El comercio corrige la fila con error y vuelve a subir el lote completo
        var segunda = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { Fila(2, "A-1"), Fila(3, "A-2", peso: 5m) }), default);

        Assert.Equal(1, segunda.Creados);
        Assert.Equal(1, segunda.Duplicados);
        Assert.Equal(0, segunda.ConError);
        Assert.Equal(2, _envios.Envios.Count);
        Assert.Equal(2, _lotes.Lotes.Single().CantidadExitosos);
    }

    [Fact]
    public async Task Importar_FilaSinReferencia_SeRechazaEsaFila()
    {
        var resultado = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { Fila(2, null), Fila(3, "A-2") }), default);

        Assert.Equal(1, resultado.Creados);
        Assert.Equal(1, resultado.ConError);
        Assert.Contains("referencia", resultado.Filas.Single(f => f.Fila == 2).Error);
    }

    [Fact]
    public async Task Importar_ReferenciaRepetidaEnElArchivo_SoloLaPrimeraSeCrea()
    {
        var resultado = await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { Fila(2, "A-1"), Fila(3, "A-1") }), default);

        Assert.Equal(1, resultado.Creados);
        Assert.Equal(1, resultado.ConError);
        Assert.Single(_envios.Envios);
    }

    [Fact]
    public async Task Importar_ElComercioDelArchivoSiempreEsElDeLaSesion()
    {
        var ajeno = Guid.NewGuid();
        var fila = new FilaImportacion(2, Fila(2, "A-1").Alta! with { ComercioId = ajeno });

        await HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { fila }), default);

        Assert.Equal(_tenant.ComercioOperadorId, Assert.Single(_envios.Envios).ComercioOperadorId);
    }

    [Fact]
    public async Task Importar_UsuarioSinComercio_LanzaUnauthorized()
    {
        _tenant.ComercioOperadorId = null;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", new[] { Fila(2, "A-1") }), default));
    }

    [Fact]
    public async Task Importar_ArchivoVacioOSinClave_LanzaDomainValidation()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", Array.Empty<FilaImportacion>()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerImportacion().Handle(new ImportarEnviosCommand(" ", new[] { Fila(2, "A-1") }), default));
    }

    [Fact]
    public async Task Importar_MasDelMaximoPorLote_LanzaDomainValidation()
    {
        var filas = Enumerable.Range(2, ImportarEnviosCommandHandler.MaxFilasPorLote + 1).Select(n => Fila(n, $"R-{n}")).ToList();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            HandlerImportacion().Handle(new ImportarEnviosCommand("lote-1", filas), default));
        Assert.Empty(_envios.Envios);
    }

    // Lectura del CSV

    [Fact]
    public void Csv_ConSeparadorPuntoYComaYComillas_LeeLosCampos()
    {
        const string csv = "referencia;modalidad;destinatario;calle;numero;ciudad;peso;alto;ancho;profundo;email\r\n" +
                           "P-1;Estandar;\"Gomez; Ana\";18 de Julio;1234;Montevideo;2.5;10;20;30;ana@correo.com\r\n";

        var fila = Assert.Single(CsvEnviosParser.Leer(csv));

        Assert.Equal(2, fila.Numero);
        Assert.Equal("Gomez; Ana", fila.Alta!.Destinatario.Nombre);
        Assert.Equal("ana@correo.com", fila.Alta.Destinatario.Email);
        Assert.Equal(2.5m, fila.Alta.Bultos[0].Peso);
        Assert.Equal("P-1", fila.Alta.ReferenciaExterna);
    }

    [Fact]
    public void Csv_FilaConNumeroInvalidoOColumnasDeMas_SeMarcaConErrorSinCortarElArchivo()
    {
        const string csv = "referencia,modalidad,destinatario,calle,numero,ciudad,peso,alto,ancho,profundo\n" +
                           "P-1,Estandar,Ana,Calle,1,Montevideo,dos,10,10,10\n" +
                           "P-2,Estandar,Ana,Calle,1,Montevideo,2,10,10,10,extra\n" +
                           "P-3,Estandar,Ana,Calle,1,Montevideo,2,10,10,10\n";

        var filas = CsvEnviosParser.Leer(csv);

        Assert.Equal(3, filas.Count);
        Assert.NotNull(filas[0].ErrorLectura);
        Assert.NotNull(filas[1].ErrorLectura);
        Assert.NotNull(filas[2].Alta);
    }

    [Fact]
    public void Csv_SinColumnasObligatorias_LanzaDomainValidation()
    {
        var ex = Assert.Throws<DomainValidationException>(() => CsvEnviosParser.Leer("referencia,peso\nP-1,2\n"));

        Assert.Contains("modalidad", ex.Message);
    }
}
