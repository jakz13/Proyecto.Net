using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Tablero;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Backoffice.Pages.Tablero;

/// <summary>
/// Controlador del tablero de operación en vivo para el monitoreo de flota, alertas de ventana horaria e intervención (UC-12, RF-29).
/// </summary>
[Authorize(Roles = "Despachador,Administrador")]
public class IndexModel : PageModel
{
    private readonly ITableroApi _tableroApi;
    private readonly IConfiguration? _configuracion;
    private readonly MockContextoOperador? _contextoOperador;

    public IndexModel(
        ITableroApi tableroApi,
        IConfiguration? configuracion = null,
        MockContextoOperador? contextoOperador = null)
    {
        _tableroApi = tableroApi;
        _configuracion = configuracion;
        _contextoOperador = contextoOperador;
    }

    public string UrlHubTablero { get; private set; } = string.Empty;

    public ResumenTableroDto Resumen { get; private set; } = new();

    public IReadOnlyList<VehiculoEnVivoDto> VehiculosFiltrados { get; private set; } = new List<VehiculoEnVivoDto>();

    [BindProperty(SupportsGet = true)]
    public string FiltroEstado { get; set; } = "Todos"; // "Todos", "SoloRiesgo", "EnMovimiento", "Detenidos"

    [BindProperty(SupportsGet = true)]
    public string Busqueda { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public Guid? RutaSeleccionadaId { get; set; }

    [TempData]
    public string? MensajeExito { get; set; }

    [TempData]
    public string? MensajeError { get; set; }

    public async Task OnGetAsync()
    {
        UrlHubTablero = ObtenerUrlHub();
        Resumen = await _tableroApi.ObtenerResumenEnVivoAsync();

        var lista = Resumen.Vehiculos.AsEnumerable();

        if (FiltroEstado == "SoloRiesgo")
        {
            lista = lista.Where(v => v.TieneAlertasRiesgo);
        }
        else if (FiltroEstado == "EnMovimiento")
        {
            lista = lista.Where(v => v.EstadoOperativo == "EnMovimiento");
        }
        else if (FiltroEstado == "Detenidos")
        {
            lista = lista.Where(v => v.EstadoOperativo == "DetenidoAlerta" || v.VelocidadKmh == 0);
        }

        if (!string.IsNullOrWhiteSpace(Busqueda))
        {
            var texto = Busqueda.Trim().ToLowerInvariant();
            lista = lista.Where(v =>
                v.VehiculoMatricula.ToLowerInvariant().Contains(texto) ||
                v.RepartidorNombre.ToLowerInvariant().Contains(texto) ||
                v.NumeroRuta.ToLowerInvariant().Contains(texto) ||
                v.ZonaActual.ToLowerInvariant().Contains(texto) ||
                v.EnviosEnRiesgo.Any(e => e.CodigoSeguimiento.ToLowerInvariant().Contains(texto) || e.Destinatario.ToLowerInvariant().Contains(texto)));
        }

        VehiculosFiltrados = lista.ToList();
    }

    /// <summary>
    /// Envía una instrucción operativa o mensaje prioritario al repartidor en su aplicación móvil.
    /// </summary>
    public async Task<IActionResult> OnPostEnviarInstruccionAsync(Guid repartidorId, string mensajeInstruccion)
    {
        if (repartidorId == Guid.Empty || string.IsNullOrWhiteSpace(mensajeInstruccion))
        {
            MensajeError = "Debe especificar un mensaje válido para el repartidor.";
            return RedirectToPage(new { FiltroEstado, Busqueda, RutaSeleccionadaId });
        }

        var exito = await _tableroApi.EnviarInstruccionRepartidorAsync(repartidorId, mensajeInstruccion.Trim());
        if (exito)
        {
            MensajeExito = "Instrucción enviada exitosamente a la terminal móvil del repartidor.";
        }
        else
        {
            MensajeError = "No fue posible contactar la terminal del repartidor.";
        }

        return RedirectToPage(new { FiltroEstado, Busqueda, RutaSeleccionadaId });
    }

    /// <summary>
    /// Endpoint AJAX para actualizar en tiempo real la telemetría del tablero sin recargar la página completa.
    /// </summary>
    public async Task<IActionResult> OnGetDatosEnVivoAsync()
    {
        var datos = await _tableroApi.ObtenerResumenEnVivoAsync();
        return new JsonResult(datos, new JsonSerializerOptions { PropertyNamingPolicy = null });
    }

    /// <summary>
    /// Emite un token JWT de corta duración firmado en el servidor con el <c>OperadorId</c> verificado de la sesión
    /// del Backoffice para autenticar la conexión SignalR hacia <c>/hubs/tablero</c>.
    /// </summary>
    public IActionResult OnGetTokenTiempoReal()
    {
        var operadorId = ResolverOperadorDeSesion();
        var usuarioIdTexto = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var usuarioId = Guid.TryParse(usuarioIdTexto, out var uid) && uid != Guid.Empty ? uid : Guid.NewGuid();
        var nombre = User.Identity?.Name ?? "Despachador Backoffice";
        var rol = User.FindFirst(ClaimTypes.Role)?.Value ?? "Despachador";
        var perfilId = string.Equals(rol, "Administrador", StringComparison.OrdinalIgnoreCase)
            ? DatabaseSeeder.DefaultPerfilAdminId
            : DatabaseSeeder.DefaultPerfilDespachadorId;

        var usuario = new Usuario(usuarioId, nombre, "tablero@ultimamilla.com.uy", "sesion-backoffice", perfilId, operadorId: operadorId);
        var perfil = new Perfil(perfilId, rol, "Sesión autenticada de Backoffice");
        var servicioJwt = new TokenJwtService(_configuracion ?? new ConfigurationBuilder().Build());
        var token = servicioJwt.GenerarToken(usuario, perfil, new[] { "RUTAS_DESPACHO" }, TimeSpan.FromMinutes(15));

        return new JsonResult(new
        {
            accessToken = token.Token,
            expiraEnUtc = token.ExpiraUtc,
            hubUrl = ObtenerUrlHub()
        });
    }

    private string ObtenerUrlHub()
    {
        var baseUrl = _configuracion?["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
        return $"{baseUrl.TrimEnd('/')}/hubs/tablero";
    }

    private Guid ResolverOperadorDeSesion()
    {
        var claimTenant = User.FindFirst("tenant_id")?.Value ?? User.FindFirst("operador_id")?.Value;
        if (!string.IsNullOrWhiteSpace(claimTenant))
            return LoginModel.ResolverOperadorGuid(claimTenant);

        return LoginModel.ResolverOperadorGuid(_contextoOperador?.OperadorActivo.OperadorId);
    }
}
