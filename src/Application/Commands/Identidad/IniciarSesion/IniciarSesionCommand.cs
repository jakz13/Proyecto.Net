using MediatR;

namespace UltimaMilla.Application.Commands.Identidad.IniciarSesion;

/// <summary>
/// Comando para autenticación y emisión de token según UC-40.
/// </summary>
public record IniciarSesionCommand(string Email, string Password) : IRequest<IniciarSesionResultadoDto>;

/// <summary>
/// Resultado del inicio de sesión con token JWT y datos de contexto del usuario.
/// </summary>
public record IniciarSesionResultadoDto(
    string Token,
    Guid UsuarioId,
    string Nombre,
    string Email,
    string Perfil,
    Guid? OperadorId,
    Guid? ComercioId,
    IReadOnlyList<string> Permisos,
    DateTime ExpiraUtc);

/// <summary>
/// Contrato de solicitud para endpoints de API.
/// </summary>
public record IniciarSesionRequest(string Email, string Password);
