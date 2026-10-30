using Microsoft.JSInterop;
using UltimaMilla.PortalComercio.Client.Auth;

namespace UltimaMilla.PortalComercio.Client.Servicios;

/// <summary>
/// Mantiene la conexión en vivo con la API para que el listado y el tablero del comercio reflejen los cambios de estado
/// de sus envíos sin recargar. Una sola conexión por sesión del navegador, compartida por todas las páginas.
/// Si no hay conexión (modo mock, API caída) las páginas siguen funcionando con su carga habitual.
/// </summary>
public sealed class TiempoRealEnviosServicio : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ITokenStore _tokenStore;
    private readonly string _urlHub;
    private DotNetObjectReference<TiempoRealEnviosServicio>? _referencia;
    private bool _iniciado;

    public TiempoRealEnviosServicio(IJSRuntime js, ITokenStore tokenStore, string urlBaseApi)
    {
        _js = js;
        _tokenStore = tokenStore;
        _urlHub = $"{urlBaseApi.TrimEnd('/')}/hubs/tablero";
    }

    /// <summary>Se dispara con el código de seguimiento y el estado nuevo; ambos vacíos indican "refrescá todo" (tras reconectar).</summary>
    public event Func<string, string, Task>? EnvioCambioEstado;

    public async Task IniciarAsync()
    {
        if (_iniciado)
            return;
        _iniciado = true;

        try
        {
            _referencia = DotNetObjectReference.Create(this);
            await _js.InvokeAsync<bool>("TiempoRealEnviosPortal.iniciar", _referencia, _urlHub);
        }
        catch
        {
            // Sin tiempo real la página sigue siendo usable; se vuelve a intentar en la próxima navegación
            _iniciado = false;
        }
    }

    [JSInvokable]
    public async Task<string> ObtenerToken() => await _tokenStore.ObtenerTokenAsync() ?? string.Empty;

    [JSInvokable]
    public async Task AlCambiarEstado(string codigoSeguimiento, string estado)
    {
        var manejador = EnvioCambioEstado;
        if (manejador is null)
            return;

        foreach (Func<string, string, Task> escucha in manejador.GetInvocationList())
        {
            try
            {
                await escucha(codigoSeguimiento, estado);
            }
            catch
            {
                // Una página defectuosa no debe impedir que las demás se actualicen
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("TiempoRealEnviosPortal.detener");
        }
        catch
        {
            // El navegador ya cerró el circuito
        }

        _referencia?.Dispose();
    }
}
