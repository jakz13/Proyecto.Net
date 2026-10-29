using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;

namespace UltimaMilla.Infrastructure.Services;

/// <summary>
/// Generador y validador de tokens JWT firmado con algoritmo HMAC-SHA256 (RFC 7519).
/// Integra claims de usuario, perfil/rol, operador logístico (tenant) y comercio.
/// </summary>
public class TokenJwtService : ITokenJwtService
{
    private readonly string _claveSecreta;
    private readonly string _emisor;
    private readonly string _audiencia;
    private static readonly TimeSpan DuracionDefecto = TimeSpan.FromHours(8);

    public const string ClaveSecretaPorDefecto = "ClaveSecretaSuperSeguraParaDesarrolloLocalUltimaMilla2026!Minimo32Bytes";

    public TokenJwtService(IConfiguration configuration)
    {
        var claveConfigurada = configuration["Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(claveConfigurada)
            && string.Equals(configuration["Jwt:RequireConfiguredSecret"], "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Debe configurarse 'Jwt:SecretKey' (o variable de entorno JWT_SECRET_KEY) con al menos 32 caracteres.");
        }

        _claveSecreta = !string.IsNullOrWhiteSpace(claveConfigurada) ? claveConfigurada : ClaveSecretaPorDefecto;
        if (_claveSecreta.Length < 32)
        {
            throw new InvalidOperationException("La clave secreta JWT debe poseer al menos 32 caracteres para HMAC-SHA256.");
        }
        _emisor = configuration["Jwt:Issuer"] ?? "UltimaMilla.Api";
        _audiencia = configuration["Jwt:Audience"] ?? "UltimaMilla.Clients";
    }

    public TokenGeneradoDto GenerarToken(
        Usuario usuario,
        Perfil perfil,
        IReadOnlyCollection<string> codigosPermisos,
        TimeSpan? tiempoExpiracion = null)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        ArgumentNullException.ThrowIfNull(perfil);

        var ahoraUtc = DateTime.UtcNow;
        var expiraUtc = ahoraUtc.Add(tiempoExpiracion ?? DuracionDefecto);

        var header = new Dictionary<string, object>
        {
            { "alg", "HS256" },
            { "typ", "JWT" }
        };

        var payload = new Dictionary<string, object>
        {
            { "sub", usuario.Id.ToString() },
            { "name", usuario.Nombre },
            { "email", usuario.Email },
            { "role", perfil.Nombre },
            { "perfil_id", perfil.Id.ToString() },
            { "iss", _emisor },
            { "aud", _audiencia },
            { "iat", new DateTimeOffset(ahoraUtc).ToUnixTimeSeconds() },
            { "nbf", new DateTimeOffset(ahoraUtc).ToUnixTimeSeconds() },
            { "exp", new DateTimeOffset(expiraUtc).ToUnixTimeSeconds() },
            { "permisos", codigosPermisos ?? Array.Empty<string>() }
        };

        if (usuario.OperadorId.HasValue)
        {
            payload["operador_id"] = usuario.OperadorId.Value.ToString();
            payload["tenant_id"] = usuario.OperadorId.Value.ToString();
        }

        if (usuario.ComercioId.HasValue)
        {
            payload["comercio_id"] = usuario.ComercioId.Value.ToString();
        }

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = CodificarBase64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = CodificarBase64Url(Encoding.UTF8.GetBytes(payloadJson));

        var contenidoAFirmar = $"{headerBase64}.{payloadBase64}";
        var firmaBytes = CalcularHmacSha256(contenidoAFirmar, _claveSecreta);
        var firmaBase64 = CodificarBase64Url(firmaBytes);

        var tokenCompleto = $"{contenidoAFirmar}.{firmaBase64}";
        return new TokenGeneradoDto(tokenCompleto, expiraUtc);
    }

    /// <summary>
    /// Valida la firma criptográfica y la vigencia temporal de un token JWT, extrayendo el ClaimsPrincipal.
    /// </summary>
    public static ClaimsPrincipal? ValidarYExtraerClaims(string token, string? claveSecreta = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var partes = token.Trim().Split('.');
        if (partes.Length != 3)
            return null;

        var headerBase64 = partes[0];
        var payloadBase64 = partes[1];
        var firmaBase64 = partes[2];

        var clave = claveSecreta ?? ClaveSecretaPorDefecto;
        var contenidoFirmado = $"{headerBase64}.{payloadBase64}";
        var firmaEsperadaBytes = CalcularHmacSha256(contenidoFirmado, clave);
        var firmaEsperadaBase64 = CodificarBase64Url(firmaEsperadaBytes);

        // Comparación en tiempo constante para mitigar ataques de temporización
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(firmaBase64),
            Encoding.UTF8.GetBytes(firmaEsperadaBase64)))
        {
            return null;
        }

        try
        {
            var payloadBytes = DecodificarBase64Url(payloadBase64);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            using var documento = JsonDocument.Parse(payloadJson);
            var raiz = documento.RootElement;

            // Validar expiración
            if (raiz.TryGetProperty("exp", out var expElement))
            {
                var expSeconds = expElement.GetInt64();
                var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                if (DateTime.UtcNow > expDateTime)
                    return null;
            }

            var claims = new List<Claim>();

            if (raiz.TryGetProperty("sub", out var sub))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, sub.GetString() ?? string.Empty));

            if (raiz.TryGetProperty("name", out var name))
                claims.Add(new Claim(ClaimTypes.Name, name.GetString() ?? string.Empty));

            if (raiz.TryGetProperty("email", out var email))
                claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? string.Empty));

            if (raiz.TryGetProperty("role", out var role))
                claims.Add(new Claim(ClaimTypes.Role, role.GetString() ?? string.Empty));

            if (raiz.TryGetProperty("operador_id", out var opId))
            {
                var valorOp = opId.GetString() ?? string.Empty;
                claims.Add(new Claim("operador_id", valorOp));
                claims.Add(new Claim("tenant_id", valorOp));
                claims.Add(new Claim("OperadorId", valorOp));
            }

            if (raiz.TryGetProperty("comercio_id", out var comId))
            {
                var valorCom = comId.GetString() ?? string.Empty;
                claims.Add(new Claim("comercio_id", valorCom));
                claims.Add(new Claim("comercio_operador_id", valorCom));
            }

            if (raiz.TryGetProperty("permisos", out var perms) && perms.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in perms.EnumerateArray())
                {
                    var valorPerm = p.GetString();
                    if (!string.IsNullOrEmpty(valorPerm))
                        claims.Add(new Claim("permiso", valorPerm));
                }
            }

            var identidad = new ClaimsIdentity(claims, "Bearer");
            return new ClaimsPrincipal(identidad);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] CalcularHmacSha256(string dato, string clave)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clave));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(dato));
    }

    private static string CodificarBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] DecodificarBase64Url(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}
