using UltimaMilla.Application.Commands.Operador.GestionUsuarios;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador.GestionUsuarios;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;
using Xunit;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Tests;

public class GestionUsuariosHandlerTests
{
    private readonly FakeUsuarioRepository _usuarioRepositorio = new();
    private readonly FakeComercioRepository _comercioRepositorio = new();
    private readonly FakeComercioOperadorRepository _comercioOperadorRepositorio = new();
    private readonly FakePasswordHasherService _hasheadorContrasena = new();
    private readonly FakeCurrentTenant _inquilinoActual = new();
    private readonly FakeUnitOfWork _unidadDeTrabajo = new();

    private readonly Perfil _perfilAdmin;
    private readonly Perfil _perfilOperador;

    public GestionUsuariosHandlerTests()
    {
        _inquilinoActual.OperadorId = Guid.NewGuid();

        _perfilAdmin = new Perfil(Guid.NewGuid(), "Administrador", "Acceso total");
        _perfilOperador = new Perfil(Guid.NewGuid(), "Operador", "Gestión operativa");

        _usuarioRepositorio.Perfiles.AddRange(new[] { _perfilAdmin, _perfilOperador });
    }

    // ==========================================
    // 1. Pruebas de CrearUsuarioCommandHandler
    // ==========================================

    [Fact]
    public async Task CrearUsuario_DatosValidos_CreaUsuarioConContrasenaHasheadaYPerfil()
    {
        // Arrange
        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand(
            "Carlos Ramirez",
            "carlos.ramirez@operador.com",
            "ClaveSegura123",
            _perfilAdmin.Id);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("Carlos Ramirez", resultado.Nombre);
        Assert.Equal("carlos.ramirez@operador.com", resultado.Email);
        Assert.Equal(_perfilAdmin.Id, resultado.PerfilId);
        Assert.Equal("Administrador", resultado.NombrePerfil);
        Assert.True(resultado.Activo);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);

