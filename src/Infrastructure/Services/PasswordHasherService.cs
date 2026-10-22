using Microsoft.AspNetCore.Identity;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Servicio de hashing de contraseñas utilizando PBKDF2 con HMAC-SHA256 y salting criptográfico (estándar ASP.NET Identity).
/// Cumple con las directrices de seguridad OWASP para almacenamiento de credenciales.
/// </summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object UsuarioContexto = new();

    public string HashearPassword(string passwordPlano)
    {
        if (string.IsNullOrWhiteSpace(passwordPlano))
            throw new ArgumentException("La contraseña a hashear no puede ser vacía.", nameof(passwordPlano));

        return _hasher.HashPassword(UsuarioContexto, passwordPlano);
    }

    public bool VerificarPassword(string passwordPlano, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordPlano) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        var resultado = _hasher.VerifyHashedPassword(UsuarioContexto, passwordHash, passwordPlano);
        return resultado != PasswordVerificationResult.Failed;
    }
}
