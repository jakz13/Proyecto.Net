using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación HTTP de IIncidenciasApi (UC-11) sobre /api/backoffice/incidencias.
/// La API identifica envíos, rutas y usuarios solo por id: el cliente completa los nombres consultando sus listados.
/// </summary>
public class HttpIncidenciasApi : IIncidenciasApi
{
    private const string RutaIncidencias = "/api/backoffice/incidencias";
    private const int TamanoPagina = 100;
    private const int MaximoPaginas = 5;

    // El backend llama "EnSeguimiento" al estado que la pantalla muestra como "EnInvestigacion"
    private const string EstadoApiEnSeguimiento = "EnSeguimiento";
    private const string EstadoPantallaEnInvestigacion = "EnInvestigacion";

    private readonly HttpClient _http;

    public HttpIncidenciasApi(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<IncidenciaDto>> ListarIncidenciasAsync(FiltroIncidenciasDto? filtro = null, CancellationToken cancelacion = default)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro?.Estado) && filtro.Estado != "Todos")
            parametros.Add($"estado={Uri.EscapeDataString(EstadoParaApi(filtro.Estado))}");
        if (!string.IsNullOrWhiteSpace(filtro?.Tipo))
            parametros.Add($"tipo={Uri.EscapeDataString(filtro.Tipo)}");

        var resumenes = new List<ResumenApi>();
        for (var pagina = 1; pagina <= MaximoPaginas; pagina++)
        {
            var consulta = string.Join("&", parametros.Append($"pagina={pagina}").Append($"tamanoPagina={TamanoPagina}"));
            var respuesta = await _http.GetAsync($"{RutaIncidencias}?{consulta}", cancelacion);
            await AsegurarExitoAsync(respuesta, cancelacion);

            var datos = await respuesta.Content.ReadFromJsonAsync<PaginaApi>(CatalogosHttp.OpcionesJson, cancelacion);
            if (datos is null)
                break;

            resumenes.AddRange(datos.Items);
            if (resumenes.Count >= datos.Total || datos.Items.Count == 0)
                break;
        }

        var contexto = await ContextoAsync(cancelacion);
        var incidencias = resumenes.Select(r => Mapear(r, null, contexto)).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro?.Severidad))
            incidencias = incidencias.Where(i => i.Severidad.Equals(filtro.Severidad, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(filtro?.Busqueda))
        {
            var termino = filtro.Busqueda.Trim();
            incidencias = incidencias.Where(i =>
                Contiene(i.NumeroIncidencia, termino) || Contiene(i.Descripcion, termino) || Contiene(i.CodigoSeguimientoEnvio, termino) ||
                Contiene(i.NumeroRuta, termino) || Contiene(i.DestinatarioEnvio, termino) || Contiene(i.RepartidorNombre, termino));
        }

        if (DateTime.TryParse(filtro?.Fecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            incidencias = incidencias.Where(i => i.FechaRegistro.Date == fecha.Date);

        return incidencias.OrderByDescending(i => i.FechaRegistro).ToList();
    }

    public async Task<IncidenciaDto?> ObtenerIncidenciaPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync($"{RutaIncidencias}/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        await AsegurarExitoAsync(respuesta, cancelacion);

        var detalle = await respuesta.Content.ReadFromJsonAsync<DetalleApi>(CatalogosHttp.OpcionesJson, cancelacion);
        return detalle is null ? null : Mapear(detalle, detalle, await ContextoAsync(cancelacion));
    }

    public async Task<IncidenciaDto> RegistrarIncidenciaAsync(RegistrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var descripcion = dto.Descripcion.Trim();
        if (!string.IsNullOrWhiteSpace(dto.AccionInmediata))
            descripcion += $"\nAcción inmediata: {dto.AccionInmediata.Trim()}";
        if (!dto.HojaDeRutaId.HasValue && (!string.IsNullOrWhiteSpace(dto.RepartidorNombre) || !string.IsNullOrWhiteSpace(dto.VehiculoMatricula)))
            descripcion += $"\nRepartidor: {dto.RepartidorNombre ?? "—"} · Vehículo: {dto.VehiculoMatricula ?? "—"}";

        var respuesta = await _http.PostAsJsonAsync(RutaIncidencias, new
        {
            tipo = dto.Tipo,
            descripcion,
            severidad = dto.Severidad,
            envioId = dto.EnvioAfectadoId,
            hojaDeRutaId = dto.HojaDeRutaId
        }, cancelacion);
        await AsegurarExitoAsync(respuesta, cancelacion);

        var creada = await respuesta.Content.ReadFromJsonAsync<ResultadoApi>(CatalogosHttp.OpcionesJson, cancelacion)
            ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo leer la incidencia registrada.");

        return await ObtenerIncidenciaPorIdAsync(creada.Id, cancelacion)
            ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "La incidencia registrada no se pudo consultar.");
    }

    public async Task<bool> AgregarSeguimientoAsync(AgregarSeguimientoIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync($"{RutaIncidencias}/{dto.IncidenciaId}/seguimiento", new
        {
            comentario = dto.Comentario,
            tomarACargo = dto.NuevoEstado == EstadoPantallaEnInvestigacion
        }, cancelacion);

        return await ResolverBooleanoAsync(respuesta, cancelacion);
    }

    public async Task<bool> CerrarIncidenciaAsync(CerrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync($"{RutaIncidencias}/{dto.IncidenciaId}/cierre", new { resolucion = dto.ResolucionDetalle }, cancelacion);
        return await ResolverBooleanoAsync(respuesta, cancelacion);
    }

    public async Task<IReadOnlyList<EnvioDisponibleIncidenciaDto>> ListarEnviosDisponiblesAsync(CancellationToken cancelacion = default)
    {
        var envios = await LeerEnviosAsync(cancelacion);
        return envios
            .OrderByDescending(e => e.FechaCreacion)
            .Select(e => new EnvioDisponibleIncidenciaDto(e.Id, CodigoDe(e), e.NombreDestinatario, e.DireccionDestino, e.Estado))
            .ToList();
    }

    public async Task<IReadOnlyList<RutaDisponibleIncidenciaDto>> ListarRutasDisponiblesAsync(CancellationToken cancelacion = default)
    {
        var contexto = await ContextoAsync(cancelacion);
        return contexto.Rutas.Values
            .OrderByDescending(r => r.Fecha)
            .Select(r => new RutaDisponibleIncidenciaDto(r.HojaDeRutaId, r.NumeroRuta, contexto.NombreRepartidor(r.RepartidorId) ?? "Sin asignar", r.VehiculoPatente ?? "—", r.Estado))
            .ToList();
    }

    // ───────────────────────────── Auxiliares ─────────────────────────────

    private static string EstadoParaApi(string estadoPantalla) => estadoPantalla switch
    {
        EstadoPantallaEnInvestigacion => EstadoApiEnSeguimiento,
        "Resuelta" => "Cerrada",
        _ => estadoPantalla
    };

    private static string EstadoParaPantalla(string estadoApi) => estadoApi == EstadoApiEnSeguimiento ? EstadoPantallaEnInvestigacion : estadoApi;

    private static bool Contiene(string? texto, string termino) => texto?.Contains(termino, StringComparison.OrdinalIgnoreCase) == true;

    private static string CodigoDe(EnvioApi envio) =>
        !string.IsNullOrWhiteSpace(envio.CodigoSeguimiento) ? envio.CodigoSeguimiento : $"ENV-{envio.Id.ToString()[..8].ToUpperInvariant()}";

    private static IncidenciaDto Mapear(ResumenApi r, DetalleApi? detalle, ContextoIncidencias contexto)
    {
        var envio = r.EnvioId.HasValue ? contexto.Envios.GetValueOrDefault(r.EnvioId.Value) : null;
        var ruta = r.HojaDeRutaId.HasValue ? contexto.Rutas.GetValueOrDefault(r.HojaDeRutaId.Value) : null;
        var nombresUsuarios = contexto.Usuarios;

        var bitacora = detalle?.Bitacora
            .OrderBy(n => n.Fecha)
            .Select(n => new EntradaBitacoraIncidenciaDto
            {
                FechaHora = n.Fecha,
                Usuario = nombresUsuarios.GetValueOrDefault(n.UsuarioId, "Usuario"),
                Comentario = n.Comentario
            })
            .ToList() ?? new List<EntradaBitacoraIncidenciaDto>();

        return new IncidenciaDto
        {
            Id = r.Id,
            NumeroIncidencia = $"INC-{r.FechaApertura:yyyyMM}-{r.Id.ToString()[..4].ToUpperInvariant()}",
            FechaRegistro = r.FechaApertura,
            FechaResolucion = r.FechaCierre,
            Tipo = r.Tipo,
            Severidad = r.Severidad,
            Estado = EstadoParaPantalla(r.Estado),
            Descripcion = r.Descripcion,
            EnvioAfectadoId = r.EnvioId,
            CodigoSeguimientoEnvio = envio is null ? null : CodigoDe(envio),
            DestinatarioEnvio = envio?.NombreDestinatario,
            HojaDeRutaId = r.HojaDeRutaId,
            NumeroRuta = ruta?.NumeroRuta,
            RepartidorNombre = ruta is null ? null : contexto.NombreRepartidor(ruta.RepartidorId),
            VehiculoMatricula = ruta?.VehiculoPatente,
            DespachadorRegistro = "Backoffice",
            DespachadorCierre = r.FechaCierre.HasValue && r.ResponsableUsuarioId.HasValue
                ? nombresUsuarios.GetValueOrDefault(r.ResponsableUsuarioId.Value)
                : null,
            ResolucionDetalle = detalle?.Resolucion,
            HistorialSeguimiento = bitacora
        };
    }

    private async Task<ContextoIncidencias> ContextoAsync(CancellationToken ct)
    {
        var envios = await LeerEnviosAsync(ct);
        var rutas = await LeerRutasAsync(ct);
        var repartidores = await CatalogosHttp.RepartidoresAsync(_http, ct);
        var usuarios = await CatalogosHttp.NombresDeUsuariosAsync(_http, ct);

        return new ContextoIncidencias(
            envios.ToDictionary(e => e.Id),
            rutas.ToDictionary(r => r.HojaDeRutaId),
            repartidores.ToDictionary(r => r.Key, r => r.Value.NombreCompleto),
            usuarios);
    }

    private async Task<List<EnvioApi>> LeerEnviosAsync(CancellationToken ct)
    {
        try
        {
            var respuesta = await _http.GetAsync("/api/envios", ct);
            return respuesta.IsSuccessStatusCode
                ? await respuesta.Content.ReadFromJsonAsync<List<EnvioApi>>(CatalogosHttp.OpcionesJson, ct) ?? new List<EnvioApi>()
                : new List<EnvioApi>();
        }
        catch (HttpRequestException)
        {
            return new List<EnvioApi>();
        }
    }

    private async Task<List<RutaApi>> LeerRutasAsync(CancellationToken ct)
    {
        try
        {
            var respuesta = await _http.GetAsync("/api/planificacion/hojas-de-ruta", ct);
            return respuesta.IsSuccessStatusCode
                ? await respuesta.Content.ReadFromJsonAsync<List<RutaApi>>(CatalogosHttp.OpcionesJson, ct) ?? new List<RutaApi>()
                : new List<RutaApi>();
        }
        catch (HttpRequestException)
        {
            return new List<RutaApi>();
        }
    }

    /// <summary>La pantalla distingue "no existe" (false) de los errores inesperados, que se informan como excepción.</summary>
    private static async Task<bool> ResolverBooleanoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return true;
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return false;

        await AsegurarExitoAsync(respuesta, ct);
        return false;
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        throw new ApiExcepcion(respuesta.StatusCode, ExtraerMensaje(cuerpo) ?? $"Error HTTP {(int)respuesta.StatusCode}");
    }

    /// <summary>Los errores de la API operativa llegan como {"error":"..."}; se muestra solo el mensaje.</summary>
    internal static string? ExtraerMensaje(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
            return null;

        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            if (documento.RootElement.ValueKind == JsonValueKind.Object && documento.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch (JsonException)
        {
            // No era JSON: se usa el texto tal cual
        }

        return cuerpo;
    }

    private sealed record ContextoIncidencias(
        Dictionary<Guid, EnvioApi> Envios,
        Dictionary<Guid, RutaApi> Rutas,
        Dictionary<Guid, string> Repartidores,
        Dictionary<Guid, string> Usuarios)
    {
        public string? NombreRepartidor(Guid? repartidorId) => repartidorId.HasValue ? Repartidores.GetValueOrDefault(repartidorId.Value) : null;
    }

    private record PaginaApi(List<ResumenApi> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);

    private record ResumenApi(
        Guid Id, string Tipo, string Severidad, string Estado, string Descripcion, Guid? EnvioId, Guid? HojaDeRutaId,
        Guid? ResponsableUsuarioId, DateTime FechaApertura, DateTime? FechaCierre);

    private sealed record DetalleApi(
        Guid Id, string Tipo, string Severidad, string Estado, string Descripcion, Guid? EnvioId, Guid? HojaDeRutaId,
        Guid? ResponsableUsuarioId, DateTime FechaApertura, DateTime? FechaCierre, string? Resolucion, List<NotaApi> Bitacora)
        : ResumenApi(Id, Tipo, Severidad, Estado, Descripcion, EnvioId, HojaDeRutaId, ResponsableUsuarioId, FechaApertura, FechaCierre);

    private record NotaApi(DateTime Fecha, Guid UsuarioId, string Comentario);

    private record ResultadoApi(Guid Id);

    private record EnvioApi(Guid Id, string DireccionDestino, string NombreDestinatario, DateTime FechaCreacion, string Estado, string? CodigoSeguimiento);

    private record RutaApi(Guid HojaDeRutaId, string NumeroRuta, DateOnly Fecha, string Estado, Guid? RepartidorId, string? VehiculoPatente);
}
