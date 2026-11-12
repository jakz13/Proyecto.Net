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

        var despachador = User.Identity?.Name ?? "Despachador Juan";
        var inputCompleto = DeclaracionInput with { DespachadorUsuario = despachador };

        // Si solicitó crear una incidencia operativa, podemos registrarla en el subsistema de incidencias
        string? numIncidencia = inputCompleto.NumeroIncidenciaExistente;
        if (inputCompleto.CrearIncidenciaOperativa && string.IsNullOrEmpty(numIncidencia))
        {
            var nuevaInc = new RegistrarIncidenciaDto
            {
                Tipo = "PaqueteDanado", // Categoría anómala de paquete
                Severidad = "Alta",
                CodigoSeguimientoEnvio = inputCompleto.CodigoSeguimiento,
                Descripcion = $"Envío declarado formalmente como extraviado. Motivo: {inputCompleto.MotivoExtravio}. {inputCompleto.ObservacionesDetalladas}",
                AccionInmediata = "Se emitió aviso inmediato al comercio y se inició expediente de liquidación/indemnización."
            };

            var incCreada = await _incidenciasApi.RegistrarIncidenciaAsync(nuevaInc, despachador);
            numIncidencia = incCreada.NumeroIncidencia;
            inputCompleto = inputCompleto with { NumeroIncidenciaExistente = numIncidencia };
        }

        var resultado = await _enviosApi.DeclararEnvioExtraviadoAsync(inputCompleto);

        if (resultado.Exito)
        {
            MensajeExito = $"El envío {resultado.CodigoSeguimiento} ha sido declarado como EXTRAVIADO. Evento de trazabilidad registrado y aviso emitido al comercio.";
            if (!string.IsNullOrEmpty(resultado.NumeroIncidenciaGenerada))
            {
                MensajeExito += $" Incidencia operativa asociada: {resultado.NumeroIncidenciaGenerada}.";
            }
        }
        else
        {
            MensajeError = resultado.Mensaje;
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
}
