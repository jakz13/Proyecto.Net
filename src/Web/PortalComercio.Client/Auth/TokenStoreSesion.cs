using Microsoft.JSInterop;

namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Guarda el JWT real de la API en el sessionStorage del navegador: sobrevive a recargar la página
/// pero se pierde al cerrar la pestaña. Se usa cuando el modo demo está apagado (servidor).
/// </summary>
public class TokenStoreSesion : ITokenStore
{
    private const string Clave = "um_portal_token";
    private readonly IJSRuntime _js;
    private string? _token;
    private bool _leido;

    public TokenStoreSesion(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (!_leido)
        {
            try { _token = await _js.InvokeAsync<string?>("sessionStorage.getItem", Clave); }
            catch { _token = null; }
            _leido = true;
        }
        return _token;
    }

    public async Task GuardarTokenAsync(string token)
    {
        _token = token;
        _leido = true;
        try { await _js.InvokeVoidAsync("sessionStorage.setItem", Clave, token); } catch { }
    }

    public async Task EliminarTokenAsync()
    {
        _token = null;
        _leido = true;
        try { await _js.InvokeVoidAsync("sessionStorage.removeItem", Clave); } catch { }
    }
}
