using Microsoft.JSInterop;

namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Almacenamiento en memoria y sessionStorage para tokens JWT en Blazor WASM.
/// Si el usuario inició sesión o cambió de operador, conserva el token real en sessionStorage ante recargas (forceLoad);
/// de lo contrario provee el token de demostración sincronizado con el comercio sembrado (MegaStore Uruguay S.R.L.).
/// </summary>
public class TokenStoreEnMemoria : ITokenStore
{
    private const string ClaveSesion = "um_portal_token";

    // JWT demo codificado en Base64Url sincronizado con DatabaseSeeder:
    // Header: {"alg":"HS256","typ":"JWT"}
    // Payload: {"sub":"70000000-0000-0000-0000-000000000005","name":"MegaStore Uruguay S.R.L.","email":"comercio@gmail.com","role":"Comercio","operador_id":"11111111-1111-1111-1111-111111111111","comercio_id":"33333333-3333-3333-3333-333333333333"}
    private const string TokenPorDefecto = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
                                            "eyJzdWIiOiI3MDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDUiLCJuYW1lIjoiTWVnYVN0b3JlIFVydWd1YXkgUy5SLkwuIiwiZW1haWwiOiJjb21lcmNpb0BnbWFpbC5jb20iLCJyb2xlIjoiQ29tZXJjaW8iLCJvcGVyYWRvcl9pZCI6IjExMTExMTExLTExMTEtMTExMS0xMTExLTExMTExMTExMTExMSIsImNvbWVyY2lvX2lkIjoiMzMzMzMzMzMtMzMzMy0zMzMzLTMzMzMtMzMzMzMzMzMzMzMzIn0." +
                                            "demo-firma-jwt";

    private readonly IJSRuntime? _js;
    private string? _tokenActual = TokenPorDefecto;
    private bool _leidoDeStorage;

    public TokenStoreEnMemoria(IJSRuntime? js = null)
    {
        _js = js;
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (!_leidoDeStorage && _js is not null)
        {
            _leidoDeStorage = true;
            try
            {
                var guardado = await _js.InvokeAsync<string?>("sessionStorage.getItem", ClaveSesion);
                if (!string.IsNullOrWhiteSpace(guardado))
                {
                    _tokenActual = guardado;
                }
            }
            catch
            {
                // Ignorar si JSInterop aún no está disponible
            }
        }

        return _tokenActual;
    }

    public async Task GuardarTokenAsync(string token)
    {
        _tokenActual = token;
        _leidoDeStorage = true;
        if (_js is not null)
        {
            try
            {
                await _js.InvokeVoidAsync("sessionStorage.setItem", ClaveSesion, token);
            }
            catch
            {
                // Ignorar si JSInterop no está disponible en pruebas
            }
        }
    }

    public async Task EliminarTokenAsync()
    {
        _tokenActual = null;
        _leidoDeStorage = true;
        if (_js is not null)
        {
            try
            {
                await _js.InvokeVoidAsync("sessionStorage.removeItem", ClaveSesion);
            }
            catch
            {
                // Ignorar si JSInterop no está disponible en pruebas
            }
        }
    }
}
