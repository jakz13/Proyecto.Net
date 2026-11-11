using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Services.Handlers;

namespace UltimaMilla.Mobile.Services.Auth;

public record ResultadoLogin(
    bool Exitoso,
    string Mensaje,
    Guid? RepartidorId = null,
    string? Nombre = null,
    string? Email = null,
    Guid? TenantId = null
);

public interface IAuthService
{
    string? AccessToken { get; }
    Guid? RepartidorId { get; }
    Guid? UsuarioId { get; }
    string? Nombre { get; }
    string? Email { get; }
    Guid? TenantId { get; }

    Task<ResultadoLogin> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<bool> EstaAutenticadoAsync();
    Task CerrarSesionAsync();
    Task ActualizarRepartidorIdAsync(Guid repartidorId);
}

public class AuthService : IAuthService
{
    public const string RepartidorIdKey = "auth_repartidor_id";
    public const string UsuarioIdKey = "auth_usuario_id";
    public const string NombreKey = "auth_nombre";
    public const string EmailKey = "auth_email";
    public const string TenantIdKey = "auth_tenant_id";
    public const string ExpiraUtcKey = "auth_exp_utc";
    public const string PasswordHashKey = "auth_password_hash";
    public const string DemoRepartidorEmail = "repartidor@gmail.com";
    public const string DemoRepartidorPassword = "Repartidor.2026!";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthService> _logger;

