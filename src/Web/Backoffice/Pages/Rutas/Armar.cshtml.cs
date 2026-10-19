using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Rutas;

[Authorize(Roles = "Despachador,Administrador")]
public class ArmarModel : PageModel
{
    private readonly IRutasApi _rutasApi;
    private readonly IFlotaApi _flotaApi;

    public ArmarModel(IRutasApi rutasApi, IFlotaApi flotaApi)
    {
        _rutasApi = rutasApi;
        _flotaApi = flotaApi;
    }

    [BindProperty]
    public ArmarRutaFormInput Formulario { get; set; } = new();

    public IReadOnlyList<RepartidorDto> RepartidoresDisponibles { get; private set; } = new List<RepartidorDto>();
    public IReadOnlyList<VehiculoDto> VehiculosDisponibles { get; private set; } = new List<VehiculoDto>();
    public IReadOnlyList<EnvioPendienteRutaDto> EnviosEnDeposito { get; private set; } = new List<EnvioPendienteRutaDto>();

    public List<string> ErroresValidacion { get; private set; } = new();
    public List<string> AdvertenciasValidacion { get; private set; } = new();
    public bool ColisionDetectada { get; private set; }

    public async Task OnGetAsync()
    {
        await CargarCatalogosAsync();

        // Valores por defecto
        if (Formulario.Fecha == default)
        {
            Formulario.Fecha = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        // Si hay un repartidor que ya tiene un vehículo habitual, sugerirlo
        var primerRepartidor = RepartidoresDisponibles.FirstOrDefault(r => r.VehiculoAsignadoId.HasValue);
        if (primerRepartidor != null)
        {
            Formulario.RepartidorId = primerRepartidor.Id;
            Formulario.VehiculoId = primerRepartidor.VehiculoAsignadoId!.Value;
        }
        else if (RepartidoresDisponibles.Count > 0 && VehiculosDisponibles.Count > 0)
        {
            Formulario.RepartidorId = RepartidoresDisponibles[0].Id;
            Formulario.VehiculoId = VehiculosDisponibles[0].Id;
        }
    }

    public async Task<IActionResult> OnPostConfirmarAsync()
    {
        await CargarCatalogosAsync();

        if (Formulario.EnviosSeleccionados.Count == 0)
        {
            ModelState.AddModelError("", "Debe seleccionar al menos un envío para incluir en la hoja de ruta.");
            return Page();
        }

        var dto = new ArmarHojaDeRutaDto
        {
            RepartidorId = Formulario.RepartidorId,
            VehiculoId = Formulario.VehiculoId,
            Fecha = Formulario.Fecha,
            ViajeNumero = Formulario.ViajeNumero,
            Turno = Formulario.Turno,
            CriterioOrdenamiento = Formulario.CriterioOrdenamiento,
            EnviosIds = Formulario.EnviosSeleccionados,
            SimularColision = Formulario.SimularColision
        };

        var resultado = await _rutasApi.ArmarYConfirmarRutaAsync(dto);

        if (!resultado.Exitoso)
        {
            ErroresValidacion = resultado.ErroresValidacion;
            AdvertenciasValidacion = resultado.Advertencias;
            ColisionDetectada = resultado.ColisionDetectada;
            return Page();
        }

        TempData["MensajeExito"] = resultado.Mensaje;
        return RedirectToPage("/Rutas/Detalle", new { id = resultado.HojaDeRuta!.Id });
    }

    public async Task<IActionResult> OnPostValidarAsync([FromBody] ArmarHojaDeRutaDto dto)
    {
        var resultado = await _rutasApi.ValidarArmadoRutaAsync(dto);
        return new JsonResult(resultado);
    }

    private async Task CargarCatalogosAsync()
    {
        RepartidoresDisponibles = await _flotaApi.ListarRepartidoresAsync(estado: "Disponible");
        VehiculosDisponibles = await _flotaApi.ListarVehiculosAsync(soloActivos: true);
        EnviosEnDeposito = await _rutasApi.ListarEnviosPendientesAsync();
    }

    public class ArmarRutaFormInput
    {
        public Guid RepartidorId { get; set; }
        public Guid VehiculoId { get; set; }
        public DateOnly Fecha { get; set; }
        public int ViajeNumero { get; set; } = 1;
        public string Turno { get; set; } = "Mañana";
        public string CriterioOrdenamiento { get; set; } = "FranjaHoraria";
        public List<Guid> EnviosSeleccionados { get; set; } = new();
        public bool SimularColision { get; set; } = false;
    }
}
