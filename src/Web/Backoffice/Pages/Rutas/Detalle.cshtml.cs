using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;

namespace UltimaMilla.Backoffice.Pages.Rutas;

/// <summary>
/// Controlador de la vista de detalle de hoja de ruta y reasignación / reprogramación de envíos.
/// </summary>
[Authorize(Roles = "Despachador")]
public class DetalleModel : PageModel
{
    private readonly IRutasApi _rutasApi;
    private readonly IMotivosNoEntregaApi _motivosNoEntregaApi;
    private readonly ITableroApi _tableroApi;
    private readonly IEnviosApi? _enviosApi;

    public DetalleModel(IRutasApi rutasApi, IMotivosNoEntregaApi motivosNoEntregaApi, ITableroApi tableroApi, IEnviosApi? enviosApi = null)
    {
        _rutasApi = rutasApi;
        _motivosNoEntregaApi = motivosNoEntregaApi;
        _tableroApi = tableroApi;
        _enviosApi = enviosApi;
    }

    public HojaDeRutaDto? HojaDeRuta { get; private set; }

    /// <summary>
    /// Otras hojas de ruta activas (liberadas o borrador) candidatas a recibir un envío reasignado.
    /// </summary>
    public IReadOnlyList<HojaDeRutaDto> OtrasRutasActivas { get; private set; } = new List<HojaDeRutaDto>();

    /// <summary>
    /// Catálogo interno de motivos de no entrega para incidencias en ruta despachada.
    /// </summary>
    public IReadOnlyList<MotivoNoEntregaDto> MotivosCatalogo { get; private set; } = new List<MotivoNoEntregaDto>();

    /// <summary>
    /// Historial de auditoría de reasignaciones e incidencias de esta hoja de ruta.
    /// </summary>
    public IReadOnlyList<EventoReasignacionDto> HistorialEventos { get; private set; } = new List<EventoReasignacionDto>();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    [TempData]
    public string? NotificacionRepartidor { get; set; }

