using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Retiros;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Backoffice.Pages.Rutas;

[Authorize(Roles = "Despachador")]
public class ArmarModel : PageModel
{
    private readonly IRutasApi _rutasApi;
    private readonly IFlotaApi _flotaApi;
    private readonly IRetirosApi _retirosApi;
    private readonly IZonasCoberturaApi? _zonasApi;

    public ArmarModel(IRutasApi rutasApi, IFlotaApi flotaApi, IRetirosApi retirosApi, IZonasCoberturaApi? zonasApi = null)
    {
        _rutasApi = rutasApi;
        _flotaApi = flotaApi;
        _retirosApi = retirosApi;
        _zonasApi = zonasApi;
    }

    /// <summary>Retiros en domicilio pendientes de asignar: pueden sumarse a la ruta, que pasa a ser mixta.</summary>
    public IReadOnlyList<RetiroResumenDto> RetirosPendientes { get; private set; } = new List<RetiroResumenDto>();

    public IReadOnlyList<string> ZonasDisponibles { get; private set; } = new List<string>();

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

        // Valores por defecto: la fecha es la de hoy en la hora local de la operación (no la UTC)
        if (Formulario.Fecha == default)
        {
            Formulario.Fecha = FechaDeHoy;
        }

        // Se sugiere un repartidor con vehículo asignado: la hoja de ruta siempre usa el vehículo del repartidor
        var primerRepartidor = RepartidoresDisponibles.FirstOrDefault(r => r.VehiculoAsignadoId.HasValue) ?? RepartidoresDisponibles.FirstOrDefault();
        if (primerRepartidor != null)
        {
            Formulario.RepartidorId = primerRepartidor.Id;
            Formulario.VehiculoId = primerRepartidor.VehiculoAsignadoId ?? Guid.Empty;
        }
    }

    /// <summary>Fecha de hoy en la hora local de la operación.</summary>
    public static DateOnly FechaDeHoy => UltimaMilla.Application.Queries.Tablero.ZonaHorariaOperacion.FechaLocalDe(DateTime.UtcNow);

    /// <summary>Minutos transcurridos del día en la hora local de la operación (la pantalla deshabilita los turnos que ya terminaron).</summary>
    public static int MinutosLocalesDelDia
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UltimaMilla.Application.Queries.Tablero.ZonaHorariaOperacion.Zona);
            return local.Hour * 60 + local.Minute;
        }
    }

    /// <summary>Hora de fin de cada turno, en minutos del día (debe coincidir con la regla del servidor).</summary>
    public static int FinDelTurnoEnMinutos(string? turno) => turno switch { "Mañana" => 13 * 60, _ => 18 * 60 };

    public async Task<IActionResult> OnPostConfirmarAsync()
    {
        await CargarCatalogosAsync();

        if (Formulario.EnviosSeleccionados.Count == 0 && Formulario.RetirosSeleccionados.Count == 0)
        {
            ModelState.AddModelError("", "Debe seleccionar al menos un envío o un retiro para incluir en la hoja de ruta.");
            return Page();
        }

        // El vehículo no se elige: es el que tiene asignado el repartidor (no se confía en lo que traiga el formulario)
        var repartidor = RepartidoresDisponibles.FirstOrDefault(r => r.Id == Formulario.RepartidorId);
        if (repartidor is null)
        {
            ModelState.AddModelError("", "Seleccione un repartidor disponible.");
            return Page();
        }

        if (!repartidor.VehiculoAsignadoId.HasValue)
        {
            ModelState.AddModelError("", $"{repartidor.NombreCompleto} no tiene un vehículo asignado: asígnele uno en Repartidores y Vehículos antes de armar su hoja de ruta.");
            return Page();
        }

        Formulario.VehiculoId = repartidor.VehiculoAsignadoId.Value;

        if (Formulario.Fecha == FechaDeHoy && MinutosLocalesDelDia >= FinDelTurnoEnMinutos(Formulario.Turno))
        {
            ModelState.AddModelError("", "No es posible crear una hoja de ruta para una franja horaria anterior a la hora actual.");
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
            RetirosIds = Formulario.RetirosSeleccionados
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
        RetirosPendientes = (await _retirosApi.ListarDelOperadorAsync("Solicitado")).Items;

        var zonasDeEnvios = EnviosEnDeposito.Select(e => e.Zona).Where(z => !string.IsNullOrWhiteSpace(z) && z != "Sin zona");
        var zonasConfiguradas = Enumerable.Empty<string>();
        if (_zonasApi is not null)
        {
            try
            {
                zonasConfiguradas = (await _zonasApi.ListarZonasAsync(new UltimaMilla.Frontend.Contracts.Modelos.Zonas.FiltroZonasDto { SoloActivas = true }))
                    .Select(z => z.Nombre);
            }
            catch
            {
            }
        }
        ZonasDisponibles = zonasConfiguradas.Concat(zonasDeEnvios).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(z => z).ToList();
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
        public List<Guid> RetirosSeleccionados { get; set; } = new();
    }
}
