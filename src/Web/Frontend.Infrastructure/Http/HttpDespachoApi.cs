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
/// Implementación basada en HTTP/REST de IDespachoApi para el control de salida física de vehículos.
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

        var repartidores = await CatalogosHttp.RepartidoresAsync(_clienteHttp, cancelacion);

        return rutasResumen.Select(r =>
        {
            var rep = r.RepartidorId.HasValue ? repartidores.GetValueOrDefault(r.RepartidorId.Value) : null;
            return new HojaDeRutaDto
            {
                Id = r.HojaDeRutaId,
                NumeroRuta = r.NumeroRuta,
                Fecha = r.Fecha,
                Estado = r.Estado,
                RepartidorId = r.RepartidorId ?? Guid.Empty,
                RepartidorNombre = rep?.NombreCompleto ?? r.RepartidorDocumento ?? "Repartidor asignado",
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
            };
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

        var repartidores = await CatalogosHttp.RepartidoresAsync(_clienteHttp, cancelacion);
        var repartidor = resultadoApi.RepartidorId.HasValue
            ? repartidores.GetValueOrDefault(resultadoApi.RepartidorId.Value)
            : null;

        return new VerificacionDespachoDto
        {
            HojaDeRutaId = resultadoApi.HojaDeRutaId,
            NumeroRuta = resultadoApi.NumeroRuta,
            Fecha = resultadoApi.Fecha,
            EstadoRuta = resultadoApi.EstadoRuta,
            RepartidorId = resultadoApi.RepartidorId ?? Guid.Empty,
            RepartidorNombre = repartidor?.NombreCompleto ?? "Repartidor asignado",
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
    /// Confirma formalmente la salida del vehículo.
    /// </summary>
    public async Task<ResultadoDespachoDto> ConfirmarDespachoAsync(ConfirmarDespachoDto dto, string operarioUsuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.SimularRutaPreviaSinRendir)
        {
            const string msgSimulacion = "El repartidor asignado posee una hoja de ruta previa sin rendir en depósito. Debe registrar su rendición antes de autorizar una nueva salida.";
            return new ResultadoDespachoDto
            {
                Exitoso = false,
                Mensaje = msgSimulacion,
                Errores = new List<string> { msgSimulacion }
            };
        }

        var url = $"/api/planificacion/hojas-de-ruta/{dto.HojaDeRutaId}/despacho";
        var enviosNoCargadosIds = (dto.BultosNoCargados ?? new List<BultoNoCargadoItemDto>())
            .Where(b => b.EnvioId != Guid.Empty)
            .Select(b => b.EnvioId)
            .Distinct()
            .ToList();

        var cargaUtil = new
        {
            Observaciones = dto.ObservacionesSalida,
            EnviosNoCargadosIds = enviosNoCargadosIds,
            ConfirmarCargaEnMuelle = true
        };

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _clienteHttp.PostAsJsonAsync(url, cargaUtil, OpcionesJson, cancelacion);
        }
        catch (HttpRequestException ex)
        {
            var msgRed = $"Error de red al despachar la hoja de ruta: {ex.Message}";
            return new ResultadoDespachoDto
            {
                Exitoso = false,
                Mensaje = msgRed,
                Errores = new List<string> { msgRed }
            };
        }

        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            var detalleConflicto = HttpIncidenciasApi.ExtraerMensaje(await respuesta.Content.ReadAsStringAsync(cancelacion))
                ?? "La hoja de ruta fue modificada por otro usuario o ya fue despachada.";
            return new ResultadoDespachoDto
            {
                Exitoso = false,
                Mensaje = detalleConflicto,
                Errores = new List<string> { detalleConflicto }
            };
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpoError = await respuesta.Content.ReadAsStringAsync(cancelacion);
            var detalleError = HttpIncidenciasApi.ExtraerMensaje(cuerpoError)
                ?? (string.IsNullOrWhiteSpace(cuerpoError) ? $"Error HTTP {(int)respuesta.StatusCode}" : cuerpoError);
            return new ResultadoDespachoDto
            {
                Exitoso = false,
                Mensaje = detalleError,
                Errores = new List<string> { detalleError }
            };
        }

        var resultadoApi = await respuesta.Content.ReadFromJsonAsync<DespachoResultApiDto>(OpcionesJson, cancelacion);
        if (resultadoApi is null)
        {
            const string msgDeserializacion = "No fue posible procesar la confirmación de despacho recibida del servidor.";
            return new ResultadoDespachoDto
            {
                Exitoso = false,
                Mensaje = msgDeserializacion,
                Errores = new List<string> { msgDeserializacion }
            };
        }

        var retiradosCount = resultadoApi.EnviosRetirados?.Count ?? 0;
        var numeroRuta = $"HR-{(resultadoApi.FechaDespacho ?? DateTime.UtcNow):yyyyMMdd}-{resultadoApi.HojaDeRutaId.ToString()[..4].ToUpperInvariant()}";

        return new ResultadoDespachoDto
        {
            Exitoso = true,
            Mensaje = !string.IsNullOrWhiteSpace(resultadoApi.Mensaje)
                ? resultadoApi.Mensaje
                : $"Hoja de ruta {numeroRuta} despachada con éxito. Envíos en tránsito: {resultadoApi.EnviosEnTransito}. Reintegrados a depósito: {retiradosCount}.",
            HojaDeRutaId = resultadoApi.HojaDeRutaId,
            NumeroRuta = numeroRuta,
            EnviosEnTransitoCount = resultadoApi.EnviosEnTransito,
            EnviosReintegradosDepositoCount = retiradosCount
        };
    }

    /// <summary>
    /// Registra la rendición de una ruta previa si se requiere desbloquear al repartidor.
    /// </summary>
    public async Task<bool> RendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(numeroRuta))
            return false;

        try
        {
            var respPendientes = await _clienteHttp.GetAsync("/api/deposito/hojas-de-ruta-pendientes-rendicion", cancelacion);
            if (!respPendientes.IsSuccessStatusCode)
                return false;

            var pendientes = await respPendientes.Content.ReadFromJsonAsync<List<RutaPendienteRendicionApiDto>>(OpcionesJson, cancelacion);
            var coincidencia = pendientes?.FirstOrDefault(r =>
                string.Equals(r.NumeroRuta, numeroRuta.Trim(), StringComparison.OrdinalIgnoreCase));

            if (coincidencia is null)
                return false;

            var respDetalle = await _clienteHttp.GetAsync($"/api/deposito/hojas-de-ruta/{coincidencia.HojaDeRutaId}/rendicion", cancelacion);
            List<string> codigosDevueltos = new();
            string motivoInterno = "RETORNO_SIN_INTENTO";

            if (respDetalle.IsSuccessStatusCode)
            {
                var detalle = await respDetalle.Content.ReadFromJsonAsync<DetalleRendicionApiDto>(OpcionesJson, cancelacion);
                if (detalle?.BultosEsperadosRetorno is { Count: > 0 })
                {
                    codigosDevueltos = detalle.BultosEsperadosRetorno.Select(b => b.Identificacion).ToList();
                }
                if (detalle?.MotivosDisponibles is { Count: > 0 })
                {
                    motivoInterno = detalle.MotivosDisponibles[0].Codigo;
                }
            }

            var respRendir = await _clienteHttp.PostAsJsonAsync(
                $"/api/deposito/hojas-de-ruta/{coincidencia.HojaDeRutaId}/rendicion",
                new
                {
                    CodigosBultosDevueltos = codigosDevueltos,
                    MotivoInternoCodigo = motivoInterno,
                    Observaciones = "Rendición registrada desde verificación de despacho para habilitar nueva salida."
                },
                OpcionesJson,
                cancelacion);

            return respRendir.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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
        string Estado,
        DateTime? FechaDespacho,
        int EnviosEnTransito,
        List<EnvioRetiradoApiDto>? EnviosRetirados,
        bool Idempotente,
        string? Mensaje);

    private record EnvioRetiradoApiDto(Guid EnvioId, string CodigoSeguimiento, string Motivo);

    private record RutaPendienteRendicionApiDto(Guid HojaDeRutaId, string NumeroRuta);

    private record DetalleRendicionApiDto(
        List<BultoEsperadoRendicionApiDto>? BultosEsperadosRetorno,
        List<MotivoRendicionApiDto>? MotivosDisponibles);

    private record BultoEsperadoRendicionApiDto(string Identificacion);

    private record MotivoRendicionApiDto(string Codigo);
}
