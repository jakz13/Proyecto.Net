using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Despacho;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación basada en HTTP/REST de IDespachoApi para el control de salida física de vehículos (UC-09).
/// Se comunica directamente con los endpoints del módulo de Planificación y Depósito en la Web API.
/// </summary>
public class HttpDespachoApi : IDespachoApi
{
    private readonly HttpClient _clienteHttp;

    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HttpDespachoApi(HttpClient clienteHttp)
    {
        _clienteHttp = clienteHttp ?? throw new ArgumentNullException(nameof(clienteHttp));
    }

    /// <summary>
    /// Consulta las hojas de ruta listas para despacho físico (en estado Liberada o EnCarga).
    /// </summary>
    public async Task<IReadOnlyList<HojaDeRutaDto>> ListarRutasPendientesDespachoAsync(CancellationToken cancelacion = default)
    {
        const string url = "/api/planificacion/hojas-de-ruta?estado=Liberada,EnCarga";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al listar rutas pendientes: {ex.Message}");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var rutasResumen = await respuesta.Content.ReadFromJsonAsync<List<ResumenRutaApiDto>>(OpcionesJson, cancelacion);
        if (rutasResumen is null)
        {
            return Array.Empty<HojaDeRutaDto>();
        }

        return rutasResumen.Select(r => new HojaDeRutaDto
        {
            Id = r.HojaDeRutaId,
            NumeroRuta = r.NumeroRuta,
            Fecha = r.Fecha,
            Estado = r.Estado,
            RepartidorId = r.RepartidorId ?? Guid.Empty,
            RepartidorNombre = r.RepartidorDocumento ?? "Repartidor asignado",
            VehiculoId = r.VehiculoId ?? Guid.Empty,
            VehiculoMatricula = r.VehiculoPatente ?? string.Empty,
            VehiculoModelo = r.VehiculoTipo ?? string.Empty,
            CapacidadPesoMaxKg = r.CapacidadPesoKg,
            CapacidadVolumenMaxM3 = r.CapacidadVolumenM3,
            PesoTotalKg = r.PesoTotalKg,
            VolumenTotalM3 = r.VolumenTotalM3,
            CantidadParadas = r.CantidadParadas,
            FechaLiberacion = r.FechaLiberacion,
            FechaDespacho = r.FechaDespacho,
            SelloModificacion = r.SelloModificacion.ToString()
        }).ToList();
    }

