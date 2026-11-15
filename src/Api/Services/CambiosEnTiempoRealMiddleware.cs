using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Api.Services;

/// <summary>
/// Cualquier cambio de datos hecho por una sesión (alta, modificación o baja que la API aceptó) se avisa en tiempo real a las demás sesiones del mismo operador, para
/// que sus pantallas se pongan al día solas: el Backoffice y el portal del comercio se suscriben al evento <see cref="EventoDatosCambiaron"/> del hub de tablero.
/// Es genérico a propósito: no depende de qué entidad cambió ni de que cada caso de uso se acuerde de avisar, así no queda ninguna modificación sin reflejarse.
/// El aviso solo dice QUÉ ÁREA cambió (nunca datos): cada cliente vuelve a pedir su pantalla con sus propios permisos y filtros.
/// </summary>
public sealed class CambiosEnTiempoRealMiddleware
{
    public const string EventoDatosCambiaron = "DatosCambiaron";

    private readonly RequestDelegate _siguiente;
    private readonly ILogger<CambiosEnTiempoRealMiddleware> _logger;

    public CambiosEnTiempoRealMiddleware(RequestDelegate siguiente, ILogger<CambiosEnTiempoRealMiddleware> logger)
    {
        _siguiente = siguiente;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto, IHubContext<TableroHub> hub, ICurrentTenant tenant)
    {
        await _siguiente(contexto);

        if (!EsCambioDeDatos(contexto.Request.Method, contexto.Request.Path, contexto.Response.StatusCode))
            return;

        // Sin operador resuelto (petición anónima o de un proceso sin inquilino) no hay a quién avisar
        var operadorId = tenant.OperadorId;
        if (operadorId == Guid.Empty)
            return;

        try
        {
            var aviso = new { area = AreaDe(contexto.Request.Path), accion = contexto.Request.Method, timestamp = DateTime.UtcNow };

            // El personal del operador ve todo lo de su operador; un comercio solo lo suyo
            await hub.Clients.Group(TableroHub.GrupoOperador(operadorId)).SendAsync(EventoDatosCambiaron, aviso, CancellationToken.None);
            if (tenant.ComercioOperadorId is { } relacion && relacion != Guid.Empty)
                await hub.Clients.Group(TableroHub.GrupoComercio(relacion)).SendAsync(EventoDatosCambiaron, aviso, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Un fallo del transporte nunca debe presentar como fallida una operación que ya se guardó
            _logger.LogDebug(ex, "No se pudo emitir el aviso de datos cambiados en tiempo real.");
        }
    }

    /// <summary>Una escritura aceptada por la API. Quedan fuera lo que no cambia datos que otras sesiones muestren: inicio de sesión, telemetría de posición y los hubs.</summary>
    public static bool EsCambioDeDatos(string metodo, PathString ruta, int estado)
    {
        if (estado is < 200 or >= 300)
            return false;

        if (!(HttpMethods.IsPost(metodo) || HttpMethods.IsPut(metodo) || HttpMethods.IsPatch(metodo) || HttpMethods.IsDelete(metodo)))
            return false;

        if (!ruta.StartsWithSegments("/api"))
            return false;

        return !(ruta.StartsWithSegments("/api/v1/auth")
              || ruta.StartsWithSegments("/api/auth")
              || ruta.StartsWithSegments("/api/testing")
              || ruta.StartsWithSegments("/api/mobile/v1/posiciones"));
    }

    /// <summary>"/api/v1/flota/vehiculos" → "flota"; "/api/envios/123" → "envios".</summary>
    public static string AreaDe(PathString ruta)
    {
        var segmentos = (ruta.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var indice = 1;   // después de "api"
        while (indice < segmentos.Length - 1 && segmentos[indice].Length > 1 && segmentos[indice][0] == 'v' && segmentos[indice].Skip(1).All(char.IsDigit))
            indice++;
        return indice < segmentos.Length ? segmentos[indice].ToLowerInvariant() : "general";
    }
}
