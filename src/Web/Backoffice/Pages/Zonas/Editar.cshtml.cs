using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Zonas;

[Authorize(Roles = "Administrador")]
public class EditarModel : PageModel
{
    private readonly IZonasCoberturaApi _zonasApi;

    public EditarModel(IZonasCoberturaApi zonasApi)
    {
        _zonasApi = zonasApi;
    }

    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public ActualizarZonaCoberturaDto Zona { get; set; } = new();

    [BindProperty]
    public List<FranjaHorariaFormulario> Franjas { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Id = id;
        var entidad = await _zonasApi.ObtenerPorIdAsync(id);
        if (entidad == null)
        {
            return NotFound();
        }

        Zona = new ActualizarZonaCoberturaDto
        {
            Nombre = entidad.Nombre,
            PoligonoGeografico = entidad.PoligonoGeografico,
            ColorHex = entidad.ColorHex,
            Activa = entidad.Activa
        };

        Franjas = entidad.FranjasHorarias.Select(f => new FranjaHorariaFormulario
        {
            DiaSemana = f.DiaSemana.ToString(),
            HoraInicio = f.HoraInicio,
            HoraFin = f.HoraFin,
            CapacidadMaximaEnvios = f.CapacidadMaximaEnvios
        }).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Franjas == null || Franjas.Count == 0)
        {
            ErrorMessage = "Debe conservar al menos una franja horaria de entrega en la zona de cobertura.";
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = FranjasTodosLosDias.ResumirErroresModelState(ModelState);
            return Page();
        }

        // "Todos los días" se convierte en una franja por cada día de la semana sin alterar las filas visibles si luego falla una validación
        var franjasExpandidas = FranjasTodosLosDias.ConvertirYExpandir(Franjas, out var errorDia);
        if (franjasExpandidas is null)
        {
            ErrorMessage = errorDia;
            ModelState.AddModelError(string.Empty, errorDia!);
            return Page();
        }

        // Validación de superposiciones inconsistentes
        var (esValido, mensajeError) = await _zonasApi.ValidarSuperposicionFranjasAsync(franjasExpandidas);
        if (!esValido)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(mensajeError) ? "Superposición horaria inconsistente detectada." : mensajeError;
            ModelState.AddModelError(string.Empty, ErrorMessage);
            return Page();
        }

        try
        {
            var zonaActualizada = Zona with { FranjasHorarias = franjasExpandidas };
            await _zonasApi.ActualizarZonaAsync(Id, zonaActualizada);

            TempData["MensajeExito"] = $"La zona de cobertura '{Zona.Nombre}' y sus {franjasExpandidas.Count} franjas horarias fueron actualizadas exitosamente.";
            return RedirectToPage("/Zonas/Index");
        }
        catch (ApiExcepcion ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al actualizar la zona: {ex.Message}";
            return Page();
        }
    }
}
