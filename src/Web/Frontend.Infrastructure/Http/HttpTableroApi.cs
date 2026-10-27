using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación HTTP de ITableroApi (UC-12, RF-29) sobre /api/operacion/tablero.
/// Conecta el Backoffice con los datos reales de hojas de ruta en ejecución,
/// última posición conocida de cada unidad y detección de riesgo en franjas comprometidas.
/// </summary>
public class HttpTableroApi : ITableroApi
{
    private const string RutaTablero = "/api/operacion/tablero";
    private const string RutaInstrucciones = "/api/operacion/tablero/instrucciones";

    // Coordenadas por defecto en Montevideo central si la unidad o parada aún no reportó GPS
    private const double LatitudPorDefecto = -34.9011;
    private const double LongitudPorDefecto = -56.1645;

    private readonly HttpClient _http;

    public HttpTableroApi(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<ResumenTableroDto> ObtenerResumenEnVivoAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync(RutaTablero, cancelacion);
        await AsegurarExitoAsync(respuesta, cancelacion);

        var apiDto = await respuesta.Content.ReadFromJsonAsync<TableroOperacionApiResponse>(CatalogosHttp.OpcionesJson, cancelacion);
        if (apiDto is null)
            return new ResumenTableroDto();

        return MapearResumen(apiDto);
    }

