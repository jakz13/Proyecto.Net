using UltimaMilla.Application.Commands.Operador.GestionTarifas;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador.GestionTarifas;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Suite de pruebas unitarias exhaustivas para el caso de uso /// Configurar el cuadro tarifario, versionado, reglas de cálculo, simulación y aislamiento Anti-IDOR.
/// </summary>
public class GestionTarifasHandlerTests
{
    private readonly FakeCuadroTarifarioRepository _cuadroRepositorio = new();
    private readonly FakeZonaCoberturaRepository _zonaRepositorio = new();
    private readonly FakeCurrentTenant _inquilinoActual = new();
    private readonly FakeUnitOfWork _unidadDeTrabajo = new();
    private readonly Guid _operadorIdPrincipal = Guid.NewGuid();
    private readonly Guid _operadorIdAjeno = Guid.NewGuid();
    private readonly Guid _zonaCentroId = Guid.NewGuid();
    private readonly Guid _zonaAjenaId = Guid.NewGuid();

    public GestionTarifasHandlerTests()
    {
        _inquilinoActual.OperadorId = _operadorIdPrincipal;

        // Zona válida del operador principal
        var zonaCentro = new ZonaCobertura(_zonaCentroId, _operadorIdPrincipal, "Centro Montevideo", "[[-34.90, -56.18]]");
        _zonaRepositorio.Zonas.Add(zonaCentro);

        // Zona de otro operador para pruebas de aislamiento Anti-IDOR
        var zonaAjena = new ZonaCobertura(_zonaAjenaId, _operadorIdAjeno, "Zona Ajena Secreta", "[[-34.80, -56.10]]");
        _zonaRepositorio.Zonas.Add(zonaAjena);
    }

    // =========================================================================
    // 1. Pruebas de PublicarVersionCuadroTarifarioCommandHandler (Versionado y Vigencia)
    // =========================================================================

    [Fact]
    public async Task PublicarPrimeraVersion_DatosValidos_GeneraVersionUnoYLaDejaVigente()
    {
        // Arrange
        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var reglas = new List<CrearReglaTarifaDto>
        {
            new(null, "Estándar 24-48h", 0, 5, 0, 0.05m, 180, 0, 0),
            new(null, "Express en el Día", 0, 5, 0, 0.05m, 250, 50, 10),
            new(_zonaCentroId, "Estándar 24-48h", 0, 5, 0, 0.05m, 150, 0, 0)
        };

        var comando = new PublicarVersionCuadroTarifarioCommand(
            DateTime.UtcNow,
            "Primer cuadro tarifario oficial",
            reglas);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal(1, resultado.Version);
        Assert.True(resultado.EsVigente);
        Assert.Null(resultado.VigenteHasta);
        Assert.Equal(3, resultado.TotalReglas);
        Assert.Equal("Primer cuadro tarifario oficial", resultado.Comentario);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
        Assert.Single(_cuadroRepositorio.Cuadros);
    }

