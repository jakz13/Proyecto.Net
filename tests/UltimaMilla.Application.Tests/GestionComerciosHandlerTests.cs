using UltimaMilla.Application.Commands.Operador.GestionComercios;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador.GestionComercios;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Exceptions;
using Xunit;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Tests;

public class GestionComerciosHandlerTests
{
    private readonly FakeComercioRepository _comercioRepositorio = new();
    private readonly FakeComercioOperadorRepository _comercioOperadorRepositorio = new();
    private readonly FakeCurrentTenant _inquilinoActual = new();
    private readonly FakeUnitOfWork _unidadDeTrabajo = new();

    public GestionComerciosHandlerTests()
    {
        _inquilinoActual.OperadorId = Guid.NewGuid();
    }

    // ==========================================
    // 1. Pruebas de CrearComercioCommandHandler
    // ==========================================

    [Fact]
    public async Task CrearComercio_DatosValidos_CreaComercioYVinculoActivo()
    {
        // Arrange
        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearComercioCommand(
            "Distribuidora Sol S.A.",
            "Sol Express",
            "219876540019",
            "contacto@solexpress.com",
            "099123456",
            "Av. 18 de Julio 1234");

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("Distribuidora Sol S.A.", resultado.RazonSocial);
        Assert.Equal("Sol Express", resultado.NombreFantasia);
        Assert.Equal("219876540019", resultado.Rut);
        Assert.Equal("contacto@solexpress.com", resultado.Email);
        Assert.True(resultado.Activo);
        Assert.NotEmpty(resultado.SelloModificacion);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
        Assert.Single(_comercioRepositorio.Comercios);
        Assert.Single(_comercioOperadorRepositorio.Relaciones);
    }

    [Fact]
    public async Task CrearComercio_ComercioExistenteNoVinculado_VinculaAlOperador()
    {
        // Arrange
        var comercioExistente = new ComercioEntidad(
            Guid.NewGuid(),
            "Empresa Global SRL",
            "123456789012",
            "info@global.com",
            "29001122",
            "Global Store");
        _comercioRepositorio.Comercios.Add(comercioExistente);

        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearComercioCommand(
            "Empresa Global SRL",
            "Global Store",
            "123456789012",
            "info@global.com",
            "29001122",
            "Calle Central 456");

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal(comercioExistente.Id, resultado.Id);
        Assert.Single(_comercioRepositorio.Comercios); // No duplica la entidad Comercio
        Assert.Single(_comercioOperadorRepositorio.Relaciones); // Crea el vínculo
        Assert.Equal(_inquilinoActual.OperadorId, _comercioOperadorRepositorio.Relaciones[0].OperadorId);
    }

