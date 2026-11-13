using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

namespace UltimaMilla.Backoffice.Pages.Envios;

/// <summary>
/// Controlador de la vista de gestión de envíos y declaración formal de extravío.
/// </summary>
[Authorize(Roles = "Despachador")]
public class IndexModel : PageModel
{
    private readonly IEnviosApi _enviosApi;
    private readonly IIncidenciasApi _incidenciasApi;

    public IndexModel(IEnviosApi enviosApi, IIncidenciasApi incidenciasApi)
    {
        _enviosApi = enviosApi;
        _incidenciasApi = incidenciasApi;
    }

    public IReadOnlyList<EnvioDto> EnvioLista { get; private set; } = new List<EnvioDto>();
    public IReadOnlyList<IncidenciaDto> IncidenciasAbiertas { get; private set; } = new List<IncidenciaDto>();

    [BindProperty(SupportsGet = true)]
    public string Busqueda { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string EstadoFiltro { get; set; } = "Todos"; // "Todos", "CandidatosExtravio", "EnDeposito", "EnTransito", "Extraviado"

    [BindProperty]
    public DeclararExtraviadoDto DeclaracionInput { get; set; } = new();

    public int TotalRegistrados { get; private set; }
    public int TotalEnDeposito { get; private set; }
    public int TotalEnTransito { get; private set; }
    public int TotalExtraviados { get; private set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        // 1. Obtener todos los envíos con filtro amplio
        var filtro = new FiltroEnviosDto
        {
            Pagina = 1,
            TamanoPagina = 200,
            TerminoBusqueda = string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda.Trim()
        };

        var resultado = await _enviosApi.ListarEnviosAsync(filtro);
        var todos = resultado.Elementos;

        TotalRegistrados = todos.Count;
        TotalEnDeposito = todos.Count(e => e.Estado == EstadoEnvioDto.EnDeposito);
        TotalEnTransito = todos.Count(e => e.Estado == EstadoEnvioDto.EnTransito);
        TotalExtraviados = todos.Count(e => e.Estado == EstadoEnvioDto.Extraviado);

        // 2. Aplicar filtro de estado
        var filtrados = todos.AsEnumerable();

        if (EstadoFiltro == "CandidatosExtravio")
        {
            // Precondición Un envío 'en depósito' o 'en tránsito' no pudo ser ubicado
            filtrados = filtrados.Where(e => e.Estado == EstadoEnvioDto.EnDeposito || e.Estado == EstadoEnvioDto.EnTransito);
        }
        else if (EstadoFiltro == "EnDeposito")
        {
            filtrados = filtrados.Where(e => e.Estado == EstadoEnvioDto.EnDeposito);
        }
        else if (EstadoFiltro == "EnTransito")
        {
            filtrados = filtrados.Where(e => e.Estado == EstadoEnvioDto.EnTransito);
        }
        else if (EstadoFiltro == "Extraviado")
        {
            filtrados = filtrados.Where(e => e.Estado == EstadoEnvioDto.Extraviado);
        }

        EnvioLista = filtrados.ToList();

        // 3. Obtener incidencias abiertas para posible asociación directa
        var filtroInc = new FiltroIncidenciasDto { Estado = "Abierta" };
        IncidenciasAbiertas = await _incidenciasApi.ListarIncidenciasAsync(filtroInc);
    }

    /// <summary>
    /// Declara formalmente el envío como extraviado, registra el evento histórico y emite aviso al comercio.
    /// </summary>
    public async Task<IActionResult> OnPostDeclararExtraviadoAsync()
    {
        if (DeclaracionInput.EnvioId == Guid.Empty || string.IsNullOrWhiteSpace(DeclaracionInput.MotivoExtravio))
        {
            MensajeError = "Debe especificar el motivo y las observaciones del extravío.";
            return RedirectToPage(new { Busqueda, EstadoFiltro });
        }

        var despachador = User.Identity?.Name ?? "Despachador";
        var inputCompleto = DeclaracionInput with { DespachadorUsuario = despachador };

        // Primero se declara el extravío: si la API lo rechaza no se deja una incidencia huérfana
        var resultado = await _enviosApi.DeclararEnvioExtraviadoAsync(inputCompleto);

        if (!resultado.Exito)
        {
            MensajeError = resultado.Mensaje;
            return RedirectToPage(new { Busqueda, EstadoFiltro });
        }

        MensajeExito = $"El envío {resultado.CodigoSeguimiento} ha sido declarado como EXTRAVIADO. El comercio recibe el aviso y el envío sale de su hoja de ruta.";

        // Si solicitó crear una incidencia operativa, se registra en el subsistema de incidencias (la falla no revierte el extravío)
        if (inputCompleto.CrearIncidenciaOperativa && string.IsNullOrEmpty(inputCompleto.NumeroIncidenciaExistente))
        {
            try
            {
                var incCreada = await _incidenciasApi.RegistrarIncidenciaAsync(new RegistrarIncidenciaDto
                {
                    Tipo = "Otro",
                    Severidad = "Alta",
                    EnvioAfectadoId = inputCompleto.EnvioId,
                    CodigoSeguimientoEnvio = inputCompleto.CodigoSeguimiento,
                    Descripcion = $"Envío declarado formalmente como extraviado. Motivo: {inputCompleto.MotivoExtravio}. {inputCompleto.ObservacionesDetalladas}",
                    AccionInmediata = "Se emitió aviso inmediato al comercio y se inició expediente de liquidación/indemnización."
                }, despachador);
                MensajeExito += $" Incidencia operativa asociada: {incCreada.NumeroIncidencia}.";
            }
            catch (Exception)
            {
                MensajeExito += " No se pudo registrar la incidencia operativa asociada: regístrela desde Incidencias.";
            }
        }
        else if (!string.IsNullOrEmpty(inputCompleto.NumeroIncidenciaExistente))
        {
            MensajeExito += $" Incidencia operativa asociada: {inputCompleto.NumeroIncidenciaExistente}.";
        }

        return RedirectToPage(new { Busqueda, EstadoFiltro });
    }

    /// <summary>
    /// Consulta el registro histórico completo de eventos de un envío para visualización en modal.
    /// </summary>
    public async Task<IActionResult> OnGetHistorialAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return new JsonResult(Array.Empty<EventoHistorialEnvioDto>());
        }