    [Fact]
    public async Task PublicarSegundaVersion_CierraVigenciaDeLaVersionAnteriorYPublicaConsecutiva()
    {
        // Arrange: Creamos la versión 1 actualmente abierta
        var fechaInicioV1 = DateTime.UtcNow.AddDays(-10);
        var cuadroV1 = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, fechaInicioV1);
        cuadroV1.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadroV1.Id, "Estándar", 0, 5, 0, 0.05m, 100));
        await _cuadroRepositorio.AgregarAsync(cuadroV1);

        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var fechaInicioV2 = DateTime.UtcNow;
        var reglasV2 = new List<CrearReglaTarifaDto>
        {
            new(null, "Estándar", 0, 5, 0, 0.05m, 120, 10, 0)
        };

        var comando = new PublicarVersionCuadroTarifarioCommand(
            fechaInicioV2,
            "Ajuste por inflación versión 2",
            reglasV2);

        // Act
        var resultadoV2 = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal(2, resultadoV2.Version);
        Assert.True(resultadoV2.EsVigente);
        Assert.Null(resultadoV2.VigenteHasta);

        // La versión 1 debe quedar con fecha de vigencia cerrada igual a la fecha de inicio de la v2
        var cuadroV1EnRepositorio = await _cuadroRepositorio.ObtenerPorIdAsync(cuadroV1.Id, _operadorIdPrincipal);
        Assert.NotNull(cuadroV1EnRepositorio);
        Assert.NotNull(cuadroV1EnRepositorio.VigenteHasta);
        Assert.Equal(fechaInicioV2, cuadroV1EnRepositorio.VigenteHasta.Value);
        Assert.False(cuadroV1EnRepositorio.EstaVigenteEn(DateTime.UtcNow.AddSeconds(1)));
        Assert.Equal(2, _cuadroRepositorio.Cuadros.Count);
    }

    [Fact]
    public async Task PublicarVersion_ComandoNulo_LanzaArgumentNullException()
    {
        // Arrange
        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public async Task PublicarVersion_ListaDeReglasVaciaONula_LanzaDomainValidationException()
    {
        // Arrange
        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comandoSinReglas = new PublicarVersionCuadroTarifarioCommand(
            DateTime.UtcNow,
            "Sin reglas",
            new List<CrearReglaTarifaDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comandoSinReglas, CancellationToken.None));

        Assert.Contains("al menos una regla", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicarVersion_ZonaInexistenteOAjena_LanzaDomainValidationException_AntiIDOR()
    {
        // Arrange
        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Se intenta asociar la zona de otro operador (_zonaAjenaId)
        var reglasConZonaAjena = new List<CrearReglaTarifaDto>
        {
            new(_zonaAjenaId, "Estándar", 0, 5, 0, 0.05m, 200)
        };

        var comando = new PublicarVersionCuadroTarifarioCommand(
            DateTime.UtcNow,
            "Intento de usar zona ajena",
            reglasConZonaAjena);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("inexistente o ajena", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicarVersion_SinOperadorAutenticado_LanzaUnauthorizedAccessException()
    {
        // Arrange
        _inquilinoActual.OperadorId = Guid.Empty;

        var handler = new PublicarVersionCuadroTarifarioCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new PublicarVersionCuadroTarifarioCommand(
            DateTime.UtcNow,
            "Sin sesión",
            new List<CrearReglaTarifaDto> { new(null, "Estándar", 0, 5, 0, 0.05m, 100) });

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(comando, CancellationToken.None));
    }

    // =========================================================================
    // 2. Pruebas de Reglas de Tarifas: Precios, Recargos, Bonificaciones y Límites
    // =========================================================================

    [Fact]
    public void ReglaTarifa_CalculoPrecioFinal_AplicaRecargoYBonificacionCorrectamente()
    {
        // Arrange: Base 200, Recargo 50, Bonificación 20 -> 230
        var regla = new ReglaTarifa(Guid.NewGuid(), Guid.NewGuid(), "Express", 0, 5, 0, 0.05m, 200, 50, 20);

        // Act
        var precioFinal = regla.CalcularPrecioFinal();

        // Assert
        Assert.Equal(230, precioFinal);
    }

    [Fact]
    public void ReglaTarifa_BonificacionMayorQueBaseYRecargo_PrecioFinalNoBajaDeCero()
    {
        // Arrange: Base 100, Bonificación 150 -> no debe ser negativo
        var regla = new ReglaTarifa(Guid.NewGuid(), Guid.NewGuid(), "Promo", 0, 5, 0, 0.05m, 100, 0, 150);

        // Act
        var precioFinal = regla.CalcularPrecioFinal();

        // Assert
        Assert.Equal(0, precioFinal);
    }

    [Theory]
    [InlineData(-1, 5)]   // Peso inicio negativo
    [InlineData(10, 5)]   // Peso inicio mayor a peso fin
    public void ReglaTarifa_RangosDePesoInvalidos_LanzaDomainValidationException(decimal desde, decimal hasta)
    {
        Assert.Throws<DomainValidationException>(() =>
            new ReglaTarifa(Guid.NewGuid(), Guid.NewGuid(), "Estándar", desde, hasta, 0, 0.05m, 100));
    }

    [Theory]
    [InlineData(-0.01, 0.05)] // Volumen inicio negativo
    [InlineData(0.1, 0.05)]   // Volumen inicio mayor a fin
    public void ReglaTarifa_RangosDeVolumenInvalidos_LanzaDomainValidationException(decimal desde, decimal hasta)
    {
        Assert.Throws<DomainValidationException>(() =>
            new ReglaTarifa(Guid.NewGuid(), Guid.NewGuid(), "Estándar", 0, 5, desde, hasta, 100));
    }

    [Fact]
    public void ReglaTarifa_PrecioBaseNegativo_LanzaDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() =>
            new ReglaTarifa(Guid.NewGuid(), Guid.NewGuid(), "Estándar", 0, 5, 0, 0.05m, -50));
    }

    // =========================================================================
    // 3. Pruebas de SimularCotizacionTarifaCommandHandler (Cálculo en vivo)
    // =========================================================================

    [Fact]
    public async Task SimularCotizacion_ReglaExistente_CalculaTarifaBajoVersionVigente()
    {
        // Arrange: Cuadro vigente con regla general y regla específica de zona
        var cuadro = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-1));
        var reglaGeneral = new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estándar", 0, 10, 0, 0.1m, 200, 0, 0);
        var reglaZonaCentro = new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estándar", 0, 10, 0, 0.1m, 160, 20, 10, _zonaCentroId);
        cuadro.AgregarRegla(reglaGeneral);
        cuadro.AgregarRegla(reglaZonaCentro);
        await _cuadroRepositorio.AgregarAsync(cuadro);

        var handler = new SimularCotizacionTarifaCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        // Act: Envío con destino en Zona Centro (debe priorizar regla específica de zona: 160 + 20 - 10 = 170)
        var comando = new SimularCotizacionTarifaCommand(3.5m, 0.02m, "Estándar", _zonaCentroId);
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.Equal(170, resultado.PrecioFinal);
        Assert.Equal(160, resultado.PrecioBase);
        Assert.Equal(20, resultado.Recargo);
        Assert.Equal(10, resultado.Bonificacion);
        Assert.Equal(1, resultado.VersionCuadroAplicada);
        Assert.Contains("Centro Montevideo", resultado.ReglaAplicada);
    }

    [Fact]
    public async Task SimularCotizacion_SinZona_AplicaReglaGeneral()
    {
        // Arrange
        var cuadro = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-1));
        var reglaGeneral = new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Express", 0, 10, 0, 0.1m, 300, 40, 0);
        cuadro.AgregarRegla(reglaGeneral);
        await _cuadroRepositorio.AgregarAsync(cuadro);

        var handler = new SimularCotizacionTarifaCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        var comando = new SimularCotizacionTarifaCommand(2.0m, 0.01m, "Express", null);
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.True(resultado.Exitoso);
        Assert.Equal(340, resultado.PrecioFinal);
        Assert.Contains("Todas las zonas", resultado.ReglaAplicada);
    }

    [Fact]
    public async Task SimularCotizacion_SinCuadroVigente_RetornaExitosoFalse()
    {
        // Arrange: Repositorio sin cuadros vigentes
        var handler = new SimularCotizacionTarifaCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        var comando = new SimularCotizacionTarifaCommand(2.0m, 0.01m, "Estándar", null);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Contains("No hay ningún cuadro tarifario vigente", resultado.Mensaje);
    }

    [Fact]
    public async Task SimularCotizacion_EnvioExcedeLimitesDeRegla_RetornaExitosoFalse()
    {
        // Arrange: Regla hasta 5kg y se cotiza un envío de 25kg
        var cuadro = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-1));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estándar", 0, 5, 0, 0.05m, 150));
        await _cuadroRepositorio.AgregarAsync(cuadro);

        var handler = new SimularCotizacionTarifaCommandHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        var comando = new SimularCotizacionTarifaCommand(25.0m, 0.02m, "Estándar", null);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.False(resultado.Exitoso);
        Assert.Equal(1, resultado.VersionCuadroAplicada);
        Assert.Contains("Ninguna regla de la versión vigente cubre", resultado.Mensaje);
    }

    // =========================================================================
    // 4. Pruebas de Consultas (Listar, Obtener Por Id, Vigente y Anti-IDOR)
    // =========================================================================

    [Fact]
    public async Task ListarVersiones_FiltraPorOperadorYOrdenaDescendentePorVersion()
    {
        // Arrange
        var v1 = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-10));
        var v2 = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 2, DateTime.UtcNow.AddDays(-10));
        var cuadroAjeno = new CuadroTarifario(Guid.NewGuid(), _operadorIdAjeno, 1, DateTime.UtcNow.AddDays(-5));

        await _cuadroRepositorio.AgregarAsync(v1);
        await _cuadroRepositorio.AgregarAsync(v2);
        await _cuadroRepositorio.AgregarAsync(cuadroAjeno);

        var handler = new ListarVersionesCuadroTarifarioQueryHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ListarVersionesCuadroTarifarioQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(2, resultado.Count);
        Assert.Equal(2, resultado[0].Version); // Más reciente primero
        Assert.Equal(1, resultado[1].Version);
        Assert.DoesNotContain(resultado, c => c.OperadorId == _operadorIdAjeno);
    }

    [Fact]
    public async Task ObtenerCuadroPorId_CuadroPropio_DevuelveDetalleConReglas()
    {
        // Arrange
        var cuadro = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-2));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.NewGuid(), cuadro.Id, "Estándar", 0, 5, 0, 0.05m, 150));
        await _cuadroRepositorio.AgregarAsync(cuadro);

        var handler = new ObtenerCuadroTarifarioPorIdQueryHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ObtenerCuadroTarifarioPorIdQuery(cuadro.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(cuadro.Id, resultado.Id);
        Assert.Single(resultado.ReglasTarifa);
    }

    [Fact]
    public async Task ObtenerCuadroPorId_CuadroDeOtroOperador_LanzaNotFoundException_AntiIDOR()
    {
        // Arrange
        var cuadroAjeno = new CuadroTarifario(Guid.NewGuid(), _operadorIdAjeno, 1, DateTime.UtcNow.AddDays(-2));
        await _cuadroRepositorio.AgregarAsync(cuadroAjeno);

        var handler = new ObtenerCuadroTarifarioPorIdQueryHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerCuadroTarifarioPorIdQuery(cuadroAjeno.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ObtenerCuadroVigente_ExisteVigente_DevuelveVersionActiva()
    {
        // Arrange
        var v1 = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 1, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-10));
        var v2 = new CuadroTarifario(Guid.NewGuid(), _operadorIdPrincipal, 2, DateTime.UtcNow.AddDays(-10));
        await _cuadroRepositorio.AgregarAsync(v1);
        await _cuadroRepositorio.AgregarAsync(v2);

        var handler = new ObtenerCuadroTarifarioVigenteQueryHandler(
            _cuadroRepositorio,
            _zonaRepositorio,
            _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ObtenerCuadroTarifarioVigenteQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Version);
        Assert.True(resultado.EsVigente);
    }
}

