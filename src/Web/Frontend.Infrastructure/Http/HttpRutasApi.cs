using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Http;

/// <summary>
/// Implementación HTTP de IRutasApi (UC-08, UC-10) sobre /api/planificacion.
/// La API planifica por hoja de ruta, repartidor, vehículo y envíos: el turno, el número de viaje y el criterio de
/// ordenamiento son datos de presentación que no se guardan, y los nombres se completan consultando la flota y las zonas.
/// </summary>
public class HttpRutasApi : IRutasApi
{
    private const string RutaPlanificacion = "/api/planificacion/hojas-de-ruta";
    private const string MotivoInternoRetiro = "RETORNO_SIN_INTENTO";

    private readonly HttpClient _http;

    public HttpRutasApi(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<HojaDeRutaDto>> ListarHojasDeRutaAsync(FiltroRutasDto? filtro = null, CancellationToken cancelacion = default)
    {
        var parametros = new List<string>();
        if (filtro?.Fecha is { } fecha)
            parametros.Add($"fecha={fecha:yyyy-MM-dd}");
        if (filtro?.RepartidorId is { } repartidorId && repartidorId != Guid.Empty)
            parametros.Add($"repartidorId={repartidorId}");
        if (!string.IsNullOrWhiteSpace(filtro?.Estado) && filtro.Estado != "Todos")
            parametros.Add($"estado={Uri.EscapeDataString(EstadoParaApi(filtro.Estado))}");

        var url = parametros.Count == 0 ? RutaPlanificacion : $"{RutaPlanificacion}?{string.Join("&", parametros)}";
        var respuesta = await _http.GetAsync(url, cancelacion);
        await AsegurarExitoAsync(respuesta, cancelacion);

        var resumenes = await respuesta.Content.ReadFromJsonAsync<List<ResumenApi>>(CatalogosHttp.OpcionesJson, cancelacion) ?? new List<ResumenApi>();
        var repartidores = await CatalogosHttp.RepartidoresAsync(_http, cancelacion);

        var rutas = resumenes.Select(r => MapearRuta(r, repartidores, new List<ParadaRutaDto>())).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filtro?.TerminoBusqueda))
        {
            var termino = filtro.TerminoBusqueda.Trim();
            rutas = rutas.Where(r =>
                r.NumeroRuta.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                r.RepartidorNombre.Contains(termino, StringComparison.OrdinalIgnoreCase) ||
                r.VehiculoMatricula.Contains(termino, StringComparison.OrdinalIgnoreCase));
        }