        var usuarioGuardado = _usuarioRepositorio.Usuarios.Single(u => u.Id == resultado.Id);
        Assert.Equal("hashed_ClaveSegura123", usuarioGuardado.PasswordHash);
        Assert.Equal(_inquilinoActual.OperadorId, usuarioGuardado.OperadorId);
    }

    [Fact]
    public async Task CrearUsuario_ConComercioAsociadoValido_AsociaComercioExitosamente()
    {
        // Arrange
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Comercio Uno", "123456789012", "c1@test.com", "099");
        _comercioRepositorio.Comercios.Add(comercio);

        var vinculo = new ComercioOperador(Guid.NewGuid(), comercio.Id, _inquilinoActual.OperadorId, "clave");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand(
            "Usuario Comercio",
            "comercio@test.com",
            "Clave123",
            _perfilOperador.Id,
            comercio.Id);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal(comercio.Id, resultado.ComercioId);
        Assert.Equal("Comercio Uno", resultado.NombreComercio);
    }

    [Fact]
    public async Task CrearUsuario_ConComercioDeOtroOperador_LanzaDomainValidationException()
    {
        // Arrange
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Comercio Ajeno", "123456789012", "c@test.com", "099");
        _comercioRepositorio.Comercios.Add(comercio);

        var otroOperadorId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercio.Id, otroOperadorId, "clave");
        _comercioOperadorRepositorio.Relaciones.Add(vinculo);

        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand(
            "Usuario",
            "u@test.com",
            "Clave123",
            _perfilOperador.Id,
            comercio.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("no pertenece al operador", ex.Message);
    }

    [Fact]
    public async Task CrearUsuario_EmailDuplicado_LanzaDomainValidationException()
    {
        // Arrange
        var usuarioExistente = new Usuario(
            Guid.NewGuid(),
            "Existente",
            "duplicado@correo.com",
            "hash",
            _perfilAdmin.Id,
            _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuarioExistente);

        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand("Nuevo", "duplicado@correo.com", "Clave123", _perfilAdmin.Id);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("Ya existe un usuario registrado con ese correo electrónico", ex.Message);
    }

    [Fact]
    public async Task CrearUsuario_PerfilInexistente_LanzaDomainValidationException()
    {
        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand("Nombre", "email@test.com", "Clave123", Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("El perfil seleccionado no existe", ex.Message);
    }

    [Fact]
    public async Task CrearUsuario_OperadorNoAutenticado_LanzaUnauthorizedAccessException()
    {
        _inquilinoActual.OperadorId = Guid.Empty;

        var handler = new CrearUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _comercioOperadorRepositorio,
            _hasheadorContrasena,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new CrearUsuarioCommand("Nombre", "email@test.com", "Clave123", _perfilAdmin.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(comando, CancellationToken.None));
    }

    // ===============================================
    // 2. Pruebas de ActualizarUsuarioCommandHandler
    // ===============================================

    [Fact]
    public async Task ActualizarUsuario_DatosValidos_ActualizaNombreEmailYPerfil()
    {
        // Arrange
        var usuario = new Usuario(
            Guid.NewGuid(),
            "Nombre Original",
            "original@correo.com",
            "hash",
            _perfilAdmin.Id,
            _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new ActualizarUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarUsuarioCommand(
            usuario.Id,
            "Nombre Actualizado",
            "actualizado@correo.com",
            _perfilOperador.Id,
            false); // Inactivar

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal("Nombre Actualizado", resultado.Nombre);
        Assert.Equal("actualizado@correo.com", resultado.Email);
        Assert.Equal(_perfilOperador.Id, resultado.PerfilId);
        Assert.Equal("Operador", resultado.NombrePerfil);
        Assert.False(resultado.Activo);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task ActualizarUsuario_EmailDuplicadoConOtroUsuario_LanzaDomainValidationException()
    {
        // Arrange
        var u1 = new Usuario(Guid.NewGuid(), "U1", "correo1@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        var u2 = new Usuario(Guid.NewGuid(), "U2", "correo2@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.AddRange(new[] { u1, u2 });

        var handler = new ActualizarUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarUsuarioCommand(u2.Id, "U2 Nuevo", "correo1@test.com", _perfilAdmin.Id, true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(comando, CancellationToken.None));
        Assert.Contains("Ya existe otro usuario con ese correo electrónico", ex.Message);
    }

    [Fact]
    public async Task ActualizarUsuario_MismoEmailPropio_PermiteActualizacion()
    {
        // Arrange
        var u1 = new Usuario(Guid.NewGuid(), "U1", "mismo@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(u1);

        var handler = new ActualizarUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarUsuarioCommand(u1.Id, "U1 Renombrado", "mismo@test.com", _perfilOperador.Id, true);

        // Act
        var resultado = await handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.Equal("U1 Renombrado", resultado.Nombre);
        Assert.Equal("mismo@test.com", resultado.Email);
    }

    [Fact]
    public async Task ActualizarUsuario_UsuarioDeOtroOperador_LanzaNotFoundException_AntiIdor()
    {
        var otroOperadorId = Guid.NewGuid();
        var usuario = new Usuario(Guid.NewGuid(), "U1", "otro@test.com", "hash", _perfilAdmin.Id, otroOperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new ActualizarUsuarioCommandHandler(
            _usuarioRepositorio,
            _comercioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        var comando = new ActualizarUsuarioCommand(usuario.Id, "Nombre", "email@test.com", _perfilAdmin.Id, true);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(comando, CancellationToken.None));
    }

    // ==================================================
    // 3. Pruebas de CambiarEstadoUsuarioCommandHandler
    // ==================================================

    [Fact]
    public async Task CambiarEstadoUsuario_ActivarYDesactivar_ModificaEstadoExitosamente()
    {
        // Arrange
        var usuario = new Usuario(Guid.NewGuid(), "U1", "u1@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new CambiarEstadoUsuarioCommandHandler(
            _usuarioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act 1: Desactivar
        await handler.Handle(new CambiarEstadoUsuarioCommand(usuario.Id, false), CancellationToken.None);
        Assert.False(usuario.Activo);

        // Act 2: Reactivar
        await handler.Handle(new CambiarEstadoUsuarioCommand(usuario.Id, true), CancellationToken.None);
        Assert.True(usuario.Activo);
    }

    [Fact]
    public async Task CambiarEstadoUsuario_UsuarioInexistente_LanzaNotFoundException()
    {
        var handler = new CambiarEstadoUsuarioCommandHandler(
            _usuarioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CambiarEstadoUsuarioCommand(Guid.NewGuid(), true), CancellationToken.None));
    }

    // ==================================================
    // 4. Pruebas de AsignarPerfilUsuarioCommandHandler
    // ==================================================

    [Fact]
    public async Task AsignarPerfilUsuario_PerfilValido_AsignaPerfilCorrectamente()
    {
        // Arrange
        var usuario = new Usuario(Guid.NewGuid(), "U1", "u1@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new AsignarPerfilUsuarioCommandHandler(
            _usuarioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        // Act
        await handler.Handle(new AsignarPerfilUsuarioCommand(usuario.Id, _perfilOperador.Id), CancellationToken.None);

        // Assert
        Assert.Equal(_perfilOperador.Id, usuario.PerfilId);
        Assert.Equal(1, _unidadDeTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task AsignarPerfilUsuario_PerfilInexistente_LanzaDomainValidationException()
    {
        var usuario = new Usuario(Guid.NewGuid(), "U1", "u1@test.com", "hash", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new AsignarPerfilUsuarioCommandHandler(
            _usuarioRepositorio,
            _inquilinoActual,
            _unidadDeTrabajo);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            handler.Handle(new AsignarPerfilUsuarioCommand(usuario.Id, Guid.NewGuid()), CancellationToken.None));
    }

    // ===========================================
    // 5. Pruebas de ListarUsuariosQueryHandler
    // ===========================================

    [Fact]
    public async Task ListarUsuarios_ConFiltros_RetornaElementosPaginadosConNombres()
    {
        // Arrange
        var u1 = new Usuario(Guid.NewGuid(), "Martin Suarez", "martin@test.com", "h", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        var u2 = new Usuario(Guid.NewGuid(), "Lucia Perez", "lucia@test.com", "h", _perfilOperador.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.AddRange(new[] { u1, u2 });

        var handler = new ListarUsuariosQueryHandler(_usuarioRepositorio, _inquilinoActual);

        // Act 1: Todos
        var resultadoTodos = await handler.Handle(new ListarUsuariosQuery(), CancellationToken.None);

        // Act 2: Filtro texto "Lucia"
        var resultadoLucia = await handler.Handle(new ListarUsuariosQuery("Lucia"), CancellationToken.None);

        // Act 3: Filtro por perfil
        var resultadoPerfilAdmin = await handler.Handle(new ListarUsuariosQuery(null, _perfilAdmin.Id), CancellationToken.None);

        // Assert
        Assert.Equal(2, resultadoTodos.TotalRegistros);
        Assert.Single(resultadoLucia.Elementos);
        Assert.Equal("Lucia Perez", resultadoLucia.Elementos[0].Nombre);
        Assert.Single(resultadoPerfilAdmin.Elementos);
        Assert.Equal("Martin Suarez", resultadoPerfilAdmin.Elementos[0].Nombre);
    }

    // ===============================================
    // 6. Pruebas de ObtenerUsuarioPorIdQueryHandler
    // ===============================================

    [Fact]
    public async Task ObtenerUsuarioPorId_UsuarioValido_RetornaDetalle()
    {
        // Arrange
        var usuario = new Usuario(Guid.NewGuid(), "Ana Gomez", "ana@test.com", "h", _perfilAdmin.Id, _inquilinoActual.OperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new ObtenerUsuarioPorIdQueryHandler(_usuarioRepositorio, _comercioRepositorio, _inquilinoActual);

        // Act
        var resultado = await handler.Handle(new ObtenerUsuarioPorIdQuery(usuario.Id), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("Ana Gomez", resultado.Nombre);
        Assert.Equal("Administrador", resultado.NombrePerfil);
    }

    [Fact]
    public async Task ObtenerUsuarioPorId_UsuarioDeOtroOperador_LanzaNotFoundException_AntiIdor()
    {
        var otroOperadorId = Guid.NewGuid();
        var usuario = new Usuario(Guid.NewGuid(), "Ana", "ana@test.com", "h", _perfilAdmin.Id, otroOperadorId);
        _usuarioRepositorio.Usuarios.Add(usuario);

        var handler = new ObtenerUsuarioPorIdQueryHandler(_usuarioRepositorio, _comercioRepositorio, _inquilinoActual);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new ObtenerUsuarioPorIdQuery(usuario.Id), CancellationToken.None));
    }

    // ===========================================
    // 7. Pruebas de ListarPerfilesQueryHandler
    // ===========================================

    [Fact]
    public async Task ListarPerfiles_RetornaTodosLosPerfiles()
    {
        var handler = new ListarPerfilesQueryHandler(_usuarioRepositorio);
        var resultado = await handler.Handle(new ListarPerfilesQuery(), CancellationToken.None);

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, p => p.Nombre == "Administrador");
        Assert.Contains(resultado, p => p.Nombre == "Operador");
    }
}

