using UltimaMilla.Application.Commands.Operador.GestionZonas;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador.GestionZonas;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Suite de pruebas unitarias exhaustivas para el caso de uso /// Configuración de zonas de cobertura y franjas horarias con validación de superposiciones y Anti-IDOR.
/// </summary>
public class GestionZonasHandlerTests
{
    private readonly FakeZonaCoberturaRepository _repositorioZonas = new();
    private readonly FakeCurrentTenant _inquilinoActual = new();
    private readonly FakeUnitOfWork _unidadDeTrabajo = new();
    private readonly Guid _operadorIdPrincipal = Guid.NewGuid();
    private readonly Guid _operadorIdAjeno = Guid.NewGuid();

    public GestionZonasHandlerTests()
    {
        _inquilinoActual.OperadorId = _operadorIdPrincipal;
    }

    // =========================================================================
    // 1. Pruebas de CrearZonaCoberturaCommandHandler (Camino feliz y validaciones)
    // =========================================================================

    [Fact]
    public async Task CrearZonaCobertura_DatosValidosConFranjas_CreaZonaYPersisteFranjas()
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var franjas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), 25),
            new(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(18, 0), 30),
            new(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(14, 0), 20)
        };

        var comando = new CrearZonaCoberturaCommand(
            "Montevideo Centro y Sur",
            "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9080, -56.2120]]",
            "#2563eb",
            franjas);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("Montevideo Centro y Sur", resultado.Nombre);
        Assert.Equal("#2563eb", resultado.ColorHex);
        Assert.True(resultado.Activa);
        Assert.Equal(3, resultado.FranjasHorarias.Count);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
        Assert.Single(_repositorioZonas.Zonas);
    }

    [Fact]
    public async Task CrearZonaCobertura_ComandoNulo_LanzaArgumentNullException()
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearZonaCobertura_NombreVacioONulo_LanzaDomainValidationException(string? nombreInvalido)
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearZonaCoberturaCommand(
            nombreInvalido!,
            "[[-34.9080, -56.2120], [-34.9030, -56.1780]]",
            "#2563eb",
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("nombre", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearZonaCobertura_PoligonoVacioONulo_LanzaDomainValidationException(string? poligonoInvalido)
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearZonaCoberturaCommand(
            "Zona Prado",
            poligonoInvalido!,
            "#10b981",
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("polígono", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearZonaCobertura_NombreDuplicadoEnMismoOperador_LanzaDomainValidationException()
    {
        // Arrange
        var zonaExistente = new ZonaCobertura(
            Guid.NewGuid(),
            _operadorIdPrincipal,
            "Zona Pocitos",
            "[[-34.91, -56.15], [-34.92, -56.16]]");
        zonaExistente.ActualizarDatos("Zona Pocitos", "#2563eb");
        await _repositorioZonas.AgregarAsync(zonaExistente);

        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearZonaCoberturaCommand(
            "zona pocitos",
            "[[-34.90, -56.14], [-34.93, -56.17]]",
            "#ef4444",
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("Ya existe una zona de cobertura", ex.Message);
    }

    [Fact]
    public async Task CrearZonaCobertura_SinOperadorAutenticado_LanzaUnauthorizedAccessException()
    {
        // Arrange
        _inquilinoActual.OperadorId = Guid.Empty;

        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearZonaCoberturaCommand(
            "Zona Cordón",
            "[[-34.90, -56.17], [-34.91, -56.18]]",
            "#2563eb",
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(comando, CancellationToken.None));
    }

    // =========================================================================
    // 2. Pruebas de Franjas Horarias: Superposiciones, Inconsistencias y Límites
    // =========================================================================

    [Fact]
    public async Task CrearZonaCobertura_FranjasConSuperposicionMismoDia_LanzaDomainValidationException()
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var franjasSolapadas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0), 20),
            new(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(18, 0), 25)
        };

        var comando = new CrearZonaCoberturaCommand(
            "Zona Ciudad Vieja",
            "[[-34.90, -56.20], [-34.91, -56.21]]",
            "#8b5cf6",
            franjasSolapadas);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("superpon", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearZonaCobertura_FranjaConHoraInicioMayorQueHoraFin_LanzaDomainValidationException()
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var franjasInvertidas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Tuesday, new TimeOnly(18, 0), new TimeOnly(10, 0), 15)
        };

        var comando = new CrearZonaCoberturaCommand(
            "Zona Carrasco",
            "[[-34.88, -56.05], [-34.89, -56.06]]",
            "#f59e0b",
            franjasInvertidas);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("anterior", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task CrearZonaCobertura_FranjaConCapacidadInvalida_LanzaDomainValidationException(int capacidadInvalida)
    {
        // Arrange
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var franjas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(14, 0), capacidadInvalida)
        };

        var comando = new CrearZonaCoberturaCommand(
            "Zona Malvín",
            "[[-34.89, -56.10], [-34.90, -56.11]]",
            "#10b981",
            franjas);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(comando, CancellationToken.None));

        Assert.Contains("capacidad", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrearZonaCobertura_FranjasContiguasSinSuperposicion_SeCreaExitosamente()
    {
        // Arrange: Franja 1 termina a las 12:00 y Franja 2 arranca exactamente a las 12:00 (frontera permitida)
        var handler = new CrearZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var franjasContiguas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), 20),
            new(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(16, 0), 25)
        };

        var comando = new CrearZonaCoberturaCommand(
            "Zona Frontera",
            "[[-34.90, -56.16], [-34.91, -56.17]]",
            "#2563eb",
            franjasContiguas);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.FranjasHorarias.Count);
    }

    // =========================================================================
    // 3. Pruebas de ActualizarZonaCoberturaCommandHandler y Anti-IDOR
    // =========================================================================

    [Fact]
    public async Task ActualizarZonaCobertura_DatosValidos_ActualizaZonaYReemplazaFranjas()
    {
        // Arrange
        var zona = new ZonaCobertura(
            Guid.NewGuid(),
            _operadorIdPrincipal,
            "Zona Original",
            "[[-34.90, -56.16]]");
        zona.AgregarFranjaHoraria(new FranjaHoraria(Guid.NewGuid(), zona.Id, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0), 10));
        await _repositorioZonas.AgregarAsync(zona);

        var handler = new ActualizarZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var nuevasFranjas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Tuesday, new TimeOnly(14, 0), new TimeOnly(18, 0), 45)
        };

        var comando = new ActualizarZonaCoberturaCommand(
            zona.Id,
            "Zona Actualizada",
            "[[-34.90, -56.16], [-34.92, -56.18]]",
            "#10b981",
            true,
            nuevasFranjas);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal("Zona Actualizada", resultado.Nombre);
        Assert.Equal("#10b981", resultado.ColorHex);
        Assert.Single(resultado.FranjasHorarias);
        Assert.Equal(DayOfWeek.Tuesday, resultado.FranjasHorarias[0].DiaSemana);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task ActualizarZonaCobertura_ZonaDeOtroOperador_LanzaNotFoundException_AntiIDOR()
    {
        // Arrange
        var zonaAjena = new ZonaCobertura(
            Guid.NewGuid(),
            _operadorIdAjeno,
            "Zona Secreta de Competidor",
            "[[-34.90, -56.16]]");
        await _repositorioZonas.AgregarAsync(zonaAjena);

        var handler = new ActualizarZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarZonaCoberturaCommand(
            zonaAjena.Id,
            "Intento de Modificacion Ilegal",
            "[[-34.90, -56.16]]",
            "#2563eb",
            true,
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarZonaCobertura_IdInexistente_LanzaNotFoundException()
    {
        // Arrange
        var handler = new ActualizarZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarZonaCoberturaCommand(
            Guid.NewGuid(),
            "Zona Fantasma",
            "[[-34.90, -56.16]]",
            "#2563eb",
            true,
            new List<CrearFranjaHorariaDto>());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(comando, CancellationToken.None));
    }

    // =========================================================================
    // 4. Pruebas de CambiarEstadoZonaCoberturaCommandHandler
    // =========================================================================

    [Fact]
    public async Task CambiarEstadoZonaCobertura_InactivarYReactivar_ModificaEstadoCorrectamente()
    {
        // Arrange
        var zona = new ZonaCobertura(
            Guid.NewGuid(),
            _operadorIdPrincipal,
            "Zona Cordón Sur",
            "[[-34.90, -56.17]]");
        await _repositorioZonas.AgregarAsync(zona);

        var handler = new CambiarEstadoZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act 1: Inactivar
        await handler.Handle(
            new CambiarEstadoZonaCoberturaCommand(zona.Id, false),
            CancellationToken.None);

        // Assert 1
        var zonaActualizada = await _repositorioZonas.ObtenerPorIdAsync(zona.Id, _operadorIdPrincipal);
        Assert.NotNull(zonaActualizada);
        Assert.False(zonaActualizada.Activa);

        // Act 2: Reactivar
        await handler.Handle(
            new CambiarEstadoZonaCoberturaCommand(zona.Id, true),
            CancellationToken.None);

        // Assert 2
        zonaActualizada = await _repositorioZonas.ObtenerPorIdAsync(zona.Id, _operadorIdPrincipal);
        Assert.NotNull(zonaActualizada);
        Assert.True(zonaActualizada.Activa);
        Assert.Equal(2, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task CambiarEstadoZonaCobertura_ZonaAjena_LanzaNotFoundException_AntiIDOR()
    {
        // Arrange
        var zonaAjena = new ZonaCobertura(
            Guid.NewGuid(),
            _operadorIdAjeno,
            "Zona Ajena",
            "[[-34.90, -56.17]]");
        await _repositorioZonas.AgregarAsync(zonaAjena);

        var handler = new CambiarEstadoZonaCoberturaCommandHandler(
            _repositorioZonas,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new CambiarEstadoZonaCoberturaCommand(zonaAjena.Id, false),
                CancellationToken.None));
    }

    // =========================================================================
    // 5. Pruebas de ValidarFranjasHorariasCommandHandler
    // =========================================================================

    [Fact]
    public async Task ValidarFranjasHorarias_FranjasSinConflicto_EjecutaSinExcepcion()
    {
        // Arrange
        var handler = new ValidarFranjasHorariasCommandHandler();
        var franjas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Thursday, new TimeOnly(8, 30), new TimeOnly(12, 30), 20),
            new(DayOfWeek.Thursday, new TimeOnly(13, 0), new TimeOnly(17, 0), 25)
        };

        // Act & Assert: no debe disparar excepción
        var excepcion = await Record.ExceptionAsync(() =>
            handler.Handle(new ValidarFranjasHorariasCommand(franjas), CancellationToken.None));

        Assert.Null(excepcion);
    }

    [Fact]
    public async Task ValidarFranjasHorarias_FranjasConConflicto_LanzaDomainValidationException()
    {
        // Arrange
        var handler = new ValidarFranjasHorariasCommandHandler();
        var franjasSolapadas = new List<CrearFranjaHorariaDto>
        {
            new(DayOfWeek.Friday, new TimeOnly(10, 0), new TimeOnly(16, 0), 30),
            new(DayOfWeek.Friday, new TimeOnly(12, 0), new TimeOnly(18, 0), 30)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new ValidarFranjasHorariasCommand(franjasSolapadas), CancellationToken.None));

        Assert.Contains("superpon", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 6. Pruebas de Listar y Obtener Zonas (Queries y Aislamiento Multi-operador)
    // =========================================================================

    [Fact]
    public async Task ListarZonasCobertura_FiltraPorOperadorYEstado()
    {
        // Arrange
        var zonaPropiaActiva = new ZonaCobertura(Guid.NewGuid(), _operadorIdPrincipal, "Centro Activa", "[]");
        var zonaPropiaInactiva = new ZonaCobertura(Guid.NewGuid(), _operadorIdPrincipal, "Centro Inactiva", "[]");
        zonaPropiaInactiva.Desactivar();
        var zonaAjena = new ZonaCobertura(Guid.NewGuid(), _operadorIdAjeno, "Zona Ajena Activa", "[]");

        await _repositorioZonas.AgregarAsync(zonaPropiaActiva);
        await _repositorioZonas.AgregarAsync(zonaPropiaInactiva);
        await _repositorioZonas.AgregarAsync(zonaAjena);

        var handler = new ListarZonasCoberturaQueryHandler(_repositorioZonas, _inquilinoActual);

        // Act 1: Todas las propias
        var todasLasPropias = await handler.Handle(
            new ListarZonasCoberturaQuery(),
            CancellationToken.None);

        // Assert 1: Solo devuelve las 2 zonas del operador actual, ignorando la del operador ajeno
        Assert.Equal(2, todasLasPropias.Count);
        Assert.DoesNotContain(todasLasPropias, z => z.Id == zonaAjena.Id);

        // Act 2: Solo activas
        var soloActivas = await handler.Handle(
            new ListarZonasCoberturaQuery(SoloActivas: true),
            CancellationToken.None);

        // Assert 2
        Assert.Single(soloActivas);
        Assert.Equal("Centro Activa", soloActivas[0].Nombre);

        // Act 3: Búsqueda por texto
        var busqueda = await handler.Handle(
            new ListarZonasCoberturaQuery(Busqueda: "Inactiva"),
            CancellationToken.None);

        // Assert 3
        Assert.Single(busqueda);
        Assert.Equal("Centro Inactiva", busqueda[0].Nombre);
    }

    [Fact]
    public async Task ObtenerZonaPorId_ZonaExistentePropia_DevuelveDetalle()
    {
        // Arrange
        var zona = new ZonaCobertura(Guid.NewGuid(), _operadorIdPrincipal, "Zona Detalle", "[[-34.90, -56.16]]");
        zona.AgregarFranjaHoraria(new FranjaHoraria(Guid.NewGuid(), zona.Id, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0), 15));
        await _repositorioZonas.AgregarAsync(zona);

        var handler = new ObtenerZonaCoberturaPorIdQueryHandler(_repositorioZonas, _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ObtenerZonaCoberturaPorIdQuery(zona.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(zona.Id, resultado.Id);
        Assert.Equal("Zona Detalle", resultado.Nombre);
        Assert.Single(resultado.FranjasHorarias);
    }

    [Fact]
    public async Task ObtenerZonaPorId_ZonaAjena_LanzaNotFoundException_AntiIDOR()
    {
        // Arrange
        var zonaAjena = new ZonaCobertura(Guid.NewGuid(), _operadorIdAjeno, "Zona Ajena", "[]");
        await _repositorioZonas.AgregarAsync(zonaAjena);

        var handler = new ObtenerZonaCoberturaPorIdQueryHandler(_repositorioZonas, _inquilinoActual);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerZonaCoberturaPorIdQuery(zonaAjena.Id), CancellationToken.None));
    }
}
