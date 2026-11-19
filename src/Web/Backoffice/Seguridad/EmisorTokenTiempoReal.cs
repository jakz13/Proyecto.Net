using System.Security.Claims;
using System.Text;
using System.Text.Json;
using UltimaMilla.Backoffice.Pages;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Emite el token de corta duración con el que el navegador del backoffice se autentica ante los hubs de SignalR de la API.
/// El operador sale de la sesión verificada del servidor, nunca de datos enviados por el navegador (ADR-002).
/// </summary>
public static class EmisorTokenTiempoReal
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);

    public static object Emitir(ClaimsPrincipal usuarioSesion, IConfiguration? configuracion, MockContextoOperador? contextoOperador, string rutaHub)
    {
        var operadorId = ResolverOperador(usuarioSesion, contextoOperador);
        var usuarioIdTexto = usuarioSesion.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? ExtraerClaimDeJwt(usuarioSesion.FindFirst(TokenApiHandler.ClaimToken)?.Value, ClaimTypes.NameIdentifier, "sub", "nameid");
        var usuarioId = Guid.TryParse(usuarioIdTexto, out var uid) && uid != Guid.Empty ? uid : Guid.NewGuid();
        var nombre = usuarioSesion.Identity?.Name ?? "Usuario Backoffice";
        var rol = usuarioSesion.FindFirst(ClaimTypes.Role)?.Value ?? "Despachador";
        var perfilId = string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase)
            ? DatabaseSeeder.DefaultPerfilAdminId
            : DatabaseSeeder.DefaultPerfilDespachadorId;

        var sessionId = ResolverSessionId(usuarioSesion);
        var usuario = new Usuario(usuarioId, nombre, "tablero@ultimamilla.com.uy", "sesion-backoffice", perfilId, operadorId: operadorId);
        var perfil = new Perfil(perfilId, rol, "Sesión autenticada de Backoffice");
        var servicioJwt = new TokenJwtService(configuracion ?? new ConfigurationBuilder().Build());
        var token = servicioJwt.GenerarToken(usuario, perfil, new[] { "RUTAS_DESPACHO" }, Vigencia, sessionId: sessionId);

        var baseUrl = UrlPublicaDeLaApi(configuracion);
        return new
        {
            accessToken = token.Token,
            expiraEnUtc = token.ExpiraUtc,
            hubUrl = $"{baseUrl.TrimEnd('/')}{rutaHub}"
        };
    }

    /// <summary>
    /// Recupera el identificador de sesión única (<c>sid</c>) de la cookie del backoffice o del JWT de la API almacenado en ella,
    /// para que el token de SignalR conserve la misma sesión registrada en Redis al iniciar sesión.
    /// </summary>
    public static string? ResolverSessionId(ClaimsPrincipal? usuarioSesion)
    {
        if (usuarioSesion is null)
            return null;

        var sidDirecto = usuarioSesion.FindFirst("sid")?.Value
                      ?? usuarioSesion.FindFirst("jti")?.Value;
        if (!string.IsNullOrWhiteSpace(sidDirecto))
            return sidDirecto;

        var tokenApi = usuarioSesion.FindFirst(TokenApiHandler.ClaimToken)?.Value;
        return ExtraerSessionIdDeJwt(tokenApi);
    }

    public static string? ExtraerSessionIdDeJwt(string? tokenJwt) =>
        ExtraerClaimDeJwt(tokenJwt, "sid", "jti");

    private static string? ExtraerClaimDeJwt(string? tokenJwt, params string[] tiposClaim)
    {
        if (string.IsNullOrWhiteSpace(tokenJwt) || tiposClaim is null || tiposClaim.Length == 0)
            return null;

        try
        {
            var partes = tokenJwt.Trim().Split('.');
            if (partes.Length != 3)
                return null;

            var base64 = partes[1].Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            using var doc = JsonDocument.Parse(payloadJson);
            var raiz = doc.RootElement;
            foreach (var tipo in tiposClaim)
            {
                if (raiz.TryGetProperty(tipo, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    var valor = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(valor))
                        return valor;
                }
            }
        }
        catch
        {
            // Si el token no tiene formato JWT estándar, se ignora sin interrumpir la emisión
        }

        return null;
    }

    /// <summary>
    /// Dirección de la API que el NAVEGADOR puede alcanzar. Puede diferir de <c>ApiSettings:BaseUrl</c> (la que usa el servidor del backoffice):
    /// en Docker, por ejemplo, el servidor llega a la API como "http://api:8080" pero el navegador solo la ve en "http://localhost:5000".
    /// Sin configuración específica se usa la misma que el servidor.
    /// </summary>
    public static string UrlPublicaDeLaApi(IConfiguration? configuracion)
    {
        var publica = configuracion?["ApiSettings:BaseUrlPublica"] ?? configuracion?["ApiSettings:PublicUrl"];
        if (!string.IsNullOrWhiteSpace(publica))
            return publica;

        var baseUrl = configuracion?["ApiSettings:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.Contains("//api:", StringComparison.OrdinalIgnoreCase))
            return baseUrl;

        return "http://localhost:5000";
    }

    private static Guid ResolverOperador(ClaimsPrincipal usuarioSesion, MockContextoOperador? contextoOperador)
    {
        var claimTenant = usuarioSesion.FindFirst("tenant_id")?.Value ?? usuarioSesion.FindFirst("operador_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimTenant))
            return LoginModel.ResolverOperadorGuid(claimTenant);

        return LoginModel.ResolverOperadorGuid(contextoOperador?.OperadorActivo.OperadorId);
    }
}
