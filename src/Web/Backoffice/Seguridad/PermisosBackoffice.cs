using System.Security.Claims;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Quién puede abrir cada grupo de pantallas. Son las mismas reglas que las políticas de <c>Program.cs</c>
/// (SoloAdministradores, Operaciones, Deposito, Despacho, Supervision): las vistas las usan para no ofrecer enlaces ni botones que el perfil no puede usar.
/// </summary>
public static class PermisosBackoffice
{
    public const string Administrador = "Administrador";
    public const string Despachador = "Despachador";
    public const string Operario = "Operario";

    /// <summary>Planificación y gestión de rutas, incidencias y tablero: solo el despachador.</summary>
    public static bool PuedeOperar(ClaimsPrincipal usuario) => usuario.IsInRole(Despachador);

    /// <summary>Administración del operador (comercios, usuarios, tarifas, flota…): solo el administrador.</summary>
    public static bool PuedeAdministrar(ClaimsPrincipal usuario) => usuario.IsInRole(Administrador);

    /// <summary>Trabajo de depósito (recepción, clasificación, rendición): solo el operario.</summary>
    public static bool PuedeTrabajarEnDeposito(ClaimsPrincipal usuario) => usuario.IsInRole(Operario);

    /// <summary>Control del despacho y retiros: despachador y operario.</summary>
    public static bool PuedeDespachar(ClaimsPrincipal usuario) => usuario.IsInRole(Despachador) || usuario.IsInRole(Operario);

    /// <summary>Reportes de gestión: administrador y despachador.</summary>
    public static bool PuedeSupervisar(ClaimsPrincipal usuario) => usuario.IsInRole(Administrador) || usuario.IsInRole(Despachador);
}
