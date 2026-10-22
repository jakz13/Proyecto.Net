using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Identidad.IniciarSesion;

/// <summary>
/// Manejador del comando UC-40 Iniciar sesión.
/// Aplica validación de credenciales, protección contra fuerza bruta con bloqueo temporal,
/// y emite el token JWT con perfil, operador y comercio asignado.
/// </summary>
public class IniciarSesionCommandHandler : IRequestHandler<IniciarSesionCommand, IniciarSesionResultadoDto>
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ITokenJwtService _tokenJwtService;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public const int MaximoIntentosPermitidos = 5;
    public static readonly TimeSpan DuracionBloqueoTemporal = TimeSpan.FromMinutes(15);

    public IniciarSesionCommandHandler(
        IUsuarioRepository usuarioRepository,
        IPasswordHasherService passwordHasher,
        ITokenJwtService tokenJwtService,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenJwtService = tokenJwtService ?? throw new ArgumentNullException(nameof(tokenJwtService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IniciarSesionResultadoDto> Handle(IniciarSesionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new DomainValidationException("El correo electrónico es obligatorio para iniciar sesión.");

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new DomainValidationException("La contraseña es obligatoria para iniciar sesión.");

        var emailNormalizado = request.Email.Trim().ToLowerInvariant();
        var ahora = _clock.UtcNow;

        var usuario = await _usuarioRepository.ObtenerPorEmailAsync(emailNormalizado, cancellationToken);
        if (usuario is null)
        {
            // Mensaje genérico para evitar enumeración de usuarios
            throw new CredencialesInvalidasException("Credenciales inválidas.");
        }

        // Validar si la cuenta está en período de bloqueo temporal
        if (usuario.EstaBloqueado(ahora))
        {
            throw new CuentaBloqueadaException(
                $"La cuenta se encuentra temporalmente bloqueada por reiterados intentos fallidos. Intente nuevamente después de las {usuario.BloqueadoHasta:HH:mm} UTC.",
                usuario.BloqueadoHasta);
        }

        // Validar si la cuenta está activa
        if (!usuario.Activo)
        {
            throw new CuentaInactivaException("La cuenta de usuario se encuentra inactiva. Contacte a su administrador.");
        }

        // Validar contraseña
        var passwordEsValido = _passwordHasher.VerificarPassword(request.Password, usuario.PasswordHash);
        if (!passwordEsValido)
        {
            usuario.RegistrarIntentoFallido(ahora, MaximoIntentosPermitidos, DuracionBloqueoTemporal);
            await _usuarioRepository.ActualizarAsync(usuario, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (usuario.EstaBloqueado(ahora))
            {
                throw new CuentaBloqueadaException(
                    "Ha superado el número máximo de intentos fallidos permitidos. Su cuenta ha sido bloqueada temporalmente por 15 minutos.",
                    usuario.BloqueadoHasta);
            }

            throw new CredencialesInvalidasException("Credenciales inválidas.");
        }

        // Inicio de sesión exitoso: resetear contador de fallos
        usuario.RegistrarAccesoExitoso();
        await _usuarioRepository.ActualizarAsync(usuario, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Obtener perfil y permisos asociados
        var perfil = await _usuarioRepository.ObtenerPerfilPorIdAsync(usuario.PerfilId, cancellationToken);
        var nombrePerfil = perfil?.Nombre ?? "SinPerfil";

        var permisos = await _usuarioRepository.ObtenerPermisosDePerfilAsync(usuario.PerfilId, cancellationToken);
        var codigosPermisos = permisos.Select(p => p.Codigo).ToList();

        // Generar token JWT con los claims requeridos
        var tokenGenerado = _tokenJwtService.GenerarToken(usuario, perfil ?? new Perfil(usuario.PerfilId, nombrePerfil, string.Empty), codigosPermisos);

        return new IniciarSesionResultadoDto(
            tokenGenerado.Token,
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            nombrePerfil,
            usuario.OperadorId,
            usuario.ComercioId,
            codigosPermisos,
            tokenGenerado.ExpiraUtc);
    }
}
