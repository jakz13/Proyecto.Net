using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Domain.Tests;

public class UsuarioDominioTests
{
    private readonly Guid _usuarioId = Guid.NewGuid();
    private readonly Guid _perfilId = Guid.NewGuid();
    private readonly Guid _operadorId = Guid.NewGuid();

    [Fact]
    public void CrearUsuario_ConDatosValidos_InicializaCorrectamente()
    {
        // Act
        var usuario = new Usuario(_usuarioId, "Juan Pérez", "JUAN@CORREO.COM", "hash123", _perfilId, _operadorId);

        // Assert
        Assert.Equal(_usuarioId, usuario.Id);
        Assert.Equal("Juan Pérez", usuario.Nombre);
        Assert.Equal("juan@correo.com", usuario.Email); // normalizado
        Assert.Equal("hash123", usuario.PasswordHash);
        Assert.Equal(_perfilId, usuario.PerfilId);
        Assert.Equal(_operadorId, usuario.OperadorId);
        Assert.True(usuario.Activo);
        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearUsuario_NombreInvalido_LanzaDomainValidationException(string? nombreInvalido)
    {
        Assert.Throws<DomainValidationException>(() =>
            new Usuario(_usuarioId, nombreInvalido!, "test@correo.com", "hash", _perfilId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearUsuario_EmailInvalido_LanzaDomainValidationException(string? emailInvalido)
    {
        Assert.Throws<DomainValidationException>(() =>
            new Usuario(_usuarioId, "Nombre", emailInvalido!, "hash", _perfilId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CrearUsuario_PasswordHashInvalido_LanzaDomainValidationException(string? hashInvalido)
    {
        Assert.Throws<DomainValidationException>(() =>
            new Usuario(_usuarioId, "Nombre", "test@correo.com", hashInvalido!, _perfilId));
    }

    [Fact]
    public void CrearUsuario_IdVacio_LanzaDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Usuario(Guid.Empty, "Nombre", "test@correo.com", "hash", _perfilId));
    }

    [Fact]
    public void CrearUsuario_PerfilIdVacio_LanzaDomainValidationException()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", Guid.Empty));
    }

    [Fact]
    public void DesactivarYActivar_ModificaEstadoActivo()
    {
        var usuario = new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", _perfilId);
        Assert.True(usuario.Activo);

        usuario.Desactivar();
        Assert.False(usuario.Activo);

        usuario.Activar();
        Assert.True(usuario.Activo);
    }

    [Fact]
    public void RegistrarIntentoFallido_IncrementaContadorYBloqueaAlAlcanzarLimite()
    {
        var ahora = new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc);
        var usuario = new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", _perfilId);

        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 3, TimeSpan.FromMinutes(10));
        Assert.Equal(1, usuario.IntentosFallidos);
        Assert.False(usuario.EstaBloqueado(ahora));

        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 3, TimeSpan.FromMinutes(10));
        Assert.Equal(2, usuario.IntentosFallidos);
        Assert.False(usuario.EstaBloqueado(ahora));

        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 3, TimeSpan.FromMinutes(10));
        Assert.Equal(3, usuario.IntentosFallidos);
        Assert.True(usuario.EstaBloqueado(ahora));
        Assert.Equal(ahora.AddMinutes(10), usuario.BloqueadoHasta);

        // Pasado el tiempo de bloqueo
        Assert.False(usuario.EstaBloqueado(ahora.AddMinutes(11)));
    }

    [Fact]
    public void RegistrarIntentoFallido_MaxIntentosInvalido_LanzaDomainValidationException()
    {
        var ahora = DateTime.UtcNow;
        var usuario = new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", _perfilId);

        Assert.Throws<DomainValidationException>(() =>
            usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 0));
        Assert.Throws<DomainValidationException>(() =>
            usuario.RegistrarIntentoFallido(ahora, maximoIntentos: -1));
    }

    [Fact]
    public void RegistrarAccesoExitoso_ReseteaContadorYBloqueo()
    {
        var ahora = DateTime.UtcNow;
        var usuario = new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", _perfilId);
        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 2);
        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 2);
        Assert.True(usuario.EstaBloqueado(ahora));

        usuario.RegistrarAccesoExitoso();

        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
        Assert.False(usuario.EstaBloqueado(ahora));
    }

    [Fact]
    public void Desbloquear_ReseteaContadorYBloqueo()
    {
        var ahora = DateTime.UtcNow;
        var usuario = new Usuario(_usuarioId, "Nombre", "test@correo.com", "hash", _perfilId);
        usuario.RegistrarIntentoFallido(ahora, maximoIntentos: 1);
        Assert.True(usuario.EstaBloqueado(ahora));

        usuario.Desbloquear();

        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
    }
}