    [BindProperty]
    public ReasignarEnvioDto ReasignacionInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return RedirectToPage("/Rutas/Index");
        }

        HojaDeRuta = await _rutasApi.ObtenerHojaDeRutaPorIdAsync(id);
        if (HojaDeRuta == null)
        {
            return RedirectToPage("/Rutas/Index");
        }

        await CargarDatosComplementariosAsync(id);
        return Page();
    }

    /// <summary>Última posición reportada por el vehículo de la ruta; la página la consulta para mover el camión en el mapa sin recargar.</summary>
    public async Task<IActionResult> OnGetPosicionAsync(Guid id)
    {
        VehiculoEnVivoDto? vivo = null;
        try { vivo = await _tableroApi.ObtenerDetalleVehiculoEnVivoAsync(id); }
        catch { /* sin conexión con la API: la página conserva la posición que ya mostraba */ }

        if (vivo is null)
            return new JsonResult(new { disponible = false });

        return new JsonResult(new
        {
            disponible = true,
            vehiculoId = vivo.VehiculoId,
            latitud = vivo.Latitud,
            longitud = vivo.Longitud,
            velocidadKmh = vivo.VelocidadKmh,
            ultimaActualizacion = vivo.UltimaActualizacion
        });
    }

    public async Task<IActionResult> OnPostReasignarAsync(Guid id)
    {
        return await ProcesarReasignacionAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        return await ProcesarReasignacionAsync(id);
    }

    private async Task<IActionResult> ProcesarReasignacionAsync(Guid id)
    {
        if (id != Guid.Empty && ReasignacionInput.HojaDeRutaActualId == Guid.Empty)
        {
            ReasignacionInput = ReasignacionInput with { HojaDeRutaActualId = id };
        }

        if (ReasignacionInput.HojaDeRutaActualId == Guid.Empty || ReasignacionInput.EnvioId == Guid.Empty)
        {
            MensajeError = "Datos de envío u hoja de ruta incompletos para procesar la acción.";
            return RedirectToPage("/Rutas/Detalle", new { id });
        }

        var despachadorUsuario = User.Identity?.Name ?? "despachador.central";

        var resultado = await _rutasApi.ReasignarOReprogramarEnvioAsync(
            ReasignacionInput,
            despachadorUsuario,
            HttpContext.RequestAborted);

        if (resultado.Exitoso)
        {
            MensajeExito = resultado.Mensaje;
            NotificacionRepartidor = resultado.NotificacionRepartidor;
        }
        else
        {
            MensajeError = resultado.Mensaje;
        }

        return RedirectToPage("/Rutas/Detalle", new { id = ReasignacionInput.HojaDeRutaActualId });
    }

    private async Task CargarDatosComplementariosAsync(Guid id)
    {
        // 1. Obtener otras rutas activas donde reasignar envíos
        var todasLasRutas = await _rutasApi.ListarHojasDeRutaAsync();
        OtrasRutasActivas = todasLasRutas
            .Where(r => r.Id != id && (r.Estado == "Liberada" || r.Estado == "Borrador"))
            .ToList();

        // 2. Obtener catálogo oficial de motivos de no entrega activos
        var motivos = await _motivosNoEntregaApi.ListarMotivosAsync();
        MotivosCatalogo = motivos.Where(m => m.Activo).ToList();

        // 3. Obtener log histórico de eventos de reasignación
        HistorialEventos = await _rutasApi.ListarEventosReasignacionAsync(id);
    }

    /// <summary>
    /// Devuelve las etiquetas y códigos de barras de todos los bultos de un envío o de todas las paradas de la hoja de ruta.
    /// </summary>
    public async Task<IActionResult> OnGetEtiquetasAsync(Guid id, Guid? envioId = null)
    {
        var idsEnvios = new List<Guid>();
        if (envioId.HasValue && envioId.Value != Guid.Empty)
        {
            idsEnvios.Add(envioId.Value);
        }
        else if (id != Guid.Empty)
        {
            var ruta = await _rutasApi.ObtenerHojaDeRutaPorIdAsync(id);
            if (ruta?.Paradas != null)
            {
                idsEnvios.AddRange(ruta.Paradas.Where(p => p.EnvioId != Guid.Empty).Select(p => p.EnvioId).Distinct());
            }
        }

        if (idsEnvios.Count == 0)
        {
            return BadRequest();
        }

        if (_enviosApi is not null)
        {
            try
            {
                var respuesta = await _enviosApi.GenerarEtiquetasEnviosAsync(idsEnvios, HttpContext.RequestAborted);
                return new JsonResult(respuesta);
            }
            catch
            {
                // Fallback a construcción local por envío si la consulta masiva falla
            }
        }

        var rutaActual = id != Guid.Empty ? await _rutasApi.ObtenerHojaDeRutaPorIdAsync(id) : null;
        var etiquetasFallback = new List<EtiquetaBultoDto>();

        foreach (var eid in idsEnvios)
        {
            var envio = _enviosApi is not null ? await _enviosApi.ObtenerPorIdAsync(eid) : null;
            if (envio is not null)
            {
                var bultosIds = (envio.BultosIdentificaciones != null && envio.BultosIdentificaciones.Count > 0)
                    ? envio.BultosIdentificaciones
                    : new[] { $"{envio.CodigoSeguimiento}-B01" };

                var totalBultos = bultosIds.Count;
                var pesoTotal = envio.PesoKg.HasValue && envio.PesoKg.Value > 0 ? envio.PesoKg.Value : 1.0m;
                var pesoPorBulto = Math.Round(pesoTotal / totalBultos, 2);

                for (var idx = 0; idx < totalBultos; idx++)
                {
                    etiquetasFallback.Add(new EtiquetaBultoDto(
                        EnvioId: envio.Id,
                        CodigoSeguimiento: envio.CodigoSeguimiento,
                        IdentificacionBulto: bultosIds[idx],
                        NumeroBulto: idx + 1,
                        TotalBultos: totalBultos,
                        ComercioRemitente: string.IsNullOrWhiteSpace(envio.ComercioNombre) ? "Comercio Adherido" : envio.ComercioNombre,
                        OperadorLogistico: "Última Milla Express",
                        NombreDestinatario: envio.NombreDestinatario,
                        DireccionDestino: envio.DireccionDestino,
                        TelefonoDestinatario: envio.TelefonoDestinatario,
                        ModalidadServicio: string.IsNullOrWhiteSpace(envio.ModalidadServicio) ? "Estándar" : envio.ModalidadServicio,
                        PesoKg: pesoPorBulto,
                        Observaciones: envio.Observaciones,
                        FechaAdmision: envio.FechaCreacion));
                }
            }
            else if (rutaActual?.Paradas != null)
            {
                var parada = rutaActual.Paradas.FirstOrDefault(p => p.EnvioId == eid);
                if (parada is not null)
                {
                    etiquetasFallback.Add(new EtiquetaBultoDto(
                        EnvioId: parada.EnvioId,
                        CodigoSeguimiento: parada.CodigoSeguimiento,
                        IdentificacionBulto: $"{parada.CodigoSeguimiento}-B01",
                        NumeroBulto: 1,
                        TotalBultos: 1,
                        ComercioRemitente: "Comercio Adherido",
                        OperadorLogistico: "Última Milla Express",
                        NombreDestinatario: parada.Destinatario,
                        DireccionDestino: parada.Direccion,
                        TelefonoDestinatario: null,
                        ModalidadServicio: parada.FranjaHoraria,
                        PesoKg: parada.PesoKg > 0 ? parada.PesoKg : 1.0m,
                        Observaciones: null,
                        FechaAdmision: DateTime.UtcNow));
                }
            }
        }

        var html = GeneradorDocumentoEtiquetas.ConstruirHtmlDescargable(etiquetasFallback);
        return new JsonResult(new ResultadoGeneracionEtiquetasDto(etiquetasFallback, idsEnvios.Count, etiquetasFallback.Count, "etiquetas-ruta.html", html));
    }
}