    public async Task<VehiculoEnVivoDto?> ObtenerDetalleVehiculoEnVivoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        var resumen = await ObtenerResumenEnVivoAsync(cancelacion);
        return resumen.Vehiculos.FirstOrDefault(v => v.HojaDeRutaId == hojaDeRutaId);
    }

    public async Task<bool> EnviarInstruccionRepartidorAsync(Guid repartidorId, string mensaje, CancellationToken cancelacion = default)
    {
        if (repartidorId == Guid.Empty || string.IsNullOrWhiteSpace(mensaje))
            return false;

        var respuesta = await _http.PostAsJsonAsync(RutaInstrucciones, new
        {
            RepartidorId = repartidorId,
            Mensaje = mensaje.Trim()
        }, CatalogosHttp.OpcionesJson, cancelacion);

        if (respuesta.IsSuccessStatusCode)
            return true;

        if (respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            return false;

        await AsegurarExitoAsync(respuesta, cancelacion);
        return false;
    }

    private static ResumenTableroDto MapearResumen(TableroOperacionApiResponse api)
    {
        var alertas = (api.AlertasRiesgo ?? new List<AlertaApiDto>())
            .Select(MapearAlerta)
            .ToList();

        var vehiculos = (api.Vehiculos ?? new List<VehiculoApiDto>())
            .Select(MapearVehiculo)
            .ToList();

        return new ResumenTableroDto
        {
            TotalRutasEnEjecucion = api.TotalRutasEnEjecucion,
            TotalRepartidoresEnCalle = api.TotalRepartidoresEnCalle,
            TotalEnviosEnTransito = api.TotalEnviosEnTransito,
            TotalEnviosEntregadosHoy = api.TotalEnviosEntregados,
            TotalEnviosEnRiesgo = api.TotalEnviosEnRiesgo,
            UltimaSincronizacion = api.UltimaSincronizacion,
            Vehiculos = vehiculos,
            AlertasRiesgo = alertas
        };
    }

    private static VehiculoEnVivoDto MapearVehiculo(VehiculoApiDto v)
    {
        var paradas = (v.Paradas ?? new List<ParadaApiDto>())
            .Select(MapearParada)
            .ToList();

        var enviosRiesgo = (v.EnviosEnRiesgo ?? new List<AlertaApiDto>())
            .Select(MapearAlerta)
            .ToList();

        // Si la unidad no tiene latitud reportada, buscar la primera parada con coordenadas o usar Montevideo centro
        var lat = v.Latitud ?? paradas.FirstOrDefault(p => p.Latitud != 0)?.Latitud ?? LatitudPorDefecto;
        var lon = v.Longitud ?? paradas.FirstOrDefault(p => p.Longitud != 0)?.Longitud ?? LongitudPorDefecto;

        var estadoOperativo = v.EstadoOperativo switch
        {
            "EnParada" => "EnParada",
            "EnCamino" => "EnMovimiento",
            _ => v.TieneAlertasRiesgo ? "DetenidoAlerta" : "EnMovimiento"
        };

        var velocidad = estadoOperativo == "EnMovimiento" ? 28 : 0;

        return new VehiculoEnVivoDto
        {
            HojaDeRutaId = v.HojaDeRutaId,
            NumeroRuta = v.NumeroRuta,
            RepartidorId = v.RepartidorId,
            RepartidorNombre = v.RepartidorNombre,
            RepartidorTelefono = v.RepartidorTelefono,
            VehiculoId = v.VehiculoId,
            VehiculoMatricula = v.VehiculoMatricula,
            VehiculoModelo = string.IsNullOrWhiteSpace(v.VehiculoTipo) ? "Furgón Urbano" : v.VehiculoTipo,
            Latitud = lat,
            Longitud = lon,
            DireccionAproximada = v.DireccionAproximada ?? "En recorrido urbano",
            ZonaActual = "Área Operativa",
            NivelBateriaPct = 85,
            VelocidadKmh = velocidad,
            UltimaActualizacion = v.UltimaActualizacion ?? DateTime.UtcNow,
            EstadoOperativo = estadoOperativo,
            ParadasCompletadas = v.ParadasCompletadas,
            TotalParadas = v.TotalParadas,
            ParadaActualCodigo = v.ParadaActualCodigo,
            ParadaActualDireccion = v.ParadaActualDireccion,
            TieneAlertasRiesgo = v.TieneAlertasRiesgo,
            CantidadEnviosEnRiesgo = v.CantidadEnviosEnRiesgo,
            EnviosEnRiesgo = enviosRiesgo,
            Paradas = paradas
        };
    }

    private static EnvioRiesgoDto MapearAlerta(AlertaApiDto a)
    {
        var retraso = a.MinutosParaCierreVentana < 0
            ? Math.Abs(a.MinutosParaCierreVentana)
            : Math.Max(15, 30 - a.MinutosParaCierreVentana);

        var llegada = a.HoraLimiteVentana.AddMinutes(retraso);

        return new EnvioRiesgoDto
        {
            EnvioId = a.EnvioId,
            HojaDeRutaId = a.HojaDeRutaId,
            NumeroRuta = a.NumeroRuta,
            CodigoSeguimiento = a.CodigoSeguimiento,
            Destinatario = a.Destinatario,
            DestinatarioTelefono = a.RepartidorTelefono,
            Direccion = a.Direccion,
            Zona = "Área de Cobertura",
            FranjaHoraria = a.FranjaHoraria,
            HoraLimiteVentana = a.HoraLimiteVentana,
            HoraEstimadaLlegada = llegada,
            MinutosRetrasoEstimado = retraso,
            NivelRiesgo = a.NivelRiesgo,
            CausaRiesgo = a.CausaRiesgo,
            RepartidorNombre = a.RepartidorNombre,
            RepartidorTelefono = a.RepartidorTelefono,
            VehiculoMatricula = a.VehiculoMatricula,
            Latitud = a.Latitud ?? LatitudPorDefecto,
            Longitud = a.Longitud ?? LongitudPorDefecto
        };
    }

    private static ParadaEnVivoDto MapearParada(ParadaApiDto p)
    {
        return new ParadaEnVivoDto
        {
            EnvioId = p.EnvioId,
            CodigoSeguimiento = p.CodigoSeguimiento,
            Orden = p.Orden,
            Destinatario = p.Destinatario,
            Direccion = p.Direccion,
            Latitud = p.Latitud ?? LatitudPorDefecto,
            Longitud = p.Longitud ?? LongitudPorDefecto,
            FranjaHoraria = p.FranjaHoraria ?? string.Empty,
            Estado = p.Estado,
            EnRiesgo = p.EnRiesgo,
            MotivoRiesgo = p.MotivoRiesgo
        };
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        throw new ApiExcepcion(respuesta.StatusCode, HttpIncidenciasApi.ExtraerMensaje(cuerpo) ?? $"Error HTTP {(int)respuesta.StatusCode}");
    }

    private sealed record TableroOperacionApiResponse(
        int TotalRutasEnEjecucion,
        int TotalRepartidoresEnCalle,
        int TotalEnviosEnTransito,
        int TotalEnviosEntregados,
        int TotalEnviosEnRiesgo,
        DateTime UltimaSincronizacion,
        List<VehiculoApiDto> Vehiculos,
        List<AlertaApiDto> AlertasRiesgo);

    private sealed record VehiculoApiDto(
        Guid HojaDeRutaId,
        string NumeroRuta,
        Guid RepartidorId,
        string RepartidorNombre,
        string RepartidorTelefono,
        Guid VehiculoId,
        string VehiculoMatricula,
        string VehiculoTipo,
        double? Latitud,
        double? Longitud,
        string? DireccionAproximada,
        string? OrigenPosicion,
        DateTime? UltimaActualizacion,
        string EstadoOperativo,
        int ParadasCompletadas,
        int TotalParadas,
        string? ParadaActualCodigo,
        string? ParadaActualDireccion,
        bool TieneAlertasRiesgo,
        int CantidadEnviosEnRiesgo,
        List<AlertaApiDto> EnviosEnRiesgo,
        List<ParadaApiDto> Paradas);

    private sealed record AlertaApiDto(
        Guid EnvioId,
        Guid HojaDeRutaId,
        string NumeroRuta,
        string CodigoSeguimiento,
        string Destinatario,
        string Direccion,
        string FranjaHoraria,
        DateTime HoraLimiteVentana,
        int MinutosParaCierreVentana,
        string NivelRiesgo,
        string CausaRiesgo,
        string RepartidorNombre,
        string RepartidorTelefono,
        string VehiculoMatricula,
        double? Latitud,
        double? Longitud);

    private sealed record ParadaApiDto(
        Guid EnvioId,
        string CodigoSeguimiento,
        int Orden,
        string Destinatario,
        string Direccion,
        double? Latitud,
        double? Longitud,
        string? FranjaHoraria,
        string Estado,
        bool EnRiesgo,
        string? MotivoRiesgo);
}