        var historial = await _enviosApi.ObtenerHistorialEnvioAsync(id);
        return new JsonResult(historial);
    }
    /// <summary>Evidencias (receptor, firma y foto) de las entregas de un envío, para el modal de trazabilidad.</summary>
    public async Task<IActionResult> OnGetEvidenciasAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return new JsonResult(Array.Empty<EvidenciaEntregaDto>());
        }

        return new JsonResult(await _enviosApi.ObtenerEvidenciasEnvioAsync(id));
    }

    /// <summary>Sirve la imagen de una firma o foto. Pasa por el Backoffice porque la API exige el token de sesión.</summary>
    public async Task<IActionResult> OnGetImagenEvidenciaAsync(Guid id, Guid intento, string tipo)
    {
        if (id == Guid.Empty || intento == Guid.Empty || tipo is not ("firma" or "foto"))
        {
            return BadRequest();
        }

        var imagen = await _enviosApi.ObtenerImagenEvidenciaAsync(id, intento, tipo);
        if (imagen is null)
        {
            return NotFound();
        }

        // Cabecera de privacidad: la firma y la foto son datos personales del receptor, no deben quedar en cachés compartidas.
        Response.Headers.CacheControl = "private, no-store";
        return File(imagen.Contenido, imagen.TipoContenido);
    }

    /// <summary>
    /// Obtiene todos los bultos, códigos de barras y documento de etiquetas imprimibles de un envío sin importar su estado actual.
    /// </summary>
    public async Task<IActionResult> OnGetEtiquetasAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return BadRequest();
        }

        try
        {
            var respuesta = await _enviosApi.GenerarEtiquetasEnviosAsync(new[] { id });
            return new JsonResult(respuesta);
        }
        catch
        {
            var envio = await _enviosApi.ObtenerPorIdAsync(id);
            if (envio is null)
            {
                return NotFound();
            }

            var bultosIds = (envio.BultosIdentificaciones != null && envio.BultosIdentificaciones.Count > 0)
                ? envio.BultosIdentificaciones
                : new[] { $"{envio.CodigoSeguimiento}-B01" };

            var totalBultos = bultosIds.Count;
            var pesoTotal = envio.PesoKg.HasValue && envio.PesoKg.Value > 0 ? envio.PesoKg.Value : 1.0m;
            var pesoPorBulto = Math.Round(pesoTotal / totalBultos, 2);

            var etiquetas = bultosIds.Select((bultoId, idx) => new EtiquetaBultoDto(
                EnvioId: envio.Id,
                CodigoSeguimiento: envio.CodigoSeguimiento,
                IdentificacionBulto: bultoId,
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
                FechaAdmision: envio.FechaCreacion)).ToList();

            var html = GeneradorDocumentoEtiquetas.ConstruirHtmlDescargable(etiquetas);
            return new JsonResult(new ResultadoGeneracionEtiquetasDto(etiquetas, 1, etiquetas.Count, $"etiquetas-{envio.CodigoSeguimiento}.html", html));
        }
    }
}
