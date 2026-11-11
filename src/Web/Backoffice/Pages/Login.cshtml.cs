using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Backoffice.Seguridad;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Backoffice.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private static readonly string[] PerfilesBackoffice = { "Administrador", "Despachador", "Operario" };

    private readonly MockContextoOperador _contextoOperador;
    private readonly IConfiguration _configuracion;
    private readonly IHostEnvironment _entorno;
    private readonly IHttpClientFactory? _httpFactory;

    public LoginModel(MockContextoOperador contextoOperador, IConfiguration configuracion, IHostEnvironment entorno, IHttpClientFactory? httpFactory = null)
    {
        _contextoOperador = contextoOperador;
        _configuracion = configuracion;
        _entorno = entorno;
        _httpFactory = httpFactory;
    }

    /// <summary>
    /// Con el modo demo activo (desarrollo local, por defecto) se elige el rol sin contraseña.
    /// En el servidor se apaga con <c>Auth__PermitirModoDemo=false</c> y el acceso pasa por el login real de la API.
    /// </summary>
    public bool ModoDemo => ModoDemoActivo(_configuracion, _entorno);

    [BindProperty]
    public string RolSeleccionado { get; set; } = "Administrador";

    [BindProperty]
    public string NombreUsuario { get; set; } = "Felipe Bernardi";

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? Error { get; set; }

    public string? ReturnUrl { get; set; }

    /// <summary>Seguro por defecto: sin la variable definida, el modo demo solo existe en el entorno Development.</summary>
    public static bool ModoDemoActivo(IConfiguration configuracion, IHostEnvironment entorno) =>
        configuracion.GetValue<bool?>("Auth:PermitirModoDemo") ?? entorno.IsDevelopment();

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (ModoDemo)
        {
            // Demo/desarrollo local: se autentica con el rol elegido emitiendo la Cookie con Claims
            var idOperadorGuid = ResolverOperadorGuid(_contextoOperador.OperadorActivo.OperadorId).ToString();
            var claimsDemo = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new(ClaimTypes.Name, NombreUsuario),
                new(ClaimTypes.Role, RolSeleccionado),
                new("operador_id", idOperadorGuid),
                new("tenant_id", idOperadorGuid),
                new("codigo_operador", _contextoOperador.OperadorActivo.OperadorId)
            };

            await IniciarSesionConCookieAsync(claimsDemo);
            return RedirigirTrasLogin(returnUrl, RolSeleccionado);
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Error = "Ingrese el correo y la contraseña.";
            return Page();
        }

        var resultado = await AutenticarContraApiAsync(Email.Trim(), Password);
        if (resultado is null)
        {
            return Page();
        }

        if (!PerfilesBackoffice.Contains(resultado.Perfil, StringComparer.OrdinalIgnoreCase))
        {
            Error = "Su perfil no tiene acceso al backoffice del operador.";
            return Page();
        }

        var operador = resultado.OperadorId?.ToString() ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, resultado.UsuarioId.ToString()),
            new(ClaimTypes.Name, resultado.Nombre),
            new(ClaimTypes.Role, resultado.Perfil),
            new("operador_id", operador),
            new("tenant_id", operador),
            // El JWT de la API viaja en la sesión y lo reenvía TokenApiHandler en cada llamada
            new(TokenApiHandler.ClaimToken, resultado.Token)
        };

        await IniciarSesionConCookieAsync(claims);
        return RedirigirTrasLogin(returnUrl, resultado.Perfil);
    }

    private async Task<RespuestaLogin?> AutenticarContraApiAsync(string email, string password)
    {
        var baseUrl = _configuracion["ApiSettings:BaseUrl"] ?? "http://localhost:5000";

        try
        {
            using var cliente = _httpFactory?.CreateClient() ?? new HttpClient();
            using var respuesta = await cliente
                .PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/api/v1/auth/login", new { Email = email, Password = password });

            if (respuesta.IsSuccessStatusCode)
            {
                var resultado = await respuesta.Content.ReadFromJsonAsync<RespuestaLogin>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (resultado is not null && !string.IsNullOrWhiteSpace(resultado.Token))
                {
                    return resultado;
                }
            }

            Error = respuesta.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Correo o contraseña incorrectos.",
                HttpStatusCode.Locked => "La cuenta está bloqueada temporalmente por intentos fallidos. Intente más tarde.",
                HttpStatusCode.Forbidden => "La cuenta está inactiva.",
                _ => "No se pudo iniciar sesión. Intente nuevamente."
            };
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo conectar con el servicio de autenticación.";
        }
        catch (TaskCanceledException)
        {
            Error = "El servicio de autenticación no respondió a tiempo.";
        }

        return null;
    }

    private async Task IniciarSesionConCookieAsync(IEnumerable<Claim> claims)
    {
        var identidad = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var propiedades = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidad), propiedades);
    }

    private IActionResult RedirigirTrasLogin(string? returnUrl, string perfil)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return perfil switch
        {
            "Operario" => RedirectToPage("/Despacho/Index"),
            "Despachador" => RedirectToPage("/Rutas/Index"),
            _ => RedirectToPage("/Index")
        };
    }

    internal static Guid ResolverOperadorGuid(string? codigoOperador)
    {
        if (!string.IsNullOrWhiteSpace(codigoOperador) && Guid.TryParse(codigoOperador, out var guid) && guid != Guid.Empty)
            return guid;

        return string.Equals(codigoOperador, "OP-02", StringComparison.OrdinalIgnoreCase)
            ? DatabaseSeederSegundoOperador.OperadorId
            : DatabaseSeeder.DefaultOperadorId;
    }

    private sealed record RespuestaLogin(string Token, Guid UsuarioId, string Nombre, string Perfil, Guid? OperadorId);
}
