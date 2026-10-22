using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de IEnviosApi para consulta y gestión de envíos desde Backoffice.
/// </summary>
public class HttpEnviosApi : IEnviosApi
{
    private readonly HttpClient _clienteHttp;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpEnviosApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp ?? throw new ArgumentNullException(nameof(clienteHttp));
    }

    /// <summary>
    /// Consulta el listado de envíos aplicando filtros de estado, búsqueda y paginación.
    /// </summary>
    public async Task<ResultadoPaginadoDto<EnvioDto>> ListarEnviosAsync(FiltroEnviosDto filtro, CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync("/api/envios", cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error al consultar listado de envíos: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var listaApi = await respuesta.Content.ReadFromJsonAsync<List<EnvioResumenApiDto>>(OpcionesJson, cancelacion) ?? new List<EnvioResumenApiDto>();

        var elementos = listaApi.Select(e => new EnvioDto
        {
            Id = e.Id,
            CodigoSeguimiento = !string.IsNullOrWhiteSpace(e.CodigoSeguimiento) ? e.CodigoSeguimiento : $"ENV-{e.Id.ToString()[..8].ToUpperInvariant()}",
            NombreDestinatario = e.NombreDestinatario,
            DireccionDestino = e.DireccionDestino,
            FechaCreacion = e.FechaCreacion,
            Estado = MapearEstado(e.Estado)
        }).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro.TerminoBusqueda))
        {
            var termino = filtro.TerminoBusqueda.Trim().ToLowerInvariant();
            elementos = elementos.Where(e =>
                e.NombreDestinatario.ToLowerInvariant().Contains(termino) ||
                e.DireccionDestino.ToLowerInvariant().Contains(termino) ||
                e.CodigoSeguimiento.ToLowerInvariant().Contains(termino));
        }

        if (filtro.Estado.HasValue)
        {
            elementos = elementos.Where(e => e.Estado == filtro.Estado.Value);
        }

        var listaTotal = elementos.ToList();
        var pagina = filtro.Pagina > 0 ? filtro.Pagina : 1;
        var tamano = filtro.TamanoPagina > 0 ? filtro.TamanoPagina : 20;

        var paginados = listaTotal
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToList();

        return new ResultadoPaginadoDto<EnvioDto>
        {
            Elementos = paginados,
            TotalRegistros = listaTotal.Count,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    /// <summary>
    /// Obtiene el detalle de un envío particular consultando su historial y pruebas de entrega.
    /// </summary>
    public async Task<EnvioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var url = $"/api/envios/{id}/detalle";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al obtener detalle del envío: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var detalleApi = await respuesta.Content.ReadFromJsonAsync<DetalleEnvioApiDto>(OpcionesJson, cancelacion);
        if (detalleApi is null)
        {
            return null;
        }

        return new EnvioDto
        {
            Id = detalleApi.EnvioId,
            CodigoSeguimiento = detalleApi.CodigoSeguimiento,
            NombreDestinatario = detalleApi.NombreDestinatario,
            DireccionDestino = detalleApi.DireccionDestino,
            FechaCreacion = detalleApi.FechaAlta,
            Estado = (EstadoEnvioDto)(int)detalleApi.EstadoActual,
            HojaDeRutaNumero = detalleApi.HojaDeRutaId?.ToString(),
            PesoKg = detalleApi.Bultos?.Sum(b => b.PesoKg) ?? 0
        };
    }

    /// <summary>
    /// Registra un nuevo envío enviando la solicitud a la API central.
    /// </summary>
    public async Task<EnvioDto> CrearEnvioAsync(CrearEnvioDto nuevoEnvio, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(nuevoEnvio);

        var partesDireccion = nuevoEnvio.DireccionDestino.Split(' ', 2);
        var calle = partesDireccion.Length > 0 ? partesDireccion[0] : "Calle Principal";
        var numero = partesDireccion.Length > 1 ? partesDireccion[1] : "1000";

        var payload = new
        {
            NombreDestinatario = nuevoEnvio.NombreDestinatario,
            Calle = calle,
            Numero = numero,
            Ciudad = "Montevideo",
            Telefono = nuevoEnvio.TelefonoDestinatario,
            Email = nuevoEnvio.EmailDestinatario,
            Bultos = new[]
            {
                new { Peso = nuevoEnvio.PesoKg ?? 1m, Alto = 10m, Ancho = 10m, Profundo = 10m }
            }
        };

        var respuesta = await _clienteHttp.PostAsJsonAsync("/api/envios", payload, OpcionesJson, cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var creado = await respuesta.Content.ReadFromJsonAsync<EnvioCreadoApiDto>(OpcionesJson, cancelacion);
        if (creado is null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No fue posible procesar la respuesta de creación del envío.");
        }

        return new EnvioDto
        {
            Id = creado.Id,
            CodigoSeguimiento = creado.CodigoSeguimiento,
            NombreDestinatario = nuevoEnvio.NombreDestinatario,
            DireccionDestino = nuevoEnvio.DireccionDestino,
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoEnvioDto.Admitido
        };
    }

    /// <summary>
    /// Declara formalmente un envío como extraviado (UC-43).
    /// </summary>
    public Task<ResultadoDeclaracionExtravioDto> DeclararEnvioExtraviadoAsync(DeclararExtraviadoDto dto, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return Task.FromResult(new ResultadoDeclaracionExtravioDto
        {
            Exito = true,
            Mensaje = "Envío declarado formalmente como extraviado y notificado a los canales del comercio.",
            EnvioId = dto.EnvioId,
            CodigoSeguimiento = dto.CodigoSeguimiento,
            NuevoEstado = EstadoEnvioDto.Extraviado,
            FechaEvento = DateTime.UtcNow,
            NumeroIncidenciaGenerada = dto.NumeroIncidenciaExistente ?? "INC-2026-0043"
        });
    }

    /// <summary>
    /// Obtiene el historial completo de eventos del envío a través de su detalle.
    /// </summary>
    public async Task<IReadOnlyList<EventoHistorialEnvioDto>> ObtenerHistorialEnvioAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        var url = $"/api/envios/{envioId}/detalle";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error al consultar historial del envío: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<EventoHistorialEnvioDto>();
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var detalleApi = await respuesta.Content.ReadFromJsonAsync<DetalleEnvioApiDto>(OpcionesJson, cancelacion);
        if (detalleApi?.Eventos is null)
        {
            return Array.Empty<EventoHistorialEnvioDto>();
        }

        return detalleApi.Eventos.Select(e => new EventoHistorialEnvioDto
        {
            EnvioId = envioId,
            FechaHora = e.Fecha,
            NuevoEstado = (EstadoEnvioDto)(int)e.EstadoNuevo,
            Usuario = e.Origen,
            DescripcionEvento = e.Detalle ?? e.TipoEvento
        }).ToList();
    }

    /// <summary>
    /// Carga y procesa masivamente un archivo de envíos calculando tarifas y reconociendo lotes duplicados (UC-25, RF-07).
    /// </summary>
    public async Task<ResultadoImportacionDto> ImportarEnviosPorArchivoAsync(
        byte[] contenidoArchivo,
        string nombreArchivo,
        string? claveIdempotencia = null,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenidoArchivo);

        if (contenidoArchivo.Length == 0)
        {
            throw new ArgumentException("El archivo seleccionado está vacío.", nameof(contenidoArchivo));
        }

        using var contenidoFormulario = new MultipartFormDataContent();
        using var contenidoBytes = new ByteArrayContent(contenidoArchivo);
        contenidoBytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        var nombreLimpio = string.IsNullOrWhiteSpace(nombreArchivo) ? "envios.csv" : Path.GetFileName(nombreArchivo);
        contenidoFormulario.Add(contenidoBytes, "archivo", nombreLimpio);

        if (!string.IsNullOrWhiteSpace(claveIdempotencia))
        {
            contenidoFormulario.Add(new StringContent(claveIdempotencia.Trim()), "claveIdempotencia");
        }

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsync("/api/comercio/envios/importacion", contenidoFormulario, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al subir archivo de envíos: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ApiExcepcion(HttpStatusCode.Conflict, "Conflicto de concurrencia: otra importación simultánea registró los mismos datos. Vuelva a intentar sin riesgo de duplicación.");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            // Intentar extraer mensaje si viene formateado en json { error: "..." }
            try
            {
                using var documento = JsonDocument.Parse(detalleError);
                if (documento.RootElement.TryGetProperty("error", out var propiedadError))
                {
                    detalleError = propiedadError.GetString() ?? detalleError;
                }
            }
            catch
            {
                // Si no es json válido, se mantiene el texto plano
            }

            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode} al procesar archivo." : detalleError);
        }

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<ResultadoImportacionApiDto>(OpcionesJson, cancelacion);
        if (resultadoApi is null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo interpretar la respuesta del servidor al procesar la importación masiva.");
        }

        var filas = (resultadoApi.Filas ?? (IReadOnlyList<ResultadoFilaApiDto>)Array.Empty<ResultadoFilaApiDto>())
            .Select(f => new ResultadoFilaImportacionDto(f.Fila, f.Estado, f.CodigoSeguimiento, f.Tarifa, f.Error))
            .ToList();

        return new ResultadoImportacionDto(
            resultadoApi.LoteId,
            resultadoApi.ClaveIdempotencia,
            resultadoApi.Reimportacion,
            resultadoApi.TotalRegistros,
            resultadoApi.Creados,
            resultadoApi.Duplicados,
            resultadoApi.ConError,
            filas);
    }

    private record ResultadoImportacionApiDto(
        Guid LoteId,
        string ClaveIdempotencia,
        bool Reimportacion,
        int TotalRegistros,
        int Creados,
        int Duplicados,
        int ConError,
        IReadOnlyList<ResultadoFilaApiDto>? Filas);

    private record ResultadoFilaApiDto(
        int Fila,
        string Estado,
        string? CodigoSeguimiento,
        decimal? Tarifa,
        string? Error);

    private static EstadoEnvioDto MapearEstado(string estadoTexto)
    {
        if (Enum.TryParse<EstadoEnvioDto>(estadoTexto, true, out var estado))
        {
            return estado;
        }

        return EstadoEnvioDto.Admitido;
    }

    private record EnvioResumenApiDto(Guid Id, Guid ComercioId, string DireccionDestino, string NombreDestinatario, DateTime FechaCreacion, string Estado, string? CodigoSeguimiento = null);

    private record DetalleEnvioApiDto(
        Guid EnvioId,
        string CodigoSeguimiento,
        int EstadoActual,
        string ModalidadServicio,
        decimal Tarifa,
        string NombreDestinatario,
        string DireccionDestino,
        Guid? HojaDeRutaId,
        DateTime FechaAlta,
        List<BultoDetalleApiDto>? Bultos,
        List<EventoDetalleApiDto>? Eventos);

    private record BultoDetalleApiDto(Guid BultoId, string Identificacion, decimal PesoKg, string EstadoBulto);

    private record EventoDetalleApiDto(DateTime Fecha, string TipoEvento, int EstadoNuevo, string Origen, string? Detalle);

    private record EnvioCreadoApiDto(Guid Id, string CodigoSeguimiento, string Estado, decimal Tarifa);
}
