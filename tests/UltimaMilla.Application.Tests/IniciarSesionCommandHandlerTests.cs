using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Pruebas exhaustivas y de resiliencia para el caso de uso UC-40 Iniciar sesión.
/// Valida camino feliz para todos los perfiles, seguridad ante entradas nulas/vacías,
/// mitigación de fuerza bruta con bloqueo temporal y aislamiento multitenant.
/// </summary>
public class IniciarSesionCommandHandlerTests
{
    private readonly FakeUsuarioRepository _usuarioRepository;
    private readonly FakePasswordHasherService _passwordHasher;
    private readonly FakeTokenJwtService _tokenJwtService;
    private readonly FakeClock _clock;
    private readonly FakeUnitOfWork _unitOfWork;
    private readonly IniciarSesionCommandHandler _handler;

    private static readonly Guid Operador1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Operador2Id = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid ComercioId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly Guid PerfilAdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PerfilDespachadorId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid PerfilOperarioId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid PerfilRepartidorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid PerfilComercioId = Guid.Parse("10000000-0000-0000-0000-000000000005");

    public IniciarSesionCommandHandlerTests()
    {
        _usuarioRepository = new FakeUsuarioRepository();
        _passwordHasher = new FakePasswordHasherService();
        _tokenJwtService = new FakeTokenJwtService();
        _clock = new FakeClock { UtcNow = new DateTime(2026, 10, 21, 14, 0, 0, DateTimeKind.Utc) };
        _unitOfWork = new FakeUnitOfWork();

        _handler = new IniciarSesionCommandHandler(
            _usuarioRepository,
            _passwordHasher,
            _tokenJwtService,
            _clock,
            _unitOfWork);

        InicializarDatosPrueba();
    }