    public string? AccessToken { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? Nombre { get; private set; }
    public string? Email { get; private set; }
    public Guid? TenantId { get; private set; }

    public AuthService(IHttpClientFactory httpClientFactory, ILogger<AuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ResultadoLogin> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return new ResultadoLogin(false, "Debe ingresar su correo electrónico y contraseña.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            var payload = new LoginRequestDto(email.Trim(), password);

            var response = await client.PostAsJsonAsync("/api/v1/auth/login", payload, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response = await client.PostAsJsonAsync("/api/auth/login", payload, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await ExtraerMensajeErrorAsync(response, cancellationToken);
                return new ResultadoLogin(false, errorMsg);
            }

            var loginResult = await response.Content.ReadFromJsonAsync<LoginResponseDto>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken);

            if (loginResult is null || string.IsNullOrWhiteSpace(loginResult.Token))
            {
                return new ResultadoLogin(false, "El servidor no devolvió un token de sesión válido.");
            }

            // Validar perfil autorizado para la app móvil de calle
            if (!string.IsNullOrWhiteSpace(loginResult.Perfil) &&
                !string.Equals(loginResult.Perfil, "Repartidor", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(loginResult.Perfil, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Acceso denegado a usuario con perfil '{Perfil}' en app móvil.", loginResult.Perfil);
                return new ResultadoLogin(false, $"Acceso denegado: esta aplicación es exclusiva para repartidores. El perfil de su usuario es '{loginResult.Perfil}'.");
            }

            // Parsear claims del JWT emitido
            var claims = ParsearClaimsJwt(loginResult.Token);

            var token = loginResult.Token.Trim();
            var usuarioId = ExtraerGuidClaim(claims, "sub", "usuario_id") ?? loginResult.UsuarioId;
            var repartidorId = ExtraerGuidClaim(claims, "repartidor_id", "sub") ?? loginResult.UsuarioId;
            var nombre = ExtraerStringClaim(claims, "name") ?? loginResult.Nombre;
            var emailResuelto = ExtraerStringClaim(claims, "email") ?? loginResult.Email;
            var tenantId = ExtraerGuidClaim(claims, "tenant_id", "operador_id") ?? loginResult.OperadorId;
            var expiraUtc = ExtraerExpiracionUtc(claims) ?? loginResult.ExpiraUtc;

            // Guardar en memoria y en AuthDelegatingHandler para todas las llamadas HTTP subsiguientes
            AccessToken = token;
            UsuarioId = usuarioId;
            RepartidorId = repartidorId;
            Nombre = nombre;
            Email = emailResuelto;
            TenantId = tenantId;
            AuthDelegatingHandler.CurrentTokenInMemory = token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Persistir de forma segura en SecureStorage del dispositivo
            await GuardarEnSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey, token);
            await GuardarEnSecureStorageAsync(UsuarioIdKey, usuarioId.ToString());
            await GuardarEnSecureStorageAsync(RepartidorIdKey, repartidorId.ToString());
            await GuardarEnSecureStorageAsync(NombreKey, nombre);
            await GuardarEnSecureStorageAsync(EmailKey, emailResuelto);
            if (tenantId.HasValue)
            {
                await GuardarEnSecureStorageAsync(TenantIdKey, tenantId.Value.ToString());
            }
            await GuardarEnSecureStorageAsync(ExpiraUtcKey, expiraUtc.ToUniversalTime().ToString("O"));

            using var sha = System.Security.Cryptography.SHA256.Create();
            var passwordHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            await GuardarEnSecureStorageAsync(PasswordHashKey, passwordHash);

            _logger.LogInformation("Sesión iniciada correctamente para el repartidor {Email} (Tenant: {TenantId}).", emailResuelto, tenantId);

            return new ResultadoLogin(
                Exitoso: true,
                Mensaje: "Inicio de sesión exitoso.",
                RepartidorId: repartidorId,
                Nombre: nombre,
                Email: emailResuelto,
                TenantId: tenantId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Fallo de conexión al intentar iniciar sesión contra el backend.");
            return new ResultadoLogin(
                false,
                "No fue posible conectar con el servidor de autenticación. Verifique que el backend se encuentre en ejecución y que el dispositivo tenga conectividad de red.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Tiempo de espera agotado al intentar autenticar.");
            return new ResultadoLogin(
                false,
                "El servidor tardó demasiado en responder. Verifique la conexión de red e intente nuevamente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en LoginAsync.");
            return new ResultadoLogin(false, $"No se pudo completar el inicio de sesión: {ex.Message}");
        }
    }

    public async Task<bool> EstaAutenticadoAsync()
    {
        try
        {
            var token = AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = await LeerDeSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey);
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var claims = ParsearClaimsJwt(token);
            var expiraUtc = ExtraerExpiracionUtc(claims);

            if (!expiraUtc.HasValue)
            {
                var expGuardada = await LeerDeSecureStorageAsync(ExpiraUtcKey);
                if (DateTime.TryParse(expGuardada, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedExp))
                {
                    expiraUtc = parsedExp.ToUniversalTime();
                }
            }

            if (expiraUtc.HasValue && DateTime.UtcNow >= expiraUtc.Value)
            {
                _logger.LogInformation("El token JWT almacenado ha expirado ({ExpiraUtc}). Limpiando sesión.", expiraUtc.Value);
                await LimpiarCredencialesAsync();
                return false;
            }

            // Restaurar sesión en memoria a partir de los claims y SecureStorage
            AccessToken = token;
            AuthDelegatingHandler.CurrentTokenInMemory = token;

            var subGuid = ExtraerGuidClaim(claims, "sub", "usuario_id");
            var repStorage = await LeerDeSecureStorageAsync(RepartidorIdKey);
            var usrStorage = await LeerDeSecureStorageAsync(UsuarioIdKey);
            var tenantStorage = await LeerDeSecureStorageAsync(TenantIdKey);

            UsuarioId = Guid.TryParse(usrStorage, out var uId) ? uId : subGuid;
            RepartidorId = Guid.TryParse(repStorage, out var rId) ? rId : subGuid;
            Nombre = await LeerDeSecureStorageAsync(NombreKey) ?? ExtraerStringClaim(claims, "name");
            Email = await LeerDeSecureStorageAsync(EmailKey) ?? ExtraerStringClaim(claims, "email");
            TenantId = Guid.TryParse(tenantStorage, out var tId) ? tId : ExtraerGuidClaim(claims, "tenant_id", "operador_id");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al verificar estado de autenticación en SecureStorage.");
            return false;
        }
    }

    public async Task ActualizarRepartidorIdAsync(Guid repartidorId)
    {
        if (repartidorId == Guid.Empty)
            return;

        RepartidorId = repartidorId;
        await GuardarEnSecureStorageAsync(RepartidorIdKey, repartidorId.ToString());
    }

    public async Task CerrarSesionAsync()
    {
        await LimpiarCredencialesAsync();

        if (Shell.Current is not null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("//LoginPage");
            });
        }
    }

