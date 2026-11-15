using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice;

/// <summary>
/// La API aplica la política de sesión única activa: si la cuenta inició sesión en otro lugar, rechaza el token anterior con 401 y código SESION_DESPLAZADA.
/// Sin este middleware esa excepción llegaba hasta la página de error (HTTP 500) y el navegador desplazado quedaba con la cookie vieja y mensajes confusos.
/// Aquí se cierra la sesión local y se vuelve al inicio de sesión con el motivo; si la petición es de <c>fetch</c> (no una navegación) responde un 401 limpio.
/// </summary>
public sealed class SesionDesplazadaMiddleware
{
    public const string CodigoSesionDesplazada = "SESION_DESPLAZADA";
    public const string MotivoEnLaUrl = "sesion-desplazada";
    public const string MensajeParaLaPersona = "Tu sesión se cerró porque se inició sesión con esta cuenta en otro dispositivo. Ingresa de nuevo para continuar.";

    private readonly RequestDelegate _siguiente;

    public SesionDesplazadaMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _siguiente(contexto);
        }
        catch (ApiExcepcion ex) when (EsSesionDesplazada(ex) && !contexto.Response.HasStarted)
        {
            await contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (EsNavegacion(contexto.Request))
            {
                contexto.Response.Redirect($"/Login?motivo={MotivoEnLaUrl}");
                return;
            }

            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await contexto.Response.WriteAsJsonAsync(new { error = MensajeParaLaPersona, codigo = CodigoSesionDesplazada });
        }
    }

    public static bool EsSesionDesplazada(ApiExcepcion ex) =>
        ex.CodigoEstado == HttpStatusCode.Unauthorized && ex.Message.Contains(CodigoSesionDesplazada, StringComparison.Ordinal);

    /// <summary>Una navegación normal del navegador (o un cliente sin esa cabecera) se redirige; un fetch/XHR recibe el 401.</summary>
    private static bool EsNavegacion(HttpRequest solicitud)
    {
        var modo = solicitud.Headers["Sec-Fetch-Mode"].ToString();
        if (!string.IsNullOrEmpty(modo))
            return string.Equals(modo, "navigate", StringComparison.OrdinalIgnoreCase);

        return !solicitud.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(solicitud.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}
