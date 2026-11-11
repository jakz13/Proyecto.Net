using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Tests.Fakes;

public class FakePasswordHasherService : IPasswordHasherService
{
    public string HashearPassword(string passwordPlano) => $"hashed_{passwordPlano}";

    public bool VerificarPassword(string passwordPlano, string passwordHash) =>
        passwordHash == $"hashed_{passwordPlano}";
}

public class FakeTokenJwtService : ITokenJwtService
{
    public Guid? UltimoComercioOperadorId { get; private set; }

    public TokenGeneradoDto GenerarToken(
        Usuario usuario,
        Perfil perfil,
        IReadOnlyCollection<string> codigosPermisos,
        TimeSpan? tiempoExpiracion = null,
        Guid? comercioOperadorId = null,
        Guid? operadorId = null)
    {
        UltimoComercioOperadorId = comercioOperadorId;
        var expira = DateTime.UtcNow.Add(tiempoExpiracion ?? TimeSpan.FromHours(8));
        return new TokenGeneradoDto($"fake.jwt.token.{usuario.Id}.{perfil.Nombre}", expira);
    }
}
