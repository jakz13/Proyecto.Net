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

    // Clave con la que ValidarYExtraerClaims (estático) verifica la firma. La fija la API al arrancar para que
    // emisor y validador usen siempre la misma; si no se inicializa, rige la clave por defecto de desarrollo.
    private static string? _claveGlobal;

    // Emisor y audiencia que debe traer todo token para ser aceptado (los mismos con los que se emiten)
    private static string _emisorGlobal = "UltimaMilla.Api";
    private static string _audienciaGlobal = "UltimaMilla.Clients";

    public TokenJwtService(IConfiguration configuration)
    {
        _claveSecreta = ResolverClave(configuration);
        _emisor = configuration["Jwt:Issuer"] ?? "UltimaMilla.Api";
        _audiencia = configuration["Jwt:Audience"] ?? "UltimaMilla.Clients";
    }

    /// <summary>
    /// Fija la clave de validación a partir de la configuración. Debe llamarse una vez al iniciar la aplicación
    /// que valida tokens (la API), antes de atender solicitudes.
    /// </summary>
    public static void InicializarClaveGlobal(IConfiguration configuration)
    {
        _claveGlobal = ResolverClave(configuration);
        _emisorGlobal = configuration["Jwt:Issuer"] ?? "UltimaMilla.Api";
        _audienciaGlobal = configuration["Jwt:Audience"] ?? "UltimaMilla.Clients";
    }

    /// <summary>Clave con la que firma la plataforma (la del JWT), para derivar otras firmas de ella.</summary>
    public static string ClaveDeFirma(IConfiguration configuration) => ResolverClave(configuration);

    private static string ResolverClave(IConfiguration configuration)
    {
        var claveConfigurada = configuration["Jwt:SecretKey"]
            ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY");

        // La clave de desarrollo está en el repositorio, así que nunca debe firmar tokens en Production
        var entorno = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var exigirClave = string.Equals(configuration["Jwt:RequireConfiguredSecret"], "true", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(entorno, "Production", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(claveConfigurada) && exigirClave)
        {
            throw new InvalidOperationException("Debe configurarse 'Jwt:SecretKey' (o variable de entorno JWT_SECRET_KEY) con al menos 32 caracteres.");
        }

        var clave = !string.IsNullOrWhiteSpace(claveConfigurada) ? claveConfigurada : ClaveSecretaPorDefecto;
        if (clave.Length < 32)
        {
            throw new InvalidOperationException("La clave secreta JWT debe poseer al menos 32 caracteres para HMAC-SHA256.");
        }

        return clave;
    }

    public TokenGeneradoDto GenerarToken(
        Usuario usuario,
        Perfil perfil,
        IReadOnlyCollection<string> codigosPermisos,
        TimeSpan? tiempoExpiracion = null,
        Guid? comercioOperadorId = null,
        Guid? operadorId = null)
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
            { "role", perfil.RolDeAutorizacion },
            { "perfil_id", perfil.Id.ToString() },
            { "iss", _emisor },
            { "aud", _audiencia },
            { "iat", new DateTimeOffset(ahoraUtc).ToUnixTimeSeconds() },
            { "nbf", new DateTimeOffset(ahoraUtc).ToUnixTimeSeconds() },
            { "exp", new DateTimeOffset(expiraUtc).ToUnixTimeSeconds() },
            { "permisos", codigosPermisos ?? Array.Empty<string>() }
        };

        // Un usuario de comercio vinculado a varios operadores puede abrir sesión en cualquiera de ellos: el operador explícito
        // (ya validado por quien emite el token) manda sobre el operador de origen del usuario.
        var operadorDelToken = operadorId ?? usuario.OperadorId;
        if (operadorDelToken.HasValue)
        {
            payload["operador_id"] = operadorDelToken.Value.ToString();
            payload["tenant_id"] = operadorDelToken.Value.ToString();
        }

        // El sistema identifica al comercio por su relación con el operador (ComercioOperador), no por el comercio a secas.
        // Si el login la resolvió, ese es el valor del claim; si no, se mantiene el comportamiento anterior.
        var comercioDelToken = comercioOperadorId ?? usuario.ComercioId;
        if (comercioDelToken.HasValue)
        {
            payload["comercio_id"] = comercioDelToken.Value.ToString();
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

        // Solo se aceptan tokens HS256: el algoritmo lo fija el servidor, nunca el token (ataque "alg: none" o de confusión de algoritmo)
        if (!EncabezadoEsHs256(headerBase64))
            return null;

        var clave = claveSecreta ?? _claveGlobal ?? ClaveSecretaPorDefecto;
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
            // Un token sin vencimiento nunca es válido
            if (!raiz.TryGetProperty("exp", out var expElement) || expElement.ValueKind != JsonValueKind.Number)
                return null;

            var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64()).UtcDateTime;
            if (DateTime.UtcNow > expDateTime)
                return null;

            // Todavía no vigente (con un minuto de tolerancia por diferencias de reloj)
            if (raiz.TryGetProperty("nbf", out var nbfElement) && nbfElement.ValueKind == JsonValueKind.Number
                && DateTime.UtcNow < DateTimeOffset.FromUnixTimeSeconds(nbfElement.GetInt64()).UtcDateTime.AddMinutes(-1))
                return null;

            // Emisor y audiencia: un token firmado con la misma clave pero emitido para otro sistema no vale aquí
            if (!raiz.TryGetProperty("iss", out var issElement) || issElement.GetString() != _emisorGlobal)
                return null;
            if (!raiz.TryGetProperty("aud", out var audElement) || audElement.GetString() != _audienciaGlobal)
                return null;

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

    private static bool EncabezadoEsHs256(string headerBase64)
    {
        try
        {
            using var encabezado = JsonDocument.Parse(DecodificarBase64Url(headerBase64));
            return encabezado.RootElement.TryGetProperty("alg", out var alg) && alg.GetString() == "HS256";
        }
        catch
        {
            return false;
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