    private void InicializarDatosPrueba()
    {
        // Perfiles
        var pAdmin = new Perfil(PerfilAdminId, "Administrador", "Acceso total");
        var pDespacho = new Perfil(PerfilDespachadorId, "Despachador", "Despacho");
        var pOperario = new Perfil(PerfilOperarioId, "Operario", "Depósito");
        var pRepartidor = new Perfil(PerfilRepartidorId, "Repartidor", "Reparto");
        var pComercio = new Perfil(PerfilComercioId, "Comercio", "Comercio");

        _usuarioRepository.Perfiles.AddRange(new[] { pAdmin, pDespacho, pOperario, pRepartidor, pComercio });

        // Permisos
        var permAdmin = new Permiso(Guid.NewGuid(), "OPERACION_ADMIN", "Admin");
        var permDespacho = new Permiso(Guid.NewGuid(), "RUTAS_DESPACHO", "Rutas");
        _usuarioRepository.Permisos.AddRange(new[] { permAdmin, permDespacho });
        _usuarioRepository.PerfilPermisos.Add(new PerfilPermiso(PerfilAdminId, permAdmin.Id));
        _usuarioRepository.PerfilPermisos.Add(new PerfilPermiso(PerfilDespachadorId, permDespacho.Id));

        // Usuarios activos
        var uAdmin = new Usuario(Guid.NewGuid(), "Admin Uno", "admin@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilAdminId, operadorId: Operador1Id);
        var uDespacho = new Usuario(Guid.NewGuid(), "Despacho Uno", "despacho@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilDespachadorId, operadorId: Operador1Id);
        var uOperario = new Usuario(Guid.NewGuid(), "Operario Uno", "deposito@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilOperarioId, operadorId: Operador1Id);
        var uRepartidor = new Usuario(Guid.NewGuid(), "Repartidor Uno", "repartidor@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilRepartidorId, operadorId: Operador1Id);
        var uComercio = new Usuario(Guid.NewGuid(), "Comercio Uno", "comercio@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilComercioId, operadorId: Operador1Id, comercioId: ComercioId);

        // Usuario en segundo operador (multitenancy)
        var uAdminOp2 = new Usuario(Guid.NewGuid(), "Admin Dos", "admin2@gmail.com", _passwordHasher.HashearPassword("Password.123!"), PerfilAdminId, operadorId: Operador2Id);

        _usuarioRepository.Usuarios.AddRange(new[] { uAdmin, uDespacho, uOperario, uRepartidor, uComercio, uAdminOp2 });
    }

    [Theory]
    [InlineData("admin@gmail.com", "Administrador", true, false)]
    [InlineData("despacho@gmail.com", "Despachador", true, false)]
    [InlineData("deposito@gmail.com", "Operario", true, false)]
    [InlineData("repartidor@gmail.com", "Repartidor", true, false)]
    [InlineData("comercio@gmail.com", "Comercio", true, true)]
    public async Task IniciarSesion_ConCredencialesValidas_EmiteTokenConPerfilYAlcanceCorrecto(
        string email,
        string rolEsperado,
        bool esperaOperador,
        bool esperaComercio)
    {
        // Act
        var comando = new IniciarSesionCommand(email, "Password.123!");
        var resultado = await _handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.False(string.IsNullOrWhiteSpace(resultado.Token));
        Assert.Equal(rolEsperado, resultado.Perfil);
        Assert.Equal(email.ToLowerInvariant(), resultado.Email);
        Assert.True(resultado.ExpiraUtc > _clock.UtcNow);

        if (esperaOperador)
            Assert.Equal(Operador1Id, resultado.OperadorId);

        if (esperaComercio)
            Assert.Equal(ComercioId, resultado.ComercioId);
        else
            Assert.Null(resultado.ComercioId);
    }

    [Fact]
    public async Task IniciarSesion_Multitenancy_EmiteTokenConOperadorCorrespondiente()
    {
        // Act: Login para el usuario del segundo operador
        var comando = new IniciarSesionCommand("admin2@gmail.com", "Password.123!");
        var resultado = await _handler.Handle(comando, CancellationToken.None);

        // Assert: El token queda acotado al Operador 2
        Assert.NotNull(resultado);
        Assert.Equal(Operador2Id, resultado.OperadorId);
        Assert.NotEqual(Operador1Id, resultado.OperadorId);
    }

    [Fact]
    public async Task IniciarSesion_EmailEnMayusculas_NormalizaYAutenticaExitosamente()
    {
        // Act
        var comando = new IniciarSesionCommand("ADMIN@GMAIL.COM", "Password.123!");
        var resultado = await _handler.Handle(comando, CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("admin@gmail.com", resultado.Email);
    }

    [Fact]
    public async Task IniciarSesion_ComandoNulo_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(null, "Password.123!")]
    [InlineData("", "Password.123!")]
    [InlineData("   ", "Password.123!")]
    [InlineData("admin@gmail.com", null)]
    [InlineData("admin@gmail.com", "")]
    [InlineData("admin@gmail.com", "   ")]
    public async Task IniciarSesion_CamposObligatoriosNulosOVacios_LanzaDomainValidationException(string? email, string? password)
    {
        var comando = new IniciarSesionCommand(email!, password!);
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            _handler.Handle(comando, CancellationToken.None));
    }

    [Fact]
    public async Task IniciarSesion_UsuarioInexistente_LanzaCredencialesInvalidasExceptionConMensajeGenerico()
    {
        // Act & Assert: mensaje genérico para evitar enumeración de cuentas
        var comando = new IniciarSesionCommand("fantasma@correo.com", "Password.123!");
        var ex = await Assert.ThrowsAsync<CredencialesInvalidasException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal("Credenciales inválidas.", ex.Message);
    }

    [Fact]
    public async Task IniciarSesion_ContrasenaIncorrecta_LanzaCredencialesInvalidasEIncrementaIntentosFallidos()
    {
        // Arrange
        var comando = new IniciarSesionCommand("admin@gmail.com", "PasswordEquivocado!");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CredencialesInvalidasException>(() =>
            _handler.Handle(comando, CancellationToken.None));

        Assert.Equal("Credenciales inválidas.", ex.Message);

        var usuario = await _usuarioRepository.ObtenerPorEmailAsync("admin@gmail.com");
        Assert.NotNull(usuario);
        Assert.Equal(1, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
        Assert.True(_unitOfWork.Confirmaciones > 0);
    }

    [Fact]
    public async Task IniciarSesion_CincoIntentosFallidosConsecutivos_BloqueaCuentaTemporalmentePor15Minutos()
    {
        // Arrange
        var email = "admin@gmail.com";

        // Act: 4 intentos fallidos
        for (int i = 1; i <= 4; i++)
        {
            await Assert.ThrowsAsync<CredencialesInvalidasException>(() =>
                _handler.Handle(new IniciarSesionCommand(email, $"Malo_{i}"), CancellationToken.None));
        }

        var usuario = await _usuarioRepository.ObtenerPorEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.Equal(4, usuario.IntentosFallidos);
        Assert.False(usuario.EstaBloqueado(_clock.UtcNow));

        // Act: 5to intento fallido -> debe bloquear la cuenta
        var exBloqueo = await Assert.ThrowsAsync<CuentaBloqueadaException>(() =>
            _handler.Handle(new IniciarSesionCommand(email, "Malo_5"), CancellationToken.None));

        Assert.Contains("bloqueada temporalmente", exBloqueo.Message);

        // Assert: usuario bloqueado por 15 minutos en el repositorio
        Assert.Equal(5, usuario.IntentosFallidos);
        Assert.True(usuario.EstaBloqueado(_clock.UtcNow));
        Assert.Equal(_clock.UtcNow.AddMinutes(15), usuario.BloqueadoHasta);

        // Intento subsecuente (incluso con password correcto) debe ser rechazado por bloqueo
        var exBloqueoRepetido = await Assert.ThrowsAsync<CuentaBloqueadaException>(() =>
            _handler.Handle(new IniciarSesionCommand(email, "Password.123!"), CancellationToken.None));

        Assert.NotNull(exBloqueoRepetido.BloqueadoHasta);
    }

    [Fact]
    public async Task IniciarSesion_DespuesDeExpirarElBloqueo_PermiteAccesoConPasswordCorrecto()
    {
        // Arrange: bloquear usuario
        var email = "admin@gmail.com";
        for (int i = 1; i <= 5; i++)
        {
            try { await _handler.Handle(new IniciarSesionCommand(email, "Malo"), CancellationToken.None); }
            catch { }
        }

        var usuario = await _usuarioRepository.ObtenerPorEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.True(usuario.EstaBloqueado(_clock.UtcNow));

        // Act: El reloj avanza 16 minutos (el bloqueo de 15 min expiró)
        _clock.UtcNow = _clock.UtcNow.AddMinutes(16);

        // Assert: Ahora el inicio de sesión debe ser exitoso
        var resultado = await _handler.Handle(new IniciarSesionCommand(email, "Password.123!"), CancellationToken.None);
        Assert.NotNull(resultado);
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task IniciarSesion_LoginExitosoTrasIntentosPrevios_ReiniciaContadorDeIntentosFallidos()
    {
        // Arrange: 2 intentos fallidos
        var email = "admin@gmail.com";
        await Assert.ThrowsAsync<CredencialesInvalidasException>(() =>
            _handler.Handle(new IniciarSesionCommand(email, "Malo_1"), CancellationToken.None));
        await Assert.ThrowsAsync<CredencialesInvalidasException>(() =>
            _handler.Handle(new IniciarSesionCommand(email, "Malo_2"), CancellationToken.None));

        var usuario = await _usuarioRepository.ObtenerPorEmailAsync(email);
        Assert.NotNull(usuario);
        Assert.Equal(2, usuario.IntentosFallidos);

        // Act: 3er intento exitoso
        var resultado = await _handler.Handle(new IniciarSesionCommand(email, "Password.123!"), CancellationToken.None);

        // Assert: se reinicia a 0
        Assert.NotNull(resultado);
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Fact]
    public async Task IniciarSesion_UsuarioInactivo_LanzaCuentaInactivaException()
    {
        // Arrange: desactivar usuario
        var usuario = await _usuarioRepository.ObtenerPorEmailAsync("admin@gmail.com");
        Assert.NotNull(usuario);
        usuario.Desactivar();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<CuentaInactivaException>(() =>
            _handler.Handle(new IniciarSesionCommand("admin@gmail.com", "Password.123!"), CancellationToken.None));

        Assert.Contains("inactiva", ex.Message);
    }
}
