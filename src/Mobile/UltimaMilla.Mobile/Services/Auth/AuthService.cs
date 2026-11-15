using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Services.Handlers;
using UltimaMilla.Mobile.Services.Realtime;
using UltimaMilla.Mobile.Services.Theme;
using UltimaMilla.Mobile.Services.Tracking;

namespace UltimaMilla.Mobile.Services.Auth;

public record ResultadoLogin(
    bool Exitoso,
    string Mensaje,
    Guid? RepartidorId = null,
    string? Nombre = null,
    string? Email = null,
    Guid? TenantId = null,
    string? Perfil = null
);

public interface IAuthService
{
    string? AccessToken { get; }
    Guid? RepartidorId { get; }
    Guid? UsuarioId { get; }
    string? Nombre { get; }
    string? Email { get; }
    Guid? TenantId { get; }
    string? Perfil { get; }
    bool EsOperarioODeposito { get; }
    bool EsAdministrador { get; }

    Task<ResultadoLogin> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<ResultadoLogin> IniciarSesionAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<bool> EstaAutenticadoAsync();
    Task<bool> TieneSesionBiometricaGuardadaAsync();
    Task<string?> ObtenerEmailBiometricoGuardadoAsync();
    Task<ResultadoLogin> RestaurarSesionBiometricaAsync(CancellationToken cancellationToken = default);
    Task RevocarVinculoBiometricoAsync();
    Task CerrarSesionAsync();
    Task ManejarSesionDesplazadaAsync(string? motivo = null);
    Task<bool> ValidarSesionVigenteEnServidorAsync(CancellationToken cancellationToken = default);
    Task ActualizarRepartidorIdAsync(Guid repartidorId);
}

public class AuthService : IAuthService
{
    public const string AuthTokenActivoKey = "auth_token_activo";
    public const string RepartidorIdKey = "auth_repartidor_id";
    public const string UsuarioIdKey = "auth_usuario_id";
    public const string NombreKey = "auth_nombre";
    public const string EmailKey = "auth_email";
    public const string TenantIdKey = "auth_tenant_id";
    public const string PerfilKey = "auth_perfil";
    public const string ExpiraUtcKey = "auth_exp_utc";
    public const string PasswordHashKey = "auth_password_hash";

    // Claves dedicadas al vínculo biométrico persistente en Android Keystore / SecureStorage
    public const string BiometriaHabilitadaKey = "biometria_habilitada";
    public const string BiometriaUsuarioEmailKey = "biometria_usuario_email";
    public const string BiometriaTokenSesionKey = "biometria_token_sesion";
    public const string BiometriaRefreshTokenKey = "biometria_refresh_token";
    public const string BiometriaUsuarioIdKey = "biometria_usuario_id";
    public const string BiometriaRepartidorIdKey = "biometria_repartidor_id";
    public const string BiometriaNombreKey = "biometria_nombre";
    public const string BiometriaPerfilKey = "biometria_perfil";
    public const string BiometriaTenantIdKey = "biometria_tenant_id";
    public const string BiometriaExpiraUtcKey = "biometria_exp_utc";
    public const string BiometriaPasswordHashKey = "biometria_password_hash";
    public const string BiometriaCredencialKey = "biometria_credencial";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthService> _logger;
    private readonly ILocalDatabaseService _localDatabaseService;
    private readonly IOperadorThemeService? _themeService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly SemaphoreSlim _desplazamientoLock = new(1, 1);
    private DateTimeOffset _ultimoDesplazamientoUtc = DateTimeOffset.MinValue;

