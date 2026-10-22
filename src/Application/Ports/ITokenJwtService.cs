using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Application.Ports;

public record TokenGeneradoDto(string Token, DateTime ExpiraUtc);

public interface ITokenJwtService
{
    TokenGeneradoDto GenerarToken(
        Usuario usuario,
        Perfil perfil,
        IReadOnlyCollection<string> codigosPermisos,
        TimeSpan? tiempoExpiracion = null);
}
