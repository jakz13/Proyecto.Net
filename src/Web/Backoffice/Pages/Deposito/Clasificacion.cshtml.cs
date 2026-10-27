using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Deposito;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Deposito;

/// <summary>
/// Modelo de página para la estación de clasificación operativa de bultos en depósito por zona, prioridad y tipo (UC-15).
/// </summary>
[Authorize(Roles = "Operario,Despachador,Administrador")]
public class ClasificacionModel : PageModel
{
    private readonly IDepositoApi _depositoApi;
    private readonly IZonasCoberturaApi _zonasApi;

    public ClasificacionModel(IDepositoApi depositoApi, IZonasCoberturaApi zonasApi)
    {
        _depositoApi = depositoApi ?? throw new ArgumentNullException(nameof(depositoApi));
        _zonasApi = zonasApi ?? throw new ArgumentNullException(nameof(zonasApi));
    }

    public IReadOnlyList<EnvioEnDepositoDto> EnviosEnDeposito { get; private set; } = Array.Empty<EnvioEnDepositoDto>();
    public IReadOnlyList<ZonaCoberturaDto> ZonasDisponibles { get; private set; } = Array.Empty<ZonaCoberturaDto>();

    public EnvioEnDepositoDto? EnvioSeleccionado { get; private set; }

    [BindProperty]
    public FormularioClasificacionEnvioDto Formulario { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? TerminoBusqueda { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Vista { get; set; } = "pendientes"; // "pendientes" o "todos"

    public ResultadoClasificacionDto? Resultado { get; private set; }
    public List<string> ErroresValidacion { get; set; } = new();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public int TotalPendientesClasificar { get; private set; }
    public int TotalListosParaRuta { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id, CancellationToken cancelacion)
    {
        await CargarDatosBaseAsync(cancelacion);

        if (id.HasValue && id.Value != Guid.Empty)
        {
            EnvioSeleccionado = await _depositoApi.ObtenerEnvioParaClasificacionAsync(id.Value, cancelacion);
            if (EnvioSeleccionado != null)
            {
                InicializarFormulario(EnvioSeleccionado);
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancelacion)
    {
        await CargarDatosBaseAsync(cancelacion);

        if (Formulario.EnvioId == Guid.Empty)
        {
            ErroresValidacion.Add("Identificador de envío inválido.");
            return Page();
        }

        EnvioSeleccionado = await _depositoApi.ObtenerEnvioParaClasificacionAsync(Formulario.EnvioId, cancelacion);
        if (EnvioSeleccionado == null)
        {
            TempData["MensajeError"] = "El envío no existe o ya no se encuentra en depósito.";
            return RedirectToPage("/Deposito/Clasificacion");
        }

        if (Formulario.Bultos == null || Formulario.Bultos.Count == 0)
        {
            ErroresValidacion.Add("Debe clasificar al menos un bulto.");
            return Page();
        }

        var items = Formulario.Bultos.Select(b => new ItemClasificacionBultoDto(
            b.CodigoBulto,
            string.IsNullOrWhiteSpace(b.Prioridad) ? "Normal" : b.Prioridad.Trim(),
            b.ZonaCoberturaId.HasValue && b.ZonaCoberturaId.Value != Guid.Empty ? b.ZonaCoberturaId : null,
            string.IsNullOrWhiteSpace(b.Tipo) ? null : b.Tipo.Trim()
        )).ToList();

        var solicitud = new ClasificarBultosRequestDto(items);

        try
        {
            Resultado = await _depositoApi.ClasificarBultosAsync(Formulario.EnvioId, solicitud, cancelacion);

            TempData["MensajeExito"] = $"Clasificación registrada con éxito para el envío '{Resultado.CodigoSeguimiento}'. {(Resultado.ListoParaHojaDeRuta ? "El envío quedó listo para su inclusión en una hoja de ruta." : "Quedan bultos pendientes de clasificar.")}";

            // Refrescar envío seleccionado tras guardar
            EnvioSeleccionado = await _depositoApi.ObtenerEnvioParaClasificacionAsync(Formulario.EnvioId, cancelacion);
            if (EnvioSeleccionado != null)
            {
                InicializarFormulario(EnvioSeleccionado);
            }

            return Page();
        }
        catch (ApiExcepcion ex)
        {
            ErroresValidacion.Add(ex.Message);
            return Page();
        }
    }

    private async Task CargarDatosBaseAsync(CancellationToken cancelacion)
    {
        ZonasDisponibles = await _zonasApi.ListarZonasAsync(null, cancelacion);

        var todos = await _depositoApi.ListarEnviosEnDepositoAsync(cancelacion);
        TotalPendientesClasificar = todos.Count(e => !e.EstaClasificado);
        TotalListosParaRuta = todos.Count(e => e.EstaClasificado);

        if (!string.IsNullOrWhiteSpace(TerminoBusqueda))
        {
            var t = TerminoBusqueda.Trim().ToLowerInvariant();
            todos = todos.Where(e =>
                e.CodigoSeguimiento.ToLowerInvariant().Contains(t) ||
                e.NombreDestinatario.ToLowerInvariant().Contains(t) ||
                e.DireccionDestino.ToLowerInvariant().Contains(t) ||
                e.Bultos.Any(b => b.Identificacion.ToLowerInvariant().Contains(t))
            ).ToList();
        }

        if (Vista == "pendientes")
        {
            EnviosEnDeposito = todos.Where(e => !e.EstaClasificado).ToList();
        }
        else
        {
            EnviosEnDeposito = todos;
        }
    }

    private void InicializarFormulario(EnvioEnDepositoDto envio)
    {
        Formulario = new FormularioClasificacionEnvioDto
        {
            EnvioId = envio.EnvioId,
            Bultos = envio.Bultos.Select(b => new FormularioItemClasificacionDto
            {
                BultoId = b.BultoId,
                CodigoBulto = b.Identificacion,
                Peso = b.Peso,
                EstadoBulto = b.EstadoBulto,
                Prioridad = !string.IsNullOrWhiteSpace(b.Prioridad) ? b.Prioridad : "Normal",
                ZonaCoberturaId = b.ZonaClasificadaId,
                Tipo = !string.IsNullOrWhiteSpace(b.TipoClasificacion) ? b.TipoClasificacion : "Estándar"
            }).ToList()
        };
    }
}

public class FormularioClasificacionEnvioDto
{
    public Guid EnvioId { get; set; }
    public List<FormularioItemClasificacionDto> Bultos { get; set; } = new();
}

public class FormularioItemClasificacionDto
{
    public Guid BultoId { get; set; }
    public string CodigoBulto { get; set; } = string.Empty;
    public decimal Peso { get; set; }
    public string EstadoBulto { get; set; } = string.Empty;
    public string Prioridad { get; set; } = "Normal";
    public Guid? ZonaCoberturaId { get; set; }
    public string? Tipo { get; set; } = "Estándar";
}