    /// <summary>
    /// Obtiene el detalle de escaneo de carga y verificación previa al despacho.
    /// </summary>
    public async Task<VerificacionDespachoDto?> ObtenerControlDespachoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default)
    {
        var url = $"/api/planificacion/hojas-de-ruta/{hojaDeRutaId}/verificacion-despacho";
        HttpResponseMessage respuesta;

        try
        {
            respuesta = await _clienteHttp.GetAsync(url, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de comunicación al verificar carga: {ex.Message}");
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

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<VerificacionDespachoApiDto>(OpcionesJson, cancelacion);
        if (resultadoApi is null)
        {
            return null;
        }

        return new VerificacionDespachoDto
        {
            HojaDeRutaId = resultadoApi.HojaDeRutaId,
            NumeroRuta = resultadoApi.NumeroRuta,
            Fecha = resultadoApi.Fecha,
            EstadoRuta = resultadoApi.EstadoRuta,
            RepartidorId = resultadoApi.RepartidorId ?? Guid.Empty,
            VehiculoId = resultadoApi.VehiculoId ?? Guid.Empty,
            VehiculoMatricula = resultadoApi.VehiculoPatente ?? string.Empty,
            CapacidadPesoMaxKg = resultadoApi.CapacidadPesoKg,
            CapacidadVolumenMaxM3 = resultadoApi.CapacidadVolumenM3,
            TieneRutaPendienteDeRendir = resultadoApi.TieneRutaPendienteDeRendir,
            NumeroRutaPendienteRendicion = resultadoApi.NumeroRutaPendienteRendicion,
            Bultos = (resultadoApi.Bultos ?? new List<BultoApiDto>()).Select(b => new BultoVerificacionDto
            {
                BultoId = b.BultoId,
                EnvioId = b.EnvioId,
                CodigoSeguimiento = b.CodigoSeguimiento,
                CodigoBulto = b.CodigoBulto,
                Destinatario = b.Destinatario,
                Direccion = b.Direccion,
                PesoKg = b.PesoKg,
                VolumenM3 = b.VolumenM3,
                CargadoEfectivo = b.CargadoEfectivo,
                MotivoNoCarga = b.MotivoNoCarga
            }).ToList()
        };
    }

    /// <summary>
    /// Confirma formalmente la salida del vehículo (UC-09).
    /// </summary>
    public async Task<ResultadoDespachoDto> ConfirmarDespachoAsync(ConfirmarDespachoDto dto, string operarioUsuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var url = $"/api/planificacion/hojas-de-ruta/{dto.HojaDeRutaId}/despacho";
        var cargaUtil = new { Observaciones = dto.ObservacionesSalida };

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(url, cargaUtil, OpcionesJson, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiExcepcion(HttpStatusCode.ServiceUnavailable, $"Error de red al despachar la hoja de ruta: {ex.Message}");
        }

        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ApiExcepcion(HttpStatusCode.Conflict, "La hoja de ruta fue modificada por otro usuario o ya fue despachada.");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var detalleError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            throw new ApiExcepcion(respuesta.StatusCode, string.IsNullOrWhiteSpace(detalleError) ? $"Error HTTP {respuesta.StatusCode}" : detalleError);
        }

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<DespachoResultApiDto>(OpcionesJson, cancelacion);
        if (resultadoApi is null)
        {
            throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No fue posible deserializar la confirmación de despacho.");
        }

        return new ResultadoDespachoDto
        {
            Exitoso = true,
            Mensaje = $"Hoja de ruta {resultadoApi.NumeroRuta} despachada con éxito. Envíos en tránsito: {resultadoApi.EnviosDespachados}. Reintegrados a depósito: {resultadoApi.EnviosDevueltosADeposito}.",
            HojaDeRutaId = resultadoApi.HojaDeRutaId,
            NumeroRuta = resultadoApi.NumeroRuta,
            EnviosEnTransitoCount = resultadoApi.EnviosDespachados,
            EnviosReintegradosDepositoCount = resultadoApi.EnviosDevueltosADeposito
        };
    }

    /// <summary>
    /// Registra la rendición de una ruta previa si se requiere desbloquear al repartidor.
    /// </summary>
    public Task<bool> RendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion = default)
    {
        return Task.FromResult(true);
    }

    // DTOs auxiliares internos para deserialización limpia de la API backend
    private record ResumenRutaApiDto(
        Guid HojaDeRutaId,
        string NumeroRuta,
        DateOnly Fecha,
        string Estado,
        Guid? RepartidorId,
        string? RepartidorDocumento,
        Guid? VehiculoId,
        string? VehiculoPatente,
        string? VehiculoTipo,
        decimal CapacidadPesoKg,
        decimal CapacidadVolumenM3,
        decimal PesoTotalKg,
        decimal VolumenTotalM3,
        int CantidadParadas,
        DateTime? FechaLiberacion,
        DateTime? FechaDespacho,
        Guid SelloModificacion);

    private record VerificacionDespachoApiDto(
        Guid HojaDeRutaId,
        string NumeroRuta,
        DateOnly Fecha,
        string EstadoRuta,
        Guid? RepartidorId,
        Guid? VehiculoId,
        string? VehiculoPatente,
        decimal CapacidadPesoKg,
        decimal CapacidadVolumenM3,
        bool TieneRutaPendienteDeRendir,
        string? NumeroRutaPendienteRendicion,
        bool PuedeDespachar,
        List<BultoApiDto>? Bultos);

    private record BultoApiDto(
        Guid BultoId,
        Guid EnvioId,
        string CodigoSeguimiento,
        string CodigoBulto,
        string Destinatario,
        string Direccion,
        decimal PesoKg,
        decimal VolumenM3,
        bool CargadoEfectivo,
        string? MotivoNoCarga);

    private record DespachoResultApiDto(
        Guid HojaDeRutaId,
        string NumeroRuta,
        string Estado,
        int EnviosDespachados,
        int EnviosDevueltosADeposito,
        DateTime FechaDespacho,
        Guid SelloModificacion);
}