    public string? AccessToken { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? UsuarioId { get; private set; }
    public string? Nombre { get; private set; }
    public string? Email { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? Perfil { get; private set; }

    public bool EsOperarioODeposito =>
        string.Equals(Perfil, "Operario", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Perfil, "OperarioDeposito", StringComparison.OrdinalIgnoreCase) ||
        EsAdministrador;

    public bool EsAdministrador =>
        string.Equals(Perfil, "Administrador", StringComparison.OrdinalIgnoreCase);

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthService> logger,
        ILocalDatabaseService localDatabaseService,
        IOperadorThemeService? themeService = null,
        IServiceProvider? serviceProvider = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _localDatabaseService = localDatabaseService;
        _themeService = themeService;
        _serviceProvider = serviceProvider;
    }

    public Task<ResultadoLogin> IniciarSesionAsync(string email, string password, CancellationToken cancellationToken = default) =>
        LoginAsync(email, password, cancellationToken);

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

            // Parsear claims del JWT emitido
            var claims = ParsearClaimsJwt(loginResult.Token);

            // Validar perfil autorizado para operaciones en la app móvil
            var perfilIngresado = !string.IsNullOrWhiteSpace(loginResult.Perfil)
                ? loginResult.Perfil
                : ExtraerStringClaim(claims, "role", "perfil", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            if (!string.IsNullOrWhiteSpace(perfilIngresado) &&
                !string.Equals(perfilIngresado, "Repartidor", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(perfilIngresado, "Operario", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(perfilIngresado, "OperarioDeposito", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(perfilIngresado, "Despachador", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(perfilIngresado, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Acceso denegado a usuario con perfil '{Perfil}' en app móvil.", perfilIngresado);
                return new ResultadoLogin(false, $"Acceso denegado: esta aplicación es para personal operativo y repartidores. El perfil de su usuario es '{perfilIngresado}'.");
            }

            var token = loginResult.Token.Trim();
            var usuarioId = ExtraerGuidClaim(claims, "sub", "usuario_id") ?? loginResult.UsuarioId;
            var repartidorId = ExtraerGuidClaim(claims, "repartidor_id", "sub") ?? loginResult.UsuarioId;
            var nombre = ExtraerStringClaim(claims, "name") ?? loginResult.Nombre;
            var emailResuelto = ExtraerStringClaim(claims, "email") ?? loginResult.Email ?? email.Trim();
            var tenantId = ExtraerGuidClaim(claims, "tenant_id", "operador_id") ?? loginResult.OperadorId;
            var expiraUtc = ExtraerExpiracionUtc(claims) ?? loginResult.ExpiraUtc;
            var perfilResuelto = !string.IsNullOrWhiteSpace(perfilIngresado)
                ? perfilIngresado
                : ExtraerStringClaim(claims, "role", "perfil", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            // Guardar en memoria y en AuthDelegatingHandler para todas las llamadas HTTP subsiguientes
            AccessToken = token;
            UsuarioId = usuarioId;
            RepartidorId = repartidorId;
            Nombre = nombre;
            Email = emailResuelto;
            TenantId = tenantId;
            Perfil = perfilResuelto;
            AuthDelegatingHandler.CurrentTokenInMemory = token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Persistir de forma segura la sesión activa en SecureStorage del dispositivo y en Preferences para lectura síncrona
            await GuardarEnSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey, token);
            await GuardarEnSecureStorageAsync(AuthTokenActivoKey, token);
            await GuardarEnSecureStorageAsync(UsuarioIdKey, usuarioId.ToString());
            await GuardarEnSecureStorageAsync(RepartidorIdKey, repartidorId.ToString());
            await GuardarEnSecureStorageAsync(NombreKey, nombre);
            await GuardarEnSecureStorageAsync(EmailKey, emailResuelto);
            if (!string.IsNullOrWhiteSpace(perfilResuelto))
            {
                await GuardarEnSecureStorageAsync(PerfilKey, perfilResuelto);
                Preferences.Default.Set("RolUsuario", perfilResuelto);
            }
            if (tenantId.HasValue)
            {
                await GuardarEnSecureStorageAsync(TenantIdKey, tenantId.Value.ToString());
            }
            await GuardarEnSecureStorageAsync(ExpiraUtcKey, expiraUtc.ToUniversalTime().ToString("O"));

            using var sha = System.Security.Cryptography.SHA256.Create();
            var passwordHash = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            await GuardarEnSecureStorageAsync(PasswordHashKey, passwordHash);

            // Guardar siempre el vínculo biométrico en Android Keystore / SecureStorage tras un login válido
            await GuardarVinculoBiometricoAsync(
                token: token,
                email: emailResuelto,
                password: password,
                passwordHash: passwordHash,
                usuarioId: usuarioId,
                repartidorId: repartidorId,
                nombre: nombre,
                perfil: perfilResuelto,
                tenantId: tenantId,
                expiraUtc: expiraUtc);

            _logger.LogInformation("Sesión iniciada correctamente y vínculo biométrico actualizado para {Email} (Perfil: {Perfil}, Tenant: {TenantId}).", emailResuelto, perfilResuelto, tenantId);

            // Sincronizar dinámicamente la identidad visual del operador en segundo plano
            if (_themeService != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _themeService.DescargarYActualizarIdentidadAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo actualizar la identidad visual tras el inicio de sesión.");
                    }
                });
            }

            return new ResultadoLogin(
                Exitoso: true,
                Mensaje: "Inicio de sesión exitoso.",
                RepartidorId: repartidorId,
                Nombre: nombre,
                Email: emailResuelto,
                TenantId: tenantId,
                Perfil: perfilResuelto);
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

    private static async Task GuardarVinculoBiometricoAsync(
        string token,
        string? email,
        string? password,
        string? passwordHash,
        Guid? usuarioId,
        Guid? repartidorId,
        string? nombre,
        string? perfil,
        Guid? tenantId,
        DateTime expiraUtc)
    {
        await GuardarEnSecureStorageAsync(BiometriaHabilitadaKey, "true");
        await GuardarEnSecureStorageAsync(BiometriaTokenSesionKey, token);
        await GuardarEnSecureStorageAsync(BiometriaRefreshTokenKey, token);
        await GuardarEnSecureStorageAsync(BiometriaUsuarioEmailKey, email);
        await GuardarEnSecureStorageAsync(BiometriaNombreKey, nombre);
        await GuardarEnSecureStorageAsync(BiometriaPerfilKey, perfil);
        await GuardarEnSecureStorageAsync(BiometriaExpiraUtcKey, expiraUtc.ToUniversalTime().ToString("O"));

        if (usuarioId.HasValue && usuarioId.Value != Guid.Empty)
        {
            await GuardarEnSecureStorageAsync(BiometriaUsuarioIdKey, usuarioId.Value.ToString());
        }

        if (repartidorId.HasValue && repartidorId.Value != Guid.Empty)
        {
            await GuardarEnSecureStorageAsync(BiometriaRepartidorIdKey, repartidorId.Value.ToString());
        }

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            await GuardarEnSecureStorageAsync(BiometriaTenantIdKey, tenantId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(passwordHash))
        {
            await GuardarEnSecureStorageAsync(BiometriaPasswordHashKey, passwordHash);
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            await GuardarEnSecureStorageAsync(BiometriaCredencialKey, password);
        }
    }

    public async Task<bool> TieneSesionBiometricaGuardadaAsync()
    {
        try
        {
            var habilitada = await LeerDeSecureStorageAsync(BiometriaHabilitadaKey);
            var tokenBiometria = await LeerDeSecureStorageAsync(BiometriaTokenSesionKey);

            if (string.Equals(habilitada, "true", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(tokenBiometria))
            {
                return true;
            }

            // Migración transparente si ya existía una sesión activa previa en este dispositivo
            var tokenActivo = AccessToken ?? await LeerDeSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey);
            if (!string.IsNullOrWhiteSpace(tokenActivo))
            {
                var claims = ParsearClaimsJwt(tokenActivo);
                var expiraUtc = ExtraerExpiracionUtc(claims) ?? DateTime.UtcNow.AddDays(7);
                var email = Email ?? await LeerDeSecureStorageAsync(EmailKey) ?? ExtraerStringClaim(claims, "email");
                var nombre = Nombre ?? await LeerDeSecureStorageAsync(NombreKey) ?? ExtraerStringClaim(claims, "name");
                var perfil = Perfil ?? await LeerDeSecureStorageAsync(PerfilKey) ?? ExtraerStringClaim(claims, "role", "perfil", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
                var subGuid = ExtraerGuidClaim(claims, "sub", "usuario_id");
                var uIdStr = await LeerDeSecureStorageAsync(UsuarioIdKey);
                var rIdStr = await LeerDeSecureStorageAsync(RepartidorIdKey);
                var tIdStr = await LeerDeSecureStorageAsync(TenantIdKey);
                var pwdHash = await LeerDeSecureStorageAsync(PasswordHashKey);

                await GuardarVinculoBiometricoAsync(
                    token: tokenActivo,
                    email: email,
                    password: null,
                    passwordHash: pwdHash,
                    usuarioId: Guid.TryParse(uIdStr, out var uId) ? uId : subGuid,
                    repartidorId: Guid.TryParse(rIdStr, out var rId) ? rId : subGuid,
                    nombre: nombre,
                    perfil: perfil,
                    tenantId: Guid.TryParse(tIdStr, out var tId) ? tId : ExtraerGuidClaim(claims, "tenant_id", "operador_id"),
                    expiraUtc: expiraUtc);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar estado de sesión biométrica en SecureStorage.");
            return false;
        }
    }

    public async Task<string?> ObtenerEmailBiometricoGuardadoAsync()
    {
        return await LeerDeSecureStorageAsync(BiometriaUsuarioEmailKey);
    }

    public async Task<ResultadoLogin> RestaurarSesionBiometricaAsync(CancellationToken cancellationToken = default)
    {
        if (!await TieneSesionBiometricaGuardadaAsync())
        {
            return new ResultadoLogin(
                false,
                "No hay una sesión guardada en este dispositivo. Ingresa primero con tu correo electrónico y contraseña para vincular tu huella.");
        }

        var tokenGuardado = await LeerDeSecureStorageAsync(BiometriaTokenSesionKey);
        var emailGuardado = await LeerDeSecureStorageAsync(BiometriaUsuarioEmailKey);
        var credencialGuardada = await LeerDeSecureStorageAsync(BiometriaCredencialKey);

        if (string.IsNullOrWhiteSpace(tokenGuardado))
        {
            return new ResultadoLogin(
                false,
                "No hay una sesión guardada en este dispositivo. Ingresa primero con tu correo electrónico y contraseña para vincular tu huella.");
        }

        var claims = ParsearClaimsJwt(tokenGuardado);
        var expiraUtc = ExtraerExpiracionUtc(claims);
        if (!expiraUtc.HasValue)
        {
            var expStr = await LeerDeSecureStorageAsync(BiometriaExpiraUtcKey);
            if (DateTime.TryParse(expStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedExp))
            {
                expiraUtc = parsedExp.ToUniversalTime();
            }
        }

        var hayInternet = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        if (hayInternet)
        {
            var tokenPorExpirarOVencido = expiraUtc.HasValue && DateTime.UtcNow >= expiraUtc.Value.AddMinutes(-15);

            // Si el token está vencido/próximo a vencer y contamos con credencial en Keystore, renovamos el JWT contra la API
            if (tokenPorExpirarOVencido &&
                !string.IsNullOrWhiteSpace(emailGuardado) &&
                !string.IsNullOrWhiteSpace(credencialGuardada))
            {
                var renovacion = await IntentarRenovarTokenConServidorAsync(emailGuardado, credencialGuardada, cancellationToken);
                if (renovacion.NoAutorizadoPorServidor)
                {
                    await RevocarVinculoBiometricoAsync();
                    return new ResultadoLogin(
                        false,
                        "La sesión guardada expiró o las credenciales fueron modificadas. Ingresa con tu correo electrónico y contraseña para vincular nuevamente tu huella.");
                }

                if (renovacion.Resultado is { Exitoso: true })
                {
                    return renovacion.Resultado;
                }
            }
            else
            {
                // Validar el JWT guardado contra el servidor si hay red disponible
                var validacionServidor = await ValidarTokenGuardadoContraServidorAsync(
                    tokenGuardado,
                    omitirManejoDesplazamientoEnHandler: true,
                    cancellationToken);
                if (validacionServidor == EstadoValidacionTokenRemoto.NoAutorizado)
                {
                    await DeshabilitarBiometriaPorDesplazamientoAsync();
                    return new ResultadoLogin(
                        false,
                        "Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo. Ingresa con tu correo electrónico y contraseña para vincular nuevamente tu huella.");
                }
            }
        }

        // Restaurar sesión activa desde los datos biométricos guardados (soporta Modo Avión / Offline con SQLCipher)
        var subGuid = ExtraerGuidClaim(claims, "sub", "usuario_id");
        var usrStorage = await LeerDeSecureStorageAsync(BiometriaUsuarioIdKey);
        var repStorage = await LeerDeSecureStorageAsync(BiometriaRepartidorIdKey);
        var tenantStorage = await LeerDeSecureStorageAsync(BiometriaTenantIdKey);
        var pwdHash = await LeerDeSecureStorageAsync(BiometriaPasswordHashKey);

        var usuarioId = Guid.TryParse(usrStorage, out var uId) ? uId : subGuid;
        var repartidorId = Guid.TryParse(repStorage, out var rId) ? rId : subGuid;
        var nombre = await LeerDeSecureStorageAsync(BiometriaNombreKey) ?? ExtraerStringClaim(claims, "name");
        var emailResuelto = emailGuardado ?? ExtraerStringClaim(claims, "email");
        var tenantId = Guid.TryParse(tenantStorage, out var tId) ? tId : ExtraerGuidClaim(claims, "tenant_id", "operador_id");
        var perfilResuelto = await LeerDeSecureStorageAsync(BiometriaPerfilKey)
            ?? ExtraerStringClaim(claims, "role", "perfil", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
        var expiracionEfectiva = expiraUtc ?? DateTime.UtcNow.AddDays(7);

        AccessToken = tokenGuardado;
        UsuarioId = usuarioId;
        RepartidorId = repartidorId;
        Nombre = nombre;
        Email = emailResuelto;
        TenantId = tenantId;
        Perfil = perfilResuelto;
        AuthDelegatingHandler.CurrentTokenInMemory = tokenGuardado;

        await GuardarEnSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey, tokenGuardado);
        await GuardarEnSecureStorageAsync(AuthTokenActivoKey, tokenGuardado);
        if (usuarioId.HasValue)
        {
            await GuardarEnSecureStorageAsync(UsuarioIdKey, usuarioId.Value.ToString());
        }
        if (repartidorId.HasValue)
        {
            await GuardarEnSecureStorageAsync(RepartidorIdKey, repartidorId.Value.ToString());
        }
        await GuardarEnSecureStorageAsync(NombreKey, nombre);
        await GuardarEnSecureStorageAsync(EmailKey, emailResuelto);
        if (!string.IsNullOrWhiteSpace(perfilResuelto))
        {
            await GuardarEnSecureStorageAsync(PerfilKey, perfilResuelto);
            Preferences.Default.Set("RolUsuario", perfilResuelto);
        }
        if (tenantId.HasValue)
        {
            await GuardarEnSecureStorageAsync(TenantIdKey, tenantId.Value.ToString());
        }
        await GuardarEnSecureStorageAsync(ExpiraUtcKey, expiracionEfectiva.ToUniversalTime().ToString("O"));
        if (!string.IsNullOrWhiteSpace(pwdHash))
        {
            await GuardarEnSecureStorageAsync(PasswordHashKey, pwdHash);
        }

        if (_themeService != null && hayInternet)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _themeService.DescargarYActualizarIdentidadAsync();
                }
                catch
                {
                    // Ignorar fallo no crítico de branding en segundo plano
                }
            });
        }

        _logger.LogInformation("Sesión restaurada mediante biometría para {Email} (Perfil: {Perfil}).", emailResuelto, perfilResuelto);

        return new ResultadoLogin(
            Exitoso: true,
            Mensaje: "Sesión restaurada con biometría.",
            RepartidorId: repartidorId,
            Nombre: nombre,
            Email: emailResuelto,
            TenantId: tenantId,
            Perfil: perfilResuelto);
    }

    private enum EstadoValidacionTokenRemoto
    {
        Valido,
        NoAutorizado,
        IndeterminadoOffline
    }

    private async Task<EstadoValidacionTokenRemoto> ValidarTokenGuardadoContraServidorAsync(
        string token,
        bool omitirManejoDesplazamientoEnHandler,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(6));

            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

            using var response = await EnviarPingValidacionAsync(
                client,
                "/api/mobile/v1/ping",
                token,
                omitirManejoDesplazamientoEnHandler,
                cts.Token);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                using var fallbackResponse = await EnviarPingValidacionAsync(
                    client,
                    "/api/mobile/v1/operador/identidad-visual",
                    token,
                    omitirManejoDesplazamientoEnHandler,
                    cts.Token);

                return fallbackResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? EstadoValidacionTokenRemoto.NoAutorizado
                    : EstadoValidacionTokenRemoto.Valido;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return EstadoValidacionTokenRemoto.NoAutorizado;
            }

            return EstadoValidacionTokenRemoto.Valido;
        }
        catch
        {
            return EstadoValidacionTokenRemoto.IndeterminadoOffline;
        }
    }

    private static Task<HttpResponseMessage> EnviarPingValidacionAsync(
        HttpClient client,
        string ruta,
        string token,
        bool omitirManejoDesplazamientoEnHandler,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (omitirManejoDesplazamientoEnHandler)
        {
            request.Options.Set(AuthDelegatingHandler.OpcionValidacionBiometrica, true);
        }

        return client.SendAsync(request, cancellationToken);
    }

    private async Task<(ResultadoLogin? Resultado, bool NoAutorizadoPorServidor)> IntentarRenovarTokenConServidorAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));

            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);
            var payload = new LoginRequestDto(email.Trim(), password);

            var response = await client.PostAsJsonAsync("/api/v1/auth/login", payload, cts.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                response = await client.PostAsJsonAsync("/api/auth/login", payload, cts.Token);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (null, true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return (null, false);
            }

            var resultado = await LoginAsync(email, password, cts.Token);
            return (resultado, false);
        }
        catch
        {
            return (null, false);
        }
    }

    public Task RevocarVinculoBiometricoAsync()
    {
        EliminarDeSecureStorage(BiometriaHabilitadaKey);
        LimpiarClavesBiometricasEnSecureStorage();
        return Task.CompletedTask;
    }

    private async Task DeshabilitarBiometriaPorDesplazamientoAsync()
    {
        await GuardarEnSecureStorageAsync(BiometriaHabilitadaKey, "false");
        LimpiarClavesBiometricasEnSecureStorage();
    }

    private static void LimpiarClavesBiometricasEnSecureStorage()
    {
        EliminarDeSecureStorage(BiometriaUsuarioEmailKey);
        EliminarDeSecureStorage(BiometriaTokenSesionKey);
        EliminarDeSecureStorage(BiometriaRefreshTokenKey);
        EliminarDeSecureStorage(BiometriaUsuarioIdKey);
        EliminarDeSecureStorage(BiometriaRepartidorIdKey);
        EliminarDeSecureStorage(BiometriaNombreKey);
        EliminarDeSecureStorage(BiometriaPerfilKey);
        EliminarDeSecureStorage(BiometriaTenantIdKey);
        EliminarDeSecureStorage(BiometriaExpiraUtcKey);
        EliminarDeSecureStorage(BiometriaPasswordHashKey);
        EliminarDeSecureStorage(BiometriaCredencialKey);
    }

    public async Task<bool> ValidarSesionVigenteEnServidorAsync(CancellationToken cancellationToken = default)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            return true;

        if (!await EstaAutenticadoAsync() || string.IsNullOrWhiteSpace(AccessToken))
            return false;

        var estado = await ValidarTokenGuardadoContraServidorAsync(
            AccessToken,
            omitirManejoDesplazamientoEnHandler: false,
            cancellationToken);

        if (estado == EstadoValidacionTokenRemoto.NoAutorizado)
        {
            await ManejarSesionDesplazadaAsync();
            return false;
        }

        return true;
    }

    public async Task<bool> EstaAutenticadoAsync()
    {
        try
        {
            var token = AccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                token = await LeerDeSecureStorageAsync(AuthDelegatingHandler.AuthTokenKey)
                     ?? await LeerDeSecureStorageAsync(AuthTokenActivoKey);
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
                _logger.LogInformation("El token JWT activo ha expirado ({ExpiraUtc}). Limpiando sesión activa.", expiraUtc.Value);
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
            Perfil = await LeerDeSecureStorageAsync(PerfilKey) ?? ExtraerStringClaim(claims, "role", "perfil", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");

            if (!string.IsNullOrWhiteSpace(Perfil))
            {
                Preferences.Default.Set("RolUsuario", Perfil);
            }

            // Asegurar que el vínculo biométrico esté sincronizado si había una sesión activa válida
            var bioHabilitada = await LeerDeSecureStorageAsync(BiometriaHabilitadaKey);
            if (!string.Equals(bioHabilitada, "true", StringComparison.OrdinalIgnoreCase))
            {
                var pwdHash = await LeerDeSecureStorageAsync(PasswordHashKey);
                await GuardarVinculoBiometricoAsync(
                    token: token,
                    email: Email,
                    password: null,
                    passwordHash: pwdHash,
                    usuarioId: UsuarioId,
                    repartidorId: RepartidorId,
                    nombre: Nombre,
                    perfil: Perfil,
                    tenantId: TenantId,
                    expiraUtc: expiraUtc ?? DateTime.UtcNow.AddDays(7));
            }

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
        await GuardarEnSecureStorageAsync(BiometriaRepartidorIdKey, repartidorId.ToString());
    }

    public async Task CerrarSesionAsync()
    {
        await DetenerServiciosActivosDeRutaAsync();
        await LimpiarCredencialesAsync();

        // El siguiente repartidor no debe ver la hoja de ruta del anterior. Si quedan operaciones sin enviar se conserva todo
        // para no perderlas; se limpia apenas se sincronicen.
        try
        {
            if (await _localDatabaseService.ObtenerCantidadPendientesAsync() == 0)
            {
                await _localDatabaseService.LimpiarDatosDeRutaAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo limpiar la hoja de ruta local al cerrar sesión.");
        }

        if (Shell.Current is AppShell appShell)
        {
            appShell.RestablecerPestanas();
        }

        if (Shell.Current is not null)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("//LoginPage");
            });
        }
    }

    public async Task ManejarSesionDesplazadaAsync(string? motivo = null)
    {
        if (!await _desplazamientoLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var ahora = DateTimeOffset.UtcNow;
            if ((ahora - _ultimoDesplazamientoUtc) < TimeSpan.FromSeconds(8))
            {
                return;
            }

            _ultimoDesplazamientoUtc = ahora;
            _logger.LogWarning("Cerrando sesión local porque la cuenta inició sesión en otro dispositivo.");

            // 1. Detener inmediatamente el servicio de geolocalización en segundo plano y cerrar SignalR
            await DetenerServiciosActivosDeRutaAsync();

            // 2. Limpiar el token activo en memoria/sesión e invalidar el vínculo biométrico
            // desplazado (biometria_habilitada = "false" y tokens/credenciales eliminados), sin borrar SQLCipher
            await LimpiarCredencialesAsync();
            await DeshabilitarBiometriaPorDesplazamientoAsync();

            // 3. Navegar limpiamente en el hilo principal hacia LoginPage y mostrar alerta corporativa
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    if (Shell.Current is AppShell appShell)
                    {
                        appShell.RestablecerPestanas();
                    }

                    if (Shell.Current is not null)
                    {
                        await Shell.Current.GoToAsync("//LoginPage");
                        await Shell.Current.DisplayAlertAsync(
                            "Sesión Cerrada",
                            "Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo.",
                            "Aceptar");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No fue posible presentar el aviso de sesión desplazada en pantalla.");
                }
            });
        }
        finally
        {
            _desplazamientoLock.Release();
        }
    }

    private async Task DetenerServiciosActivosDeRutaAsync()
    {
        try
        {
            var trackingService = _serviceProvider?.GetService<IGeolocalizacionTrackingService>();
            if (trackingService is not null)
            {
                await trackingService.DetenerTrackingAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error no bloqueante al detener geolocalización tras cierre/desplazamiento de sesión.");
        }

        try
        {
            var realtimeService = _serviceProvider?.GetService<IRealtimeTrackingService>();
            if (realtimeService is not null)
            {
                await realtimeService.StopAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error no bloqueante al detener SignalR tras cierre/desplazamiento de sesión.");
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
        Perfil = null;
        AuthDelegatingHandler.CurrentTokenInMemory = null;
        Preferences.Default.Remove("RolUsuario");

        // Limpiar únicamente el token activo y datos de la sesión en curso.
        // Las claves de vinculación biométrica (biometria_*) se conservan intactas en SecureStorage.
        EliminarDeSecureStorage(AuthDelegatingHandler.AuthTokenKey);
        EliminarDeSecureStorage(AuthTokenActivoKey);
        EliminarDeSecureStorage(RepartidorIdKey);
        EliminarDeSecureStorage(UsuarioIdKey);
        EliminarDeSecureStorage(NombreKey);
        EliminarDeSecureStorage(EmailKey);
        EliminarDeSecureStorage(TenantIdKey);
        EliminarDeSecureStorage(PerfilKey);
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
