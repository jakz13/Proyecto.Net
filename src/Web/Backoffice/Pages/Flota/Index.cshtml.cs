using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Backoffice.Pages.Flota;

/// <summary>
/// Modelo de página para el caso de uso UC-07: Gestionar repartidores y vehículos.
/// Permite al Administrador dar de alta vehículos con su capacidad de peso y volumen,
/// dar de alta repartidores y opcionalmente asociarlos a un vehículo para la planificación de rutas (RF-06).
/// </summary>
[Authorize(Roles = "Administrador")]
public class IndexModel : PageModel
{
    private readonly IFlotaApi _flotaApi;

    public IndexModel(IFlotaApi flotaApi)
    {
        _flotaApi = flotaApi;
    }

    public IReadOnlyList<VehiculoDto> Vehiculos { get; set; } = Array.Empty<VehiculoDto>();
    public IReadOnlyList<RepartidorDto> Repartidores { get; set; } = Array.Empty<RepartidorDto>();

    [BindProperty]
    public CrearVehiculoDto NuevoVehiculo { get; set; } = new();

    [BindProperty]
    public ActualizarVehiculoDto EdicionVehiculo { get; set; } = new();

    [BindProperty]
    public CrearRepartidorDto NuevoRepartidor { get; set; } = new();

    [BindProperty]
    public ActualizarRepartidorDto EdicionRepartidor { get; set; } = new();

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public int VehiculosActivos => Vehiculos.Count(v => v.Activo);
    public decimal CapacidadPesoTotalKg => Vehiculos.Where(v => v.Activo).Sum(v => v.CapacidadPeso);
    public int RepartidoresActivos => Repartidores.Count(r => r.Estado != "Inactivo");
    public int RepartidoresConVehiculo => Repartidores.Count(r => r.VehiculoAsignadoId.HasValue);

    public async Task OnGetAsync()
    {
        await CargarDatosAsync();
    }

    // =========================================================================
    // Handlers de Vehículos
    // =========================================================================

    public async Task<IActionResult> OnPostCrearVehiculoAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(NuevoVehiculo, nameof(NuevoVehiculo)))
        {
            MensajeError = "Por favor verifica los datos ingresados para el vehículo.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var creado = await _flotaApi.CrearVehiculoAsync(NuevoVehiculo);
            MensajeExito = $"Vehículo con patente '{creado.Patente}' dado de alta exitosamente con capacidad de {creado.CapacidadPeso:N0} kg y {creado.CapacidadVolumen:N1} m³.";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al registrar vehículo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEditarVehiculoAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(EdicionVehiculo, nameof(EdicionVehiculo)))
        {
            MensajeError = "Por favor verifica los datos ingresados para actualizar el vehículo.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var actualizado = await _flotaApi.ActualizarVehiculoAsync(EdicionVehiculo);
            MensajeExito = $"Vehículo '{actualizado.Patente}' actualizado exitosamente.";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al actualizar vehículo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCambiarEstadoVehiculoAsync(Guid id, bool activo)
    {
        try
        {
            await _flotaApi.CambiarEstadoVehiculoAsync(id, activo);
            MensajeExito = activo
                ? "El vehículo ha sido activado y está disponible para la flota."
                : "El vehículo fue desactivado y retirado de cualquier asignación activa.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al cambiar estado del vehículo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    // =========================================================================
    // Handlers de Repartidores
    // =========================================================================

    public async Task<IActionResult> OnPostCrearRepartidorAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(NuevoRepartidor, nameof(NuevoRepartidor)))
        {
            MensajeError = "Por favor verifica los campos obligatorios del repartidor.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var creado = await _flotaApi.CrearRepartidorAsync(NuevoRepartidor);
            MensajeExito = creado.VehiculoAsignadoId.HasValue
                ? $"Repartidor '{creado.NombreCompleto}' dado de alta y asociado al vehículo '{creado.VehiculoPatente}'."
                : $"Repartidor '{creado.NombreCompleto}' dado de alta correctamente.";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al registrar repartidor: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEditarRepartidorAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(EdicionRepartidor, nameof(EdicionRepartidor)))
        {
            MensajeError = "Por favor verifica los datos al actualizar el repartidor.";
            await CargarDatosAsync();
            return Page();
        }

        try
        {
            var actualizado = await _flotaApi.ActualizarRepartidorAsync(EdicionRepartidor);
            MensajeExito = $"Datos del repartidor '{actualizado.NombreCompleto}' actualizados exitosamente.";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al actualizar repartidor: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsignarVehiculoAsync(Guid repartidorId, Guid? vehiculoId)
    {
        try
        {
            await _flotaApi.AsignarVehiculoAsync(repartidorId, vehiculoId);
            MensajeExito = vehiculoId.HasValue
                ? "Vehículo asociado exitosamente al repartidor para la asignación de rutas."
                : "Vehículo desasociado del repartidor.";
            return RedirectToPage();
        }
        catch (ApiExcepcion ex)
        {
            MensajeError = ex.Message;
            await CargarDatosAsync();
            return Page();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al modificar asignación de vehículo: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCambiarEstadoRepartidorAsync(Guid repartidorId, string nuevoEstado)
    {
        try
        {
            await _flotaApi.CambiarEstadoRepartidorAsync(repartidorId, nuevoEstado);
            MensajeExito = $"Estado del repartidor actualizado a '{nuevoEstado}'.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al actualizar estado del repartidor: {ex.Message}";
            await CargarDatosAsync();
            return Page();
        }
    }

    private async Task CargarDatosAsync()
    {
        Vehiculos = await _flotaApi.ListarVehiculosAsync();
        Repartidores = await _flotaApi.ListarRepartidoresAsync();
    }
}
