using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class AltaEnvioCommandHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeDestinatarioRepository _destinatarios = new();
    private readonly FakeDireccionRepository _direcciones = new();
    private readonly FakeCuadroTarifarioRepository _cuadros = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly Guid _comercioId = Guid.NewGuid();

    private AltaEnvioCommandHandler CrearHandler() =>
        new(new AltaEnvioProcesador(_envios, _destinatarios, _direcciones, _cuadros, _clock), _tenant, _uow);

    private CuadroTarifario CrearCuadro(params ReglaTarifa[] reglas)
    {
        var cuadro = new CuadroTarifario(Guid.NewGuid(), _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-30));
        foreach (var regla in reglas)
            cuadro.AgregarRegla(regla);
        _cuadros.Cuadros.Add(cuadro);
        return cuadro;
    }

    private static ReglaTarifa Regla(
        Guid cuadroId,
        string modalidad = "Estandar",
        decimal pesoHasta = 10m,
        decimal volumenHasta = 1000m,
        decimal precio = 100m,
        decimal recargo = 0m,
        decimal bonificacion = 0m,
        Guid? zona = null) =>
        new(Guid.NewGuid(), cuadroId, modalidad, 0, pesoHasta, 0, volumenHasta, precio, recargo, bonificacion, zona);

    private CuadroTarifario CrearCuadroConReglaEstandar(decimal precio = 100m)
    {
        var cuadroId = Guid.NewGuid();
        var cuadro = new CuadroTarifario(cuadroId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-30));
        cuadro.AgregarRegla(Regla(cuadroId, precio: precio));
        _cuadros.Cuadros.Add(cuadro);
        return cuadro;
    }

    private AltaEnvioCommand Comando(
        string modalidad = "Estandar",
        DestinatarioAltaDto? destinatario = null,
        DireccionAltaDto? direccion = null,
        IReadOnlyList<BultoAltaDto>? bultos = null,
        Guid? comercioId = null,
        Guid? franja = null) =>
        new(
            comercioId ?? _comercioId,
            modalidad,
            destinatario ?? new DestinatarioAltaDto("Ana Gomez", "1.234.567-8", "ana@correo.com", "099123456"),
            direccion ?? new DireccionAltaDto("18 de Julio", "1234", "Montevideo"),
            bultos ?? new List<BultoAltaDto> { new(2m, 10m, 10m, 10m) },
            franja);

    private void AssertNadaPersistido()
    {
        Assert.Empty(_envios.Envios);
        Assert.Empty(_destinatarios.Destinatarios);
        Assert.Empty(_direcciones.Direcciones);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task AltaValida_CreaEnvioAdmitidoConTarifaYCodigoDeSeguimiento()
    {
        CrearCuadroConReglaEstandar(precio: 250m);

        var resultado = await CrearHandler().Handle(Comando(), CancellationToken.None);

        var envio = Assert.Single(_envios.Envios);
        Assert.Equal(envio.Id, resultado.Id);
        Assert.Equal(EstadoEnvio.Admitido, envio.EstadoActual);
        Assert.Equal("Admitido", resultado.Estado);
        Assert.Equal(250m, envio.TarifaCalculada);
        Assert.Equal(250m, resultado.Tarifa);
        Assert.NotNull(envio.ReglaTarifaAplicadaId);
        Assert.StartsWith("TRK-", resultado.CodigoSeguimiento);
        Assert.Equal(resultado.CodigoSeguimiento, envio.CodigoSeguimientoPublico);
        Assert.Equal(1, _uow.Confirmaciones);
        Assert.Single(_destinatarios.Destinatarios);
        Assert.Single(_direcciones.Direcciones);
        Assert.Single(envio.Eventos);
        Assert.Equal("AltaEnvio", envio.Eventos.Single().TipoEvento);
    }

    [Fact]
    public async Task AltaValida_VinculaDestinatarioYDireccionYGuardaCopiaDesnormalizada()
    {
        CrearCuadroConReglaEstandar();

        await CrearHandler().Handle(Comando(), CancellationToken.None);

        var envio = _envios.Envios.Single();
        Assert.Equal(_destinatarios.Destinatarios.Single().Id, envio.DestinatarioId);
        Assert.Equal(_direcciones.Direcciones.Single().Id, envio.DireccionEntregaId);
        Assert.Equal("18 de Julio 1234, Montevideo", envio.DireccionDestino);
        Assert.Equal("Ana Gomez", envio.NombreDestinatario);
        Assert.Equal(_comercioId, envio.ComercioOperadorId);
        Assert.Equal(_tenant.OperadorId, envio.OperadorId);
    }

    [Fact]
    public async Task AltaConVariosBultos_GeneraIdentificacionesYSumaPesoParaLaTarifa()
    {
        var cuadroId = Guid.NewGuid();
        var cuadro = new CuadroTarifario(cuadroId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(Regla(cuadroId, pesoHasta: 5m, precio: 100m));
        cuadro.AgregarRegla(Regla(cuadroId, pesoHasta: 20m, precio: 300m));
        _cuadros.Cuadros.Add(cuadro);

        var bultos = new List<BultoAltaDto> { new(3m, 5m, 5m, 5m), new(4m, 5m, 5m, 5m) };

        var resultado = await CrearHandler().Handle(Comando(bultos: bultos), CancellationToken.None);

        // Peso total 7 kg: no entra en la regla de hasta 5 kg
        Assert.Equal(300m, resultado.Tarifa);
        Assert.Equal(2, resultado.IdentificacionesBultos.Count);
        Assert.Equal($"{resultado.CodigoSeguimiento}-B01", resultado.IdentificacionesBultos[0]);
        Assert.Equal($"{resultado.CodigoSeguimiento}-B02", resultado.IdentificacionesBultos[1]);
        Assert.Equal(2, _envios.Envios.Single().Bultos.Count);
    }

    [Fact]
    public async Task Tarifa_IncluyeRecargoYBonificacion()
    {
        var cuadroId = Guid.NewGuid();
        var cuadro = new CuadroTarifario(cuadroId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(Regla(cuadroId, precio: 100m, recargo: 30m, bonificacion: 10m));
        _cuadros.Cuadros.Add(cuadro);

        var resultado = await CrearHandler().Handle(Comando(), CancellationToken.None);

        Assert.Equal(120m, resultado.Tarifa);
    }

    [Fact]
    public async Task Tarifa_PrefiereLaReglaDeLaZonaSobreLaGeneral()
    {
        var zona = Guid.NewGuid();
        var cuadroId = Guid.NewGuid();
        var cuadro = new CuadroTarifario(cuadroId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(Regla(cuadroId, precio: 100m));
        cuadro.AgregarRegla(Regla(cuadroId, precio: 80m, zona: zona));
        _cuadros.Cuadros.Add(cuadro);

        var direccion = new DireccionAltaDto("Rivera", "10", "Montevideo", zona);
        var resultado = await CrearHandler().Handle(Comando(direccion: direccion), CancellationToken.None);

        Assert.Equal(80m, resultado.Tarifa);
    }

    [Fact]
    public async Task Tarifa_ReglaDeOtraZonaNoAplicaYSeUsaLaGeneral()
    {
        var cuadroId = Guid.NewGuid();
        var cuadro = new CuadroTarifario(cuadroId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(Regla(cuadroId, precio: 100m));
        cuadro.AgregarRegla(Regla(cuadroId, precio: 80m, zona: Guid.NewGuid()));
        _cuadros.Cuadros.Add(cuadro);

        // Sin zona informada solo puede aplicar la regla general
        var resultado = await CrearHandler().Handle(Comando(), CancellationToken.None);

        Assert.Equal(100m, resultado.Tarifa);
    }

    [Fact]
    public async Task Tarifa_UsaElCuadroDeMayorVersionVigente()
    {
        var viejoId = Guid.NewGuid();
        var viejo = new CuadroTarifario(viejoId, _tenant.OperadorId, 1, _clock.UtcNow.AddDays(-60));
        viejo.AgregarRegla(Regla(viejoId, precio: 100m));
        var nuevoId = Guid.NewGuid();
        var nuevo = new CuadroTarifario(nuevoId, _tenant.OperadorId, 2, _clock.UtcNow.AddDays(-1));
        nuevo.AgregarRegla(Regla(nuevoId, precio: 150m));
        _cuadros.Cuadros.AddRange(new[] { viejo, nuevo });

        var resultado = await CrearHandler().Handle(Comando(), CancellationToken.None);

        Assert.Equal(150m, resultado.Tarifa);
    }

    [Fact]
    public async Task SinCuadroTarifarioVigente_RechazaElAltaSinPersistirNada()
    {
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(), CancellationToken.None));

        Assert.Contains("cuadro tarifario vigente", ex.Message);
        AssertNadaPersistido();
    }

    [Fact]
    public async Task CuadroVencido_NoSeConsideraVigente()
    {
        var cuadro = CrearCuadro();
        cuadro.AgregarRegla(Regla(cuadro.Id));
        cuadro.CerrarVigencia(_clock.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task CuadroDeOtroOperador_NoSeUsa()
    {
        var otro = new CuadroTarifario(Guid.NewGuid(), Guid.NewGuid(), 1, _clock.UtcNow.AddDays(-1));
        otro.AgregarRegla(Regla(otro.Id));
        _cuadros.Cuadros.Add(otro);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task PesoFueraDeTodoRango_RechazaElAlta()
    {
        CrearCuadroConReglaEstandar();
        var bultos = new List<BultoAltaDto> { new(50m, 10m, 10m, 10m) };

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(bultos: bultos), CancellationToken.None));

        Assert.Contains("regla de tarifa aplicable", ex.Message);
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ModalidadSinReglaDeTarifa_RechazaElAlta()
    {
        CrearCuadroConReglaEstandar();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(modalidad: "Urgente"), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ModalidadSeNormalizaEnMayusculasYMinusculasParaTarifar()
    {
        CrearCuadroConReglaEstandar(precio: 90m);

        var resultado = await CrearHandler().Handle(Comando(modalidad: "  ESTANDAR "), CancellationToken.None);

        Assert.Equal(90m, resultado.Tarifa);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ModalidadVaciaONula_LanzaDomainValidationException(string? modalidad)
    {
        CrearCuadroConReglaEstandar();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(modalidad: modalidad!), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ComandoNulo_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CrearHandler().Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task DestinatarioNulo_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();
        var comando = Comando() with { Destinatario = null! };

        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(comando, CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task DireccionNula_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();
        var comando = Comando() with { Direccion = null! };

        await Assert.ThrowsAsync<DomainValidationException>(() => CrearHandler().Handle(comando, CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task BultosNulosOVacios_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando() with { Bultos = null! }, CancellationToken.None));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(bultos: new List<BultoAltaDto>()), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task BultoNuloDentroDeLaLista_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();
        var bultos = new List<BultoAltaDto> { new(1m, 1m, 1m, 1m), null! };

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(bultos: bultos), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Theory]
    [InlineData(0, 10, 10, 10)]
    [InlineData(-1, 10, 10, 10)]
    [InlineData(1, 0, 10, 10)]
    [InlineData(1, 10, -5, 10)]
    [InlineData(1, 10, 10, 0)]
    public async Task BultoConMedidasNoPositivas_LanzaDomainValidationException(decimal peso, decimal alto, decimal ancho, decimal profundo)
    {
        CrearCuadroConReglaEstandar();
        var bultos = new List<BultoAltaDto> { new(peso, alto, ancho, profundo) };

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(bultos: bultos), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task NombreDeDestinatarioVacio_LanzaDomainValidationException(string? nombre)
    {
        CrearCuadroConReglaEstandar();
        var destinatario = new DestinatarioAltaDto(nombre!);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(destinatario: destinatario), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Theory]
    [InlineData("", "10", "Montevideo")]
    [InlineData("Rivera", "  ", "Montevideo")]
    [InlineData("Rivera", "10", "")]
    [InlineData(null, "10", "Montevideo")]
    public async Task DireccionIncompleta_LanzaDomainValidationException(string? calle, string? numero, string? ciudad)
    {
        CrearCuadroConReglaEstandar();
        var direccion = new DireccionAltaDto(calle!, numero!, ciudad!);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(direccion: direccion), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task CoordenadasFueraDeRango_LanzaDomainValidationException(double latitud, double longitud)
    {
        CrearCuadroConReglaEstandar();
        var direccion = new DireccionAltaDto("Rivera", "10", "Montevideo", Latitud: latitud, Longitud: longitud);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(direccion: direccion), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ComercioVacio_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(comercioId: Guid.Empty), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task ZonaDeCoberturaVacia_LanzaDomainValidationException()
    {
        CrearCuadroConReglaEstandar();
        var direccion = new DireccionAltaDto("Rivera", "10", "Montevideo", Guid.Empty);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CrearHandler().Handle(Comando(direccion: direccion), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task DosAltas_GeneranCodigosDeSeguimientoDistintos()
    {
        CrearCuadroConReglaEstandar();
        var handler = CrearHandler();

        var primero = await handler.Handle(Comando(), CancellationToken.None);
        var segundo = await handler.Handle(Comando(), CancellationToken.None);

        Assert.NotEqual(primero.CodigoSeguimiento, segundo.CodigoSeguimiento);
        Assert.NotEqual(primero.Id, segundo.Id);
        Assert.Equal(2, _envios.Envios.Count);
    }

    [Fact]
    public async Task ColisionDeCodigoDeSeguimiento_ReintentaYTerminaGenerandoUnoUnico()
    {
        CrearCuadroConReglaEstandar();
        _envios.ColisionesDeCodigo = 2;

        var resultado = await CrearHandler().Handle(Comando(), CancellationToken.None);

        Assert.NotNull(resultado.CodigoSeguimiento);
        Assert.Single(_envios.Envios);
    }

    [Fact]
    public async Task ColisionesDeCodigoAgotanLosReintentos_LanzaInvalidOperationSinPersistir()
    {
        CrearCuadroConReglaEstandar();
        _envios.ColisionesDeCodigo = 100;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CrearHandler().Handle(Comando(), CancellationToken.None));
        AssertNadaPersistido();
    }

    [Fact]
    public async Task FranjaHorariaInformada_SeConservaEnElEnvio()
    {
        CrearCuadroConReglaEstandar();
        var franja = Guid.NewGuid();

        await CrearHandler().Handle(Comando(franja: franja), CancellationToken.None);

        Assert.Equal(franja, _envios.Envios.Single().FranjaHorariaComprometidaId);
    }
}