        return rutas.OrderByDescending(r => r.Fecha).ThenBy(r => r.NumeroRuta).ToList();
    }

    public async Task<HojaDeRutaDto?> ObtenerHojaDeRutaPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync($"{RutaPlanificacion}/{id}", cancelacion);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        await AsegurarExitoAsync(respuesta, cancelacion);

        var detalle = await respuesta.Content.ReadFromJsonAsync<DetalleApi>(CatalogosHttp.OpcionesJson, cancelacion);
        return detalle is null ? null : await MapearDetalleAsync(detalle, cancelacion);
    }

    public async Task<IReadOnlyList<EnvioPendienteRutaDto>> ListarEnviosPendientesAsync(CancellationToken cancelacion = default)
    {
        var respuesta = await _http.GetAsync("/api/planificacion/envios-pendientes", cancelacion);
        await AsegurarExitoAsync(respuesta, cancelacion);

        var pendientes = await respuesta.Content.ReadFromJsonAsync<List<PendienteApi>>(CatalogosHttp.OpcionesJson, cancelacion) ?? new List<PendienteApi>();
        var zonas = await CatalogosHttp.ZonasAsync(_http, cancelacion);
        var franjas = zonas.Values.SelectMany(z => z.FranjasHorarias).ToDictionary(f => f.Id);

        return pendientes.Select(p =>
        {
            var franja = p.FranjaHorariaId.HasValue ? franjas.GetValueOrDefault(p.FranjaHorariaId.Value) : null;
            return new EnvioPendienteRutaDto
            {
                Id = p.EnvioId,
                CodigoSeguimiento = p.CodigoSeguimiento,
                Destinatario = p.Destinatario,
                Direccion = p.Direccion,
                Zona = NombreDeZona(zonas, p.ZonaId),
                FranjaHoraria = franja is null ? "Sin franja" : $"{franja.HoraInicio:HH\\:mm} - {franja.HoraFin:HH\\:mm}",
                Turno = franja is null ? "Todo el día" : franja.HoraInicio.Hour < 13 ? "Mañana" : "Tarde",
                PesoKg = p.PesoKg,
                VolumenM3 = p.VolumenM3,
                Observaciones = string.IsNullOrWhiteSpace(p.Prioridad) ? null : $"Prioridad {p.Prioridad}",
                SelloModificacion = p.SelloModificacion.ToString(),
                AsignadoPreviamente = false
            };
        }).ToList();
    }

    public async Task<ResultadoArmadoRutaDto> ValidarArmadoRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync($"{RutaPlanificacion}/validacion", CuerpoArmado(dto), cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            return await ResultadoFallidoAsync(respuesta, cancelacion);

        var armado = await respuesta.Content.ReadFromJsonAsync<ArmadoApi>(CatalogosHttp.OpcionesJson, cancelacion);
        var errores = await DescribirExcluidosAsync(armado?.Excluidos, cancelacion);
        var incluidos = armado?.Paradas.Count ?? 0;

        return new ResultadoArmadoRutaDto
        {
            Exitoso = incluidos > 0,
            Mensaje = incluidos > 0
                ? $"{incluidos} envío(s) cumplen las restricciones{(errores.Count > 0 ? $" y {errores.Count} quedarían excluidos" : string.Empty)}."
                : "Ningún envío cumple las restricciones para armar la ruta.",
            ErroresValidacion = errores
        };
    }

    public async Task<ResultadoArmadoRutaDto> ArmarYConfirmarRutaAsync(ArmarHojaDeRutaDto dto, CancellationToken cancelacion = default)
    {
        var respuesta = await _http.PostAsJsonAsync(RutaPlanificacion, CuerpoArmado(dto), cancelacion);
        if (!respuesta.IsSuccessStatusCode)
            return await ResultadoFallidoAsync(respuesta, cancelacion);

        var armado = await respuesta.Content.ReadFromJsonAsync<ArmadoApi>(CatalogosHttp.OpcionesJson, cancelacion)
            ?? throw new ApiExcepcion(HttpStatusCode.InternalServerError, "No se pudo leer la hoja de ruta armada.");

        var ruta = await ObtenerHojaDeRutaPorIdAsync(armado.HojaDeRutaId, cancelacion);
        var excluidos = await DescribirExcluidosAsync(armado.Excluidos, cancelacion);

        return new ResultadoArmadoRutaDto
        {
            Exitoso = true,
            Mensaje = $"Hoja de ruta {ruta?.NumeroRuta} liberada con {armado.Paradas.Count} parada(s).",
            HojaDeRuta = ruta,
            Advertencias = excluidos
        };
    }

    /// <summary>Armar la ruta ya la deja liberada: confirma que la hoja esté en ese estado.</summary>
    public async Task<bool> LiberarHojaDeRutaAsync(Guid id, CancellationToken cancelacion = default)
    {
        var ruta = await ObtenerHojaDeRutaPorIdAsync(id, cancelacion);
        return ruta?.Estado == "Liberada";
    }

    public async Task<ResultadoReasignacionDto> ReasignarOReprogramarEnvioAsync(ReasignarEnvioDto dto, string despachadorUsuario, CancellationToken cancelacion = default)
    {
        var accion = dto.AccionDestino switch
        {
            "ReasignarOtraRuta" => "ReasignarARuta",
            "ReprogramarFranja" => "Reprogramar",
            _ => "Retirar"
        };

        Guid? franjaId = null;
        if (accion == "Reprogramar")
        {
            franjaId = await BuscarFranjaAsync(dto, cancelacion);
            if (franjaId is null)
                return Rechazo($"No hay una franja horaria configurada de {dto.NuevaFranjaHoraria} para reprogramar el envío.");
        }

        var observaciones = string.Join(" · ", new[]
        {
            dto.MotivoIncidencia,
            dto.DetalleMotivo,
            string.IsNullOrWhiteSpace(dto.CodigoMotivoNoEntrega) ? null : $"Motivo: {dto.CodigoMotivoNoEntrega}",
            $"Despachador: {despachadorUsuario}"
        }.Where(t => !string.IsNullOrWhiteSpace(t)));

        var respuesta = await _http.PostAsJsonAsync($"{RutaPlanificacion}/{dto.HojaDeRutaActualId}/envios/{dto.EnvioId}/reasignacion", new
        {
            accion,
            hojaDeRutaDestinoId = dto.HojaDeRutaDestinoId,
            franjaHorariaId = franjaId,
            // Un envío retirado en la calle se registra con el motivo interno del depósito; el motivo elegido queda en las observaciones
            motivoInternoCodigo = MotivoInternoRetiro,
            observaciones
        }, cancelacion);

        if (!respuesta.IsSuccessStatusCode)
            return Rechazo(HttpIncidenciasApi.ExtraerMensaje(await respuesta.Content.ReadAsStringAsync(cancelacion)) ?? $"Error HTTP {(int)respuesta.StatusCode}");

        var resultado = await respuesta.Content.ReadFromJsonAsync<ReasignacionApi>(CatalogosHttp.OpcionesJson, cancelacion);
        return new ResultadoReasignacionDto
        {
            Exitoso = true,
            Mensaje = resultado?.Mensaje ?? "Acción registrada.",
            NuevoEstadoEnvio = resultado?.EstadoEnvio ?? string.Empty,
            NotificacionRepartidor = "Se notificará al repartidor el cambio en su hoja de ruta.",
            RegistroAuditoria = $"{resultado?.CodigoSeguimiento}: {accion} registrado por {despachadorUsuario}."
        };
    }

    public async Task<IReadOnlyList<EventoReasignacionDto>> ListarEventosReasignacionAsync(Guid? hojaDeRutaId = null, CancellationToken cancelacion = default)
    {
        // El historial es por hoja de ruta; sin ruta indicada se arma con todas las del operador
        var todas = await ListarHojasDeRutaAsync(null, cancelacion);
        var numeros = todas.ToDictionary(r => r.Id, r => r.NumeroRuta);
        var rutas = hojaDeRutaId.HasValue ? new List<Guid> { hojaDeRutaId.Value } : todas.Select(r => r.Id).ToList();

        var usuarios = await CatalogosHttp.NombresDeUsuariosAsync(_http, cancelacion);
        var eventos = new List<EventoReasignacionDto>();

        foreach (var ruta in rutas)
        {
            var respuesta = await _http.GetAsync($"{RutaPlanificacion}/{ruta}/reasignaciones", cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                continue;

            var historial = await respuesta.Content.ReadFromJsonAsync<List<HistorialApi>>(CatalogosHttp.OpcionesJson, cancelacion) ?? new List<HistorialApi>();
            eventos.AddRange(historial.Select(h => new EventoReasignacionDto
            {
                Id = h.EventoId,
                FechaHora = h.Fecha,
                HojaDeRutaId = ruta,
                HojaDeRutaNumero = numeros.GetValueOrDefault(ruta, string.Empty),
                EnvioId = h.EnvioId,
                CodigoSeguimiento = h.CodigoSeguimiento,
                Destinatario = h.Destinatario,
                Motivo = DescribirEvento(h.TipoEvento),
                Detalle = h.Detalle,
                AccionRealizada = DescribirEvento(h.TipoEvento),
                DestinoNuevo = h.TipoEvento == "ReprogramacionFranja" ? "Nueva franja horaria" : "Depósito",
                DespachadorUsuario = h.ResponsableUsuarioId.HasValue ? usuarios.GetValueOrDefault(h.ResponsableUsuarioId.Value, "Despachador") : "Sistema",
                NotificacionRepartidor = "Notificación encolada para el repartidor"
            }));
        }

        return eventos.OrderByDescending(e => e.FechaHora).ToList();
    }

    // ───────────────────────────── Mapeos ─────────────────────────────

    private static object CuerpoArmado(ArmarHojaDeRutaDto dto) => new
    {
        repartidorId = dto.RepartidorId,
        vehiculoId = dto.VehiculoId,
        fecha = dto.Fecha.ToString("yyyy-MM-dd"),
        enviosIds = dto.EnviosIds
    };

    private static string EstadoParaApi(string estadoPantalla) => estadoPantalla == "Finalizada" ? "Rendida" : estadoPantalla;

    private static string EstadoParaPantalla(string estadoApi) => estadoApi == "Rendida" ? "Finalizada" : estadoApi;

    private static string NombreDeZona(IReadOnlyDictionary<Guid, ZonaCoberturaDto> zonas, Guid? zonaId) =>
        zonaId.HasValue && zonas.TryGetValue(zonaId.Value, out var zona) ? zona.Nombre : "Sin zona";

    private async Task<HojaDeRutaDto> MapearDetalleAsync(DetalleApi detalle, CancellationToken ct)
    {
        var repartidores = await CatalogosHttp.RepartidoresAsync(_http, ct);
        var zonas = await CatalogosHttp.ZonasAsync(_http, ct);

        var paradas = detalle.Paradas.OrderBy(p => p.Orden).Select(p => new ParadaRutaDto
        {
            Id = p.ParadaId,
            Orden = p.Orden,
            EnvioId = p.EnvioId,
            CodigoSeguimiento = p.CodigoSeguimiento,
            Destinatario = p.Destinatario,
            Direccion = p.Direccion,
            Zona = NombreDeZona(zonas, p.ZonaId),
            FranjaHoraria = string.IsNullOrWhiteSpace(p.VentanaHoraria) ? "Sin franja" : p.VentanaHoraria,
            PesoKg = p.PesoKg,
            VolumenM3 = p.VolumenM3,
            Estado = p.EstadoParada
        }).ToList();

        return MapearRuta(detalle.Ruta, repartidores, paradas);
    }

    private static HojaDeRutaDto MapearRuta(ResumenApi r, IReadOnlyDictionary<Guid, RepartidorDto> repartidores, List<ParadaRutaDto> paradas)
    {
        var repartidor = r.RepartidorId.HasValue ? repartidores.GetValueOrDefault(r.RepartidorId.Value) : null;
        return new HojaDeRutaDto
        {
            Id = r.HojaDeRutaId,
            NumeroRuta = r.NumeroRuta,
            Fecha = r.Fecha,
            Turno = "Jornada Completa",
            Estado = EstadoParaPantalla(r.Estado),
            RepartidorId = r.RepartidorId ?? Guid.Empty,
            RepartidorNombre = repartidor?.NombreCompleto ?? (r.RepartidorDocumento is null ? "Sin asignar" : $"Doc. {r.RepartidorDocumento}"),
            VehiculoId = r.VehiculoId ?? Guid.Empty,
            VehiculoMatricula = r.VehiculoPatente ?? "—",
            VehiculoModelo = r.VehiculoTipo ?? string.Empty,
            CapacidadPesoMaxKg = r.CapacidadPesoKg,
            CapacidadVolumenMaxM3 = r.CapacidadVolumenM3,
            PesoTotalKg = r.PesoTotalKg,
            VolumenTotalM3 = r.VolumenTotalM3,
            CantidadParadas = r.CantidadParadas,
            Paradas = paradas,
            FechaCreacion = r.FechaLiberacion ?? r.Fecha.ToDateTime(TimeOnly.MinValue),
            FechaLiberacion = r.FechaLiberacion,
            FechaDespacho = r.FechaDespacho,
            SelloModificacion = r.SelloModificacion.ToString()
        };
    }

    private async Task<List<string>> DescribirExcluidosAsync(List<ExcluidoApi>? excluidos, CancellationToken ct)
    {
        if (excluidos is null || excluidos.Count == 0)
            return new List<string>();

        var codigos = (await ListarEnviosPendientesAsync(ct)).ToDictionary(e => e.Id, e => e.CodigoSeguimiento);
        return excluidos
            .Select(e => $"{codigos.GetValueOrDefault(e.EnvioId, $"ENV-{e.EnvioId.ToString()[..8].ToUpperInvariant()}")}: {e.Motivo}")
            .ToList();
    }

    private async Task<Guid?> BuscarFranjaAsync(ReasignarEnvioDto dto, CancellationToken ct)
    {
        var partes = (dto.NuevaFranjaHoraria ?? string.Empty).Split('-', StringSplitOptions.TrimEntries);
        if (partes.Length != 2 || !TimeOnly.TryParse(partes[0], out var inicio) || !TimeOnly.TryParse(partes[1], out var fin))
            return null;

        var zonas = await CatalogosHttp.ZonasAsync(_http, ct);
        var candidatas = zonas.Values.Where(z => z.Activa).SelectMany(z => z.FranjasHorarias)
            .Where(f => f.Activa && f.HoraInicio == inicio && f.HoraFin == fin)
            .ToList();

        // Si se indicó una nueva fecha se prefiere una franja de ese día de la semana
        var delDia = dto.NuevaFecha.HasValue ? candidatas.Where(f => f.DiaSemana == dto.NuevaFecha.Value.DayOfWeek).ToList() : candidatas;
        return (delDia.Count > 0 ? delDia : candidatas).FirstOrDefault()?.Id;
    }

    private static string DescribirEvento(string tipoEvento) => tipoEvento switch
    {
        "RetiroDeRuta" => "Retirado de la ruta (vuelve a depósito)",
        "RetiroEnRuta" => "Retirado en la calle (no entregado)",
        "ReprogramacionFranja" => "Reprogramado a otra franja",
        _ => tipoEvento
    };

    private static ResultadoReasignacionDto Rechazo(string mensaje) => new()
    {
        Exitoso = false,
        Mensaje = mensaje,
        Errores = new List<string> { mensaje }
    };

    /// <summary>Un armado rechazado (reglas, recursos no disponibles o conflicto con otro despachador) se informa como resultado, no como excepción.</summary>
    private static async Task<ResultadoArmadoRutaDto> ResultadoFallidoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        var mensaje = HttpIncidenciasApi.ExtraerMensaje(await respuesta.Content.ReadAsStringAsync(ct)) ?? $"Error HTTP {(int)respuesta.StatusCode}";
        var colision = respuesta.StatusCode == HttpStatusCode.Conflict;

        return new ResultadoArmadoRutaDto
        {
            Exitoso = false,
            Mensaje = mensaje,
            ErroresValidacion = new List<string> { mensaje },
            ColisionDetectada = colision
        };
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        throw new ApiExcepcion(respuesta.StatusCode, HttpIncidenciasApi.ExtraerMensaje(cuerpo) ?? $"Error HTTP {(int)respuesta.StatusCode}");
    }

    private record ResumenApi(
        Guid HojaDeRutaId, string NumeroRuta, DateOnly Fecha, string Estado, Guid? RepartidorId, string? RepartidorDocumento,
        Guid? VehiculoId, string? VehiculoPatente, string? VehiculoTipo, decimal CapacidadPesoKg, decimal CapacidadVolumenM3,
        decimal PesoTotalKg, decimal VolumenTotalM3, int CantidadParadas, DateTime? FechaLiberacion, DateTime? FechaDespacho, Guid SelloModificacion);

    private record DetalleApi(ResumenApi Ruta, List<ParadaApi> Paradas);

    private record ParadaApi(
        Guid ParadaId, int Orden, Guid EnvioId, string CodigoSeguimiento, string EstadoEnvio, string EstadoParada, string Destinatario,
        string Direccion, Guid? ZonaId, string? VentanaHoraria, decimal PesoKg, decimal VolumenM3);

    private record PendienteApi(
        Guid EnvioId, string CodigoSeguimiento, string Destinatario, string Direccion, Guid? ZonaId, string? Prioridad,
        string ModalidadServicio, decimal PesoKg, decimal VolumenM3, Guid? FranjaHorariaId, Guid SelloModificacion);

    private record ArmadoApi(Guid HojaDeRutaId, string Estado, List<ParadaArmadaApi> Paradas, List<ExcluidoApi>? Excluidos);

    private record ParadaArmadaApi(int Orden, Guid ParadaId, Guid EnvioId, string CodigoSeguimiento);

    private record ExcluidoApi(Guid EnvioId, string Motivo);

    private record ReasignacionApi(Guid EnvioId, string CodigoSeguimiento, string EstadoEnvio, string Accion, string Mensaje);

    private record HistorialApi(Guid EventoId, Guid EnvioId, string CodigoSeguimiento, string Destinatario, string TipoEvento, DateTime Fecha, string? Detalle, Guid? ResponsableUsuarioId);
}
