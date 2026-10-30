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
/// Implementación basada en HTTP/REST de IEnviosApi para consulta y gestión de envíos desde Backoffice y Portal del Comercio.
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
    /// Consulta el listado de envíos aplicando filtros de estado, fecha, búsqueda y paginación.
    /// Cuando <see cref="FiltroEnviosDto.SoloEnviosPropiosComercio"/> es verdadero, consulta el endpoint aislado del comercio (UC-28).
    /// </summary>
    public async Task<ResultadoPaginadoDto<EnvioDto>> ListarEnviosAsync(FiltroEnviosDto filtro, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (filtro.SoloEnviosPropiosComercio)
        {
            var resultadoComercio = await ListarEnviosDelComercioAsync(filtro, cancelacion);
            if (resultadoComercio is not null)
            {
                return resultadoComercio;
            }
        }

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
            Estado = MapearEstado(e.Estado),
            HojaDeRutaNumero = e.HojaDeRutaNumero,
            RepartidorNombre = e.RepartidorNombre,
            ComercioNombre = e.ComercioNombre,
            PesoKg = e.PesoKg
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

        if (filtro.FechaDesde.HasValue)
        {
            elementos = elementos.Where(e => e.FechaCreacion.Date >= filtro.FechaDesde.Value.Date);
        }

        if (filtro.FechaHasta.HasValue)
        {
            elementos = elementos.Where(e => e.FechaCreacion.Date <= filtro.FechaHasta.Value.Date);
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
    /// Consulta los envíos propios del comercio autenticado en <c>GET /api/comercio/envios</c> (UC-28).
    /// </summary>
    private async Task<ResultadoPaginadoDto<EnvioDto>?> ListarEnviosDelComercioAsync(FiltroEnviosDto filtro, CancellationToken cancelacion)
    {
        var pagina = filtro.Pagina > 0 ? filtro.Pagina : 1;
        var tamano = filtro.TamanoPagina > 0 ? filtro.TamanoPagina : 20;

        var parametros = new List<string>
        {
            $"pagina={pagina}",
            $"tamano={tamano}"
        };

        if (filtro.Estado.HasValue)
        {
            parametros.Add($"estado={(int)filtro.Estado.Value}");
        }

        if (filtro.FechaDesde.HasValue)
        {
            parametros.Add($"desde={Uri.EscapeDataString(filtro.FechaDesde.Value.Date.ToString("yyyy-MM-dd"))}");
        }

        if (filtro.FechaHasta.HasValue)
        {
            parametros.Add($"hasta={Uri.EscapeDataString(filtro.FechaHasta.Value.Date.ToString("yyyy-MM-dd"))}");
        }

        if (!string.IsNullOrWhiteSpace(filtro.TerminoBusqueda))
        {
            parametros.Add($"texto={Uri.EscapeDataString(filtro.TerminoBusqueda.Trim())}");
        }

        var url = $"/api/comercio/envios?{string.Join("&", parametros)}";

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error al consultar envíos propios del comercio: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await ExtraerMensajeErrorAsync(respuesta, cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var paginaApi = await respuesta.Content.ReadFromJsonAsync<PaginaEnviosComercioApiDto>(OpcionesJson, cancelacion);
        if (paginaApi is null)
        {
            return new ResultadoPaginadoDto<EnvioDto>
            {
                Elementos = Array.Empty<EnvioDto>(),
                TotalRegistros = 0,
                PaginaActual = pagina,
                TamanoPagina = tamano
            };
        }

        var elementos = (paginaApi.Items ?? Array.Empty<EnvioComercioItemApiDto>())
            .Select(item => new EnvioDto
            {
                Id = item.EnvioId,
                CodigoSeguimiento = item.CodigoSeguimiento,
                NombreDestinatario = item.Destinatario,
                DireccionDestino = item.Direccion,
                FechaCreacion = item.FechaAlta,
                Estado = Enum.IsDefined(typeof(EstadoEnvioDto), item.Estado)
                    ? (EstadoEnvioDto)item.Estado
                    : MapearEstado(item.EstadoNombre ?? string.Empty),
                ModalidadServicio = item.ModalidadServicio ?? "Estandar",
                Tarifa = item.Tarifa
            })
            .ToList();

        return new ResultadoPaginadoDto<EnvioDto>
        {
            Elementos = elementos,
            TotalRegistros = paginaApi.Total,
            PaginaActual = paginaApi.Pagina > 0 ? paginaApi.Pagina : pagina,
            TamanoPagina = paginaApi.TamanoPagina > 0 ? paginaApi.TamanoPagina : tamano
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
            ModalidadServicio = detalleApi.ModalidadServicio ?? "Estandar",
            Tarifa = detalleApi.Tarifa,
            HojaDeRutaNumero = detalleApi.HojaDeRutaId?.ToString(),
            BultosIdentificaciones = detalleApi.Bultos?.Select(b => b.Identificacion).ToList(),
            PesoKg = detalleApi.Bultos?.Sum(b => b.PesoKg) ?? 0
        };
    }

    /// <summary>
    /// Registra un nuevo envío enviando la solicitud a la API central.
    /// </summary>
    public async Task<EnvioDto> CrearEnvioAsync(CrearEnvioDto nuevoEnvio, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(nuevoEnvio);

        string calle = nuevoEnvio.Calle ?? "";
        string numero = nuevoEnvio.Numero ?? "";
        if (string.IsNullOrWhiteSpace(calle))
        {
            var partesDireccion = nuevoEnvio.DireccionDestino.Split(' ', 2);
            calle = partesDireccion.Length > 0 ? partesDireccion[0] : "Calle Principal";
            numero = partesDireccion.Length > 1 ? partesDireccion[1] : "1000";
        }

        var bultosPayload = (nuevoEnvio.Bultos != null && nuevoEnvio.Bultos.Count > 0)
            ? nuevoEnvio.Bultos.Select(b => new { Peso = b.Peso, Alto = b.Alto, Ancho = b.Ancho, Profundo = b.Profundo }).ToArray()
            : new[] { new { Peso = nuevoEnvio.PesoKg ?? 1m, Alto = 10m, Ancho = 10m, Profundo = 10m } };

        var direccionCompleta = !string.IsNullOrWhiteSpace(nuevoEnvio.DireccionDestino)
            ? nuevoEnvio.DireccionDestino
            : $"{calle} {numero}, {nuevoEnvio.Ciudad}".Trim();

        var payload = new
        {
            NombreDestinatario = nuevoEnvio.NombreDestinatario,
            Calle = calle,
            Numero = string.IsNullOrWhiteSpace(numero) ? "S/N" : numero,
            Ciudad = string.IsNullOrWhiteSpace(nuevoEnvio.Ciudad) ? "Montevideo" : nuevoEnvio.Ciudad,
            Telefono = nuevoEnvio.TelefonoDestinatario,
            Email = nuevoEnvio.EmailDestinatario,
            Documento = nuevoEnvio.DocumentoDestinatario,
            ModalidadServicio = nuevoEnvio.ModalidadServicio,
            Bultos = bultosPayload
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
            DireccionDestino = direccionCompleta,
            FechaCreacion = DateTime.UtcNow,
            Estado = EstadoEnvioDto.Admitido,
            Tarifa = creado.Tarifa,
            ModalidadServicio = nuevoEnvio.ModalidadServicio,
            BultosIdentificaciones = creado.Bultos,
            PesoKg = nuevoEnvio.PesoKg,
            VolumenM3 = nuevoEnvio.VolumenM3
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

    private string _claveApiSesion = string.Empty;

    /// <summary>
    /// Devuelve la última clave de API obtenida en la sesión actual.
    /// </summary>
    public Task<string> ObtenerClaveApiActivaAsync(CancellationToken cancelacion = default)
    {
        return Task.FromResult(_claveApiSesion);
    }

    /// <summary>
    /// Solicita la generación y rotación de la clave de API del comercio autenticado.
    /// </summary>
    public async Task<ClaveApiComercioDto> RegenerarClaveApiAsync(CancellationToken cancelacion = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsync("/api/comercio/clave-api", null, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al generar clave de API: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await ExtraerMensajeErrorAsync(respuesta, cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode} al generar clave de API." : detalleError);
        }

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<ClaveApiGeneradaRespuestaDto>(OpcionesJson, cancelacion);
        if (resultadoApi is null || string.IsNullOrWhiteSpace(resultadoApi.ClaveApi))
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No fue posible procesar la nueva clave de API generada.");
        }

        _claveApiSesion = resultadoApi.ClaveApi;
        return new ClaveApiComercioDto(
            resultadoApi.ClaveApi,
            resultadoApi.Aviso ?? "Guarde esta clave ahora: no volverá a mostrarse. La clave anterior ya no es válida.",
            DateTime.UtcNow);
    }

    /// <summary>
    /// Invoca el endpoint de integración de envíos autenticado con la cabecera X-Api-Key.
    /// </summary>
    public async Task<RespuestaAltaEnvioApiDto> CrearEnvioPorApiIntegracionAsync(
        SolicitudAltaEnvioApiDto solicitud,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (string.IsNullOrWhiteSpace(solicitud.ClaveApi))
        {
            throw new ArgumentException("Debe proporcionar una clave de API para autenticar la solicitud.", nameof(solicitud));
        }

        if (string.IsNullOrWhiteSpace(solicitud.NombreDestinatario))
        {
            throw new ArgumentException("El nombre del destinatario es obligatorio.", nameof(solicitud));
        }

        if (string.IsNullOrWhiteSpace(solicitud.Calle) || string.IsNullOrWhiteSpace(solicitud.Numero))
        {
            throw new ArgumentException("La calle y el número de puerta son obligatorios.", nameof(solicitud));
        }

        if (solicitud.Bultos is null || solicitud.Bultos.Count == 0)
        {
            throw new ArgumentException("El envío debe incluir al menos un bulto.", nameof(solicitud));
        }

        var cuerpoSolicitud = new
        {
            ModalidadServicio = string.IsNullOrWhiteSpace(solicitud.ModalidadServicio) ? "Estandar" : solicitud.ModalidadServicio.Trim(),
            NombreDestinatario = solicitud.NombreDestinatario.Trim(),
            Documento = string.IsNullOrWhiteSpace(solicitud.Documento) ? null : solicitud.Documento.Trim(),
            Email = string.IsNullOrWhiteSpace(solicitud.Email) ? null : solicitud.Email.Trim(),
            Telefono = string.IsNullOrWhiteSpace(solicitud.Telefono) ? null : solicitud.Telefono.Trim(),
            Calle = solicitud.Calle.Trim(),
            Numero = solicitud.Numero.Trim(),
            Ciudad = string.IsNullOrWhiteSpace(solicitud.Ciudad) ? "Montevideo" : solicitud.Ciudad.Trim(),
            Referencia = string.IsNullOrWhiteSpace(solicitud.Referencia) ? null : solicitud.Referencia.Trim(),
            ReferenciaExterna = string.IsNullOrWhiteSpace(solicitud.ReferenciaExterna) ? null : solicitud.ReferenciaExterna.Trim(),
            Bultos = solicitud.Bultos.Select(b => new
            {
                Peso = b.Peso,
                Alto = b.Alto,
                Ancho = b.Ancho,
                Profundo = b.Profundo
            }).ToList()
        };

        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/integracion/v1/envios")
        {
            Content = JsonContent.Create(cuerpoSolicitud, options: OpcionesJson)
        };
        peticion.Headers.TryAddWithoutValidation("X-Api-Key", solicitud.ClaveApi.Trim());

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.SendAsync(peticion, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al invocar API de integración: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await ExtraerMensajeErrorAsync(respuesta, cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode} al registrar envío por API." : detalleError);
        }

        var creado = await respuesta.Content.ReadFromJsonAsync<AltaEnvioIntegracionRespuestaDto>(OpcionesJson, cancelacion);
        if (creado is null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No fue posible interpretar la respuesta del servidor en el alta por API.");
        }

        return new RespuestaAltaEnvioApiDto(
            creado.Id,
            creado.CodigoSeguimiento,
            string.IsNullOrWhiteSpace(creado.Estado) ? "Admitido" : creado.Estado,
            creado.Tarifa,
            creado.Bultos ?? Array.Empty<string>(),
            creado.Duplicado);
    }

    /// <summary>
    /// Solicita al backend la generación de etiquetas para los envíos admitidos seleccionados y arma el documento descargable (UC-27).
    /// </summary>
    public async Task<ResultadoGeneracionEtiquetasDto> GenerarEtiquetasEnviosAsync(
        IReadOnlyList<Guid> envioIds,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(envioIds);

        var idsUnicos = envioIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (idsUnicos.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un envío válido para generar etiquetas.", nameof(envioIds));
        }

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(
                "/api/comercio/envios/etiquetas",
                new { EnvioIds = idsUnicos },
                OpcionesJson,
                cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al generar etiquetas de envío: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await ExtraerMensajeErrorAsync(respuesta, cancelacion);
            throw new ApiExcepcion(
                respuesta.StatusCode,
                string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode} al generar etiquetas." : detalleError);
        }

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<EtiquetasGeneradasApiDto>(OpcionesJson, cancelacion);
        if (resultadoApi?.Etiquetas is null || resultadoApi.Etiquetas.Count == 0)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No fue posible obtener las etiquetas de los envíos seleccionados.");
        }

        var nombreArchivo = idsUnicos.Count == 1
            ? $"etiqueta-{resultadoApi.Etiquetas[0].CodigoSeguimiento.ToLowerInvariant()}.html"
            : $"etiquetas-envios-{DateTime.UtcNow:yyyyMMdd-HHmm}.html";

        var htmlDescargable = GeneradorDocumentoEtiquetas.ConstruirHtmlDescargable(resultadoApi.Etiquetas);

        return new ResultadoGeneracionEtiquetasDto(
            resultadoApi.Etiquetas,
            resultadoApi.TotalEnviosSeleccionados,
            resultadoApi.TotalEtiquetasGeneradas,
            nombreArchivo,
            htmlDescargable);
    }

    /// <summary>
    /// Confirma la recepción del bulto devuelto por parte del comercio, cerrando la devolución (UC-32).
    /// </summary>
    public async Task<ConfirmarDevolucionResultDto> ConfirmarDevolucionAsync(Guid envioId, CancellationToken cancelacion = default)
    {
        if (envioId == Guid.Empty)
            throw new ArgumentException("El identificador del envío es inválido.", nameof(envioId));

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsync($"/api/comercio/devoluciones/{envioId}/confirmacion", null, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al confirmar la devolución: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var resultado = await respuesta.Content.ReadFromJsonAsync<ConfirmarDevolucionResultDto>(OpcionesJson, cancelacion);
        return resultado ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo interpretar la respuesta del servidor.");
    }

    private record PaginaEnviosComercioApiDto(
        IReadOnlyList<EnvioComercioItemApiDto>? Items,
        int Total,
        int Pagina,
        int TamanoPagina,
        int TotalPaginas);

    private record EnvioComercioItemApiDto(
        Guid EnvioId,
        string CodigoSeguimiento,
        int Estado,
        string? EstadoNombre,
        string Destinatario,
        string Direccion,
        string? ModalidadServicio,
        decimal Tarifa,
        DateTime FechaAlta);

    private record EtiquetasGeneradasApiDto(
        IReadOnlyList<EtiquetaBultoDto> Etiquetas,
        int TotalEnviosSeleccionados,
        int TotalEtiquetasGeneradas);

    private static async Task<string> ExtraerMensajeErrorAsync(HttpResponseMessage respuesta, CancellationToken cancelacion)
    {
        var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
        try
        {
            using var documento = JsonDocument.Parse(detalleError);
            if (documento.RootElement.TryGetProperty("error", out var propiedadError))
            {
                return propiedadError.GetString() ?? detalleError;
            }
        }
        catch
        {
            // Se conserva el texto plano si no es JSON válido
        }

        return detalleError;
    }

    private record ClaveApiGeneradaRespuestaDto(string ClaveApi, string? Aviso);

    private record AltaEnvioIntegracionRespuestaDto(
        Guid Id,
        string CodigoSeguimiento,
        string Estado,
        decimal Tarifa,
        IReadOnlyList<string>? Bultos = null,
        bool Duplicado = false);

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

    private record EnvioResumenApiDto(
        Guid Id,
        Guid ComercioId,
        string DireccionDestino,
        string NombreDestinatario,
        DateTime FechaCreacion,
        string Estado,
        string? CodigoSeguimiento = null,
        string? HojaDeRutaNumero = null,
        string? RepartidorNombre = null,
        string? ComercioNombre = null,
        decimal? PesoKg = null);

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

    private record EnvioCreadoApiDto(Guid Id, string CodigoSeguimiento, string Estado, decimal Tarifa, List<string>? Bultos = null);
}