    [Fact]
    public async Task CrearComercio_ComercioYaVinculadoAlOperador_LanzaDomainValidationException()
    {
        // Arrange
        var comercioExistente = new ComercioEntidad(
            Guid.NewGuid(),
            "Empresa Existente SRL",
            "123456789012",
            "info@existente.com",
            "29001122");
        _comercioRepositorio.Comercios.Add(comercioExistente);

        var vinculo = new ComercioOperador(
            Guid.NewGuid(),
            comercioExistente.Id,
            _inquilinoActual.OperadorId,
            "claveApi123");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearComercioCommand(
            "Empresa Existente SRL",
            "Existente",
            "123456789012",
            "info@existente.com",
            "29001122",
            "Calle 1");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("Ya existe un comercio registrado con ese RUT", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CrearComercio_RutInvalido_LanzaDomainValidationException(string? rutInvalido)
    {
        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearComercioCommand(
            "Comercio Test",
            "Fantasia",
            rutInvalido!,
            "test@correo.com",
            "099111222",
            "Direccion");

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task CrearComercio_OperadorNoAutenticado_LanzaUnauthorizedAccessException()
    {
        _inquilinoActual.OperadorId = Guid.Empty;

        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearComercioCommand("Razon", "Fantasia", "123456789012", "correo@test.com", "099", "Dir");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task CrearComercio_ComandoNulo_LanzaArgumentNullException()
    {
        var handler = new CrearComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
    }

    // ===============================================
    // 2. Pruebas de ActualizarComercioCommandHandler
    // ===============================================

    [Fact]
    public async Task ActualizarComercio_DatosValidosYSelloCorrecto_ActualizaComercioYRenuevaSello()
    {
        // Arrange
        var comercio = new ComercioEntidad(
            Guid.NewGuid(),
            "Antigua Razon Social",
            "123456789012",
            "antiguo@correo.com",
            "099111111",
            "Antiguo Nombre",
            "Antigua Direccion");
        _comercioRepositorio.Comercios.Add(comercio);

        var selloOriginal = comercio.SelloModificacion.ToString();

        var vinculo = new ComercioOperador(
            Guid.NewGuid(),
            comercio.Id,
            _inquilinoActual.OperadorId,
            "clave123",
            "Activo");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new ActualizarComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarComercioCommand(
            comercio.Id,
            "Nueva Razon Social",
            "Nuevo Nombre Fantasia",
            "nuevo@correo.com",
            "099222222",
            "Nueva Direccion",
            false, // Suspender
            selloOriginal);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal("Nueva Razon Social", resultado.RazonSocial);
        Assert.Equal("Nuevo Nombre Fantasia", resultado.NombreFantasia);
        Assert.Equal("nuevo@correo.com", resultado.Email);
        Assert.False(resultado.Activo);
        Assert.NotEqual(selloOriginal, resultado.SelloModificacion); // Se renovó el sello
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task ActualizarComercio_SelloConcurrenciaDesfasado_LanzaConflictoDeConcurrenciaException()
    {
        // Arrange
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Razon", "123456789012", "mail@test.com", "099");
        _comercioRepositorio.Comercios.Add(comercio);

        var vinculo = new ComercioOperador(Guid.NewGuid(), comercio.Id, _inquilinoActual.OperadorId, "clave123");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new ActualizarComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarComercioCommand(
            comercio.Id,
            "Razon",
            "Fantasia",
            "mail@test.com",
            "099",
            "Dir",
            true,
            Guid.NewGuid().ToString()); // Sello diferente

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictoDeConcurrenciaException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("Otra persona modificó este registro", ex.Message);
    }

    [Fact]
    public async Task ActualizarComercio_ComercioDeOtroOperador_LanzaNotFoundException_AntiIdor()
    {
        // Arrange
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Razon", "123456789012", "mail@test.com", "099");
        _comercioRepositorio.Comercios.Add(comercio);

        var otroOperadorId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercio.Id, otroOperadorId, "clave123");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new ActualizarComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarComercioCommand(
            comercio.Id,
            "Razon",
            "Fantasia",
            "mail@test.com",
            "099",
            "Dir",
            true,
            comercio.SelloModificacion.ToString());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarComercio_ComercioInexistente_LanzaNotFoundException()
    {
        var handler = new ActualizarComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarComercioCommand(
            Guid.NewGuid(),
            "Razon",
            "Fantasia",
            "mail@test.com",
            "099",
            "Dir",
            true,
            Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarComercio_IdVacio_LanzaDomainValidationException()
    {
        var handler = new ActualizarComercioCommandHandler(
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarComercioCommand(
            Guid.Empty,
            "Razon",
            "Fantasia",
            "mail@test.com",
            "099",
            "Dir",
            true,
            Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
    }

    // ==================================================
    // 3. Pruebas de CambiarEstadoComercioCommandHandler
    // ==================================================

    [Fact]
    public async Task CambiarEstadoComercio_ActivarYSuspender_ModificaEstadoExitosamente()
    {
        // Arrange
        var comercioId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercioId, _inquilinoActual.OperadorId, "clave", "Activo");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new CambiarEstadoComercioCommandHandler(
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act 1: Suspender
        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, false), CancellationToken.None);

        // Assert 1
        Assert.False(vinculo.EstaActivo);
        Assert.Equal("Suspendido", vinculo.Estado);

        // Act 2: Reactivar
        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, true), CancellationToken.None);

        // Assert 2
        Assert.True(vinculo.EstaActivo);
        Assert.Equal("Activo", vinculo.Estado);
    }

    [Fact]
    public async Task CambiarEstadoComercio_ComercioInexistente_LanzaNotFoundException()
    {
        var handler = new CambiarEstadoComercioCommandHandler(
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CambiarEstadoComercioCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    [Fact]
    public async Task CambiarEstadoComercio_IdVacio_LanzaDomainValidationException()
    {
        var handler = new CambiarEstadoComercioCommandHandler(
            _comercioOperadorRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new CambiarEstadoComercioCommand(Guid.Empty, true), CancellationToken.None));
    }

    // ===========================================
    // 4. Pruebas de ListarComerciosQueryHandler
    // ===========================================

    [Fact]
    public async Task ListarComercios_ConFiltrosYPaginacion_RetornaElementosEsperados()
    {
        // Arrange
        var c1 = new ComercioEntidad(Guid.NewGuid(), "Alfa Distribuidora", "111111111111", "alfa@correo.com", "099");
        var c2 = new ComercioEntidad(Guid.NewGuid(), "Beta Logistica", "222222222222", "beta@correo.com", "098");
        _comercioOperadorRepositorio.Comercios.AddRange(new[] { c1, c2 });

        var rel1 = new ComercioOperador(Guid.NewGuid(), c1.Id, _inquilinoActual.OperadorId, "clave1", "Activo");
        var rel2 = new ComercioOperador(Guid.NewGuid(), c2.Id, _inquilinoActual.OperadorId, "clave2", "Suspendido");
        _comercioOperadorRepositorio.Relaciones.AddRange(new[] { rel1, rel2 });

        var handler = new ListarComerciosQueryHandler(_comercioOperadorRepositorio, _inquilinoActual);

        // Act 1: Listar todos
        var resultadoTodos = await handler.Handle(new ListarComerciosQuery(), CancellationToken.None);

        // Act 2: Filtrar por texto "Alfa"
        var resultadoAlfa = await handler.Handle(new ListarComerciosQuery("Alfa"), CancellationToken.None);

        // Act 3: Filtrar solo activos
        var resultadoActivos = await handler.Handle(new ListarComerciosQuery(null, true), CancellationToken.None);

        // Assert
        Assert.Equal(2, resultadoTodos.TotalRegistros);
        Assert.Single(resultadoAlfa.Elementos);
        Assert.Equal("Alfa Distribuidora", resultadoAlfa.Elementos[0].RazonSocial);
        Assert.Single(resultadoActivos.Elementos);
        Assert.True(resultadoActivos.Elementos[0].Activo);
    }

    [Fact]
    public async Task ListarComercios_OperadorSinComercios_RetornaListaVacia()
    {
        var handler = new ListarComerciosQueryHandler(_comercioOperadorRepositorio, _inquilinoActual);
        var resultado = await handler.Handle(new ListarComerciosQuery(), CancellationToken.None);

        Assert.Empty(resultado.Elementos);
        Assert.Equal(0, resultado.TotalRegistros);
    }

    // ===============================================
    // 5. Pruebas de ObtenerComercioPorIdQueryHandler
    // ===============================================

    [Fact]
    public async Task ObtenerComercioPorId_ComercioValido_RetornaDetalle()
    {
        // Arrange
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Super Tienda", "333333333333", "super@tienda.com", "24001122");
        _comercioRepositorio.Comercios.Add(comercio);

        var rel = new ComercioOperador(Guid.NewGuid(), comercio.Id, _inquilinoActual.OperadorId, "clave", "Activo");
        _comercioOperadorRepositorio.Relaciones.Add(rel);

        var handler = new ObtenerComercioPorIdQueryHandler(_comercioRepositorio, _comercioOperadorRepositorio, _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ObtenerComercioPorIdQuery(comercio.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(comercio.Id, resultado.Id);
        Assert.Equal("Super Tienda", resultado.RazonSocial);
    }

    [Fact]
    public async Task ObtenerComercioPorId_ComercioDeOtroOperador_LanzaNotFoundException_AntiIdor()
    {
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Super Tienda", "333333333333", "super@tienda.com", "24001122");
        _comercioRepositorio.Comercios.Add(comercio);

        var rel = new ComercioOperador(Guid.NewGuid(), comercio.Id, Guid.NewGuid(), "clave", "Activo");
        _comercioOperadorRepositorio.Relaciones.Add(rel);

        var handler = new ObtenerComercioPorIdQueryHandler(_comercioRepositorio, _comercioOperadorRepositorio, _inquilinoActual);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerComercioPorIdQuery(comercio.Id), CancellationToken.None));
    }
}