    private async Task LimpiarCredencialesAsync()
    {
        AccessToken = null;
        RepartidorId = null;
        UsuarioId = null;
        Nombre = null;
        Email = null;
        TenantId = null;
        AuthDelegatingHandler.CurrentTokenInMemory = null;

        EliminarDeSecureStorage(AuthDelegatingHandler.AuthTokenKey);
        EliminarDeSecureStorage(RepartidorIdKey);
        EliminarDeSecureStorage(UsuarioIdKey);
        EliminarDeSecureStorage(NombreKey);
        EliminarDeSecureStorage(EmailKey);
        EliminarDeSecureStorage(TenantIdKey);
        EliminarDeSecureStorage(ExpiraUtcKey);
        EliminarDeSecureStorage(PasswordHashKey);

        await Task.CompletedTask;
    }

    private static async Task<string> ExtraerMensajeErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                {
                    return errorProp.GetString() ?? "Credenciales inválidas.";
                }
            }
        }
        catch
        {
            // Ignorar error de parseo
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Correo electrónico o contraseña incorrectos.",
            System.Net.HttpStatusCode.Locked => "La cuenta se encuentra bloqueada temporalmente por reiterados intentos fallidos.",
            System.Net.HttpStatusCode.Forbidden => "La cuenta no se encuentra activa o no tiene permisos de acceso.",
            _ => $"No fue posible autenticar (HTTP {(int)response.StatusCode})."
        };
    }

    private static Dictionary<string, JsonElement> ParsearClaimsJwt(string jwt)
    {
        var resultado = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var partes = jwt.Split('.');
            if (partes.Length < 2)
                return resultado;

            var payloadBase64 = partes[1]
                .Replace('-', '+')
                .Replace('_', '/');

            switch (payloadBase64.Length % 4)
            {
                case 2: payloadBase64 += "=="; break;
                case 3: payloadBase64 += "="; break;
            }

            var jsonBytes = Convert.FromBase64String(payloadBase64);
            var json = Encoding.UTF8.GetString(jsonBytes);
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                resultado[prop.Name] = prop.Value.Clone();
            }
        }
        catch
        {
            // Si el token no tiene formato JWT estándar, retornar diccionario vacío
        }

        return resultado;
    }

    private static string? ExtraerStringClaim(Dictionary<string, JsonElement> claims, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (claims.TryGetValue(k, out var val))
            {
                var str = val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }
        return null;
    }

    private static Guid? ExtraerGuidClaim(Dictionary<string, JsonElement> claims, params string[] keys)
    {
        var str = ExtraerStringClaim(claims, keys);
        return Guid.TryParse(str, out var guid) ? guid : null;
    }

    private static DateTime? ExtraerExpiracionUtc(Dictionary<string, JsonElement> claims)
    {
        if (claims.TryGetValue("exp", out var expEl))
        {
            if (expEl.ValueKind == JsonValueKind.Number && expEl.TryGetInt64(out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
            if (expEl.ValueKind == JsonValueKind.String && long.TryParse(expEl.GetString(), out var parsedSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(parsedSeconds).UtcDateTime;
            }
        }
        return null;
    }

    private static async Task GuardarEnSecureStorageAsync(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            Preferences.Default.Set(key, value);
        }
    }

    private static async Task<string?> LeerDeSecureStorageAsync(string key)
    {
        try
        {
            var val = await SecureStorage.Default.GetAsync(key);
            if (!string.IsNullOrWhiteSpace(val))
                return val;
        }
        catch
        {
            // Fallback a Preferences en entornos de escritorio sin contenedor seguro
        }

        try
        {
            var pref = Preferences.Default.Get<string?>(key, null);
            return !string.IsNullOrWhiteSpace(pref) ? pref : null;
        }
        catch
        {
            return null;
        }
    }

    private static void EliminarDeSecureStorage(string key)
    {
        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch
        {
            // Ignorar
        }

        try
        {
            Preferences.Default.Remove(key);
        }
        catch
        {
            // Ignorar
        }
    }
}
