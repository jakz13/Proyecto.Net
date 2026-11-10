using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Integration.Tests;

public class PasswordHasherServiceTests
{
    [Fact]
    public void ElHashGeneradoSeVerificaYUnaContrasenaDistintaSeRechaza()
    {
        var servicio = new PasswordHasherService();
        var hash = servicio.HashearPassword("demo1234");

        Assert.True(servicio.VerificarPassword("demo1234", hash));
        Assert.False(servicio.VerificarPassword("otra", hash));
    }

    [Theory]
    [InlineData("100000.c2FsLXNhbA==.aGFzaC1oYXNo")]   // formato antiguo "iteraciones.sal.hash"
    [InlineData("no-es-un-hash")]
    public void UnHashEnOtroFormato_SeRechazaSinLanzarExcepcion(string hashGuardado)
    {
        Assert.False(new PasswordHasherService().VerificarPassword("demo1234", hashGuardado));
    }
}
