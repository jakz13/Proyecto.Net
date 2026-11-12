using Microsoft.JSInterop;

namespace UltimaMilla.SeguimientoPublico.Servicios;

/// <summary>
/// Mantiene la conexión en vivo del seguimiento público: avisa a la página cuando el envío que se está mirando cambia de estado.
/// No requiere sesión y no recibe datos del envío, solo la señal para volver a consultar.
/// Si no hay conexión (modo mock, API caída) la página sigue funcionando con la consulta habitual.
/// </summary>
public sealed class TiempoRealSeguimientoServicio : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly string _urlHub;
    private DotNetObjectReference<TiempoRealSeguimientoServicio>? _referencia;
    private bool _iniciado;

    public TiempoRealSeguimientoServicio(IJSRuntime js, string urlBaseApi)
    {
        _js = js;
        _urlHub = $"{urlBaseApi.TrimEnd('/')}/hubs/seguimiento";
    }

    /// <summary>Se dispara cuando el envío seguido cambió (o se recuperó la conexión): hay que volver a consultar.</summary>
    public event Func<Task>? Actualizado;

    /// <summary>Empieza a seguir el código indicado. Devuelve <c>false</c> si no hay conexión en vivo; la página sigue siendo usable.</summary>
    public async Task<bool> SeguirAsync(string codigoSeguimiento)
    {
        try
        {
            if (!_iniciado)
            {
                _referencia = DotNetObjectReference.Create(this);
                _iniciado = await _js.InvokeAsync<bool>("TiempoRealSeguimiento.iniciar", _referencia, _urlHub);
                if (!_iniciado)
                    return false;
            }

            return await _js.InvokeAsync<bool>("TiempoRealSeguimiento.seguir", codigoSeguimiento);
        }
        catch
        {
            return false;
        }
    }

    [JSInvokable]
    public async Task AlActualizar()
    {
        var manejador = Actualizado;
        if (manejador is null)
            return;

        foreach (Func<Task> escucha in manejador.GetInvocationList())
        {
            try
            {
                await escucha();
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
            await _js.InvokeVoidAsync("TiempoRealSeguimiento.detener");
        }
        catch
        {
            // El navegador ya cerró la página
        }

        _referencia?.Dispose();
    }
}
