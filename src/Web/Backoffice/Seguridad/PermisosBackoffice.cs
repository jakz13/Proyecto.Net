using System.Security.Claims;

namespace UltimaMilla.Backoffice.Seguridad;

/// <summary>
/// Quién puede abrir cada grupo de pantallas. Son las mismas reglas que las políticas de <c>Program.cs</c>
/// (SoloAdministradores, Operaciones, Deposito): las vistas las usan para no ofrecer enlaces ni botones que el perfil no puede usar.
/// </summary>
public static class PermisosBackoffice
{
    public const string Administrador = "Administrador";
    public const string Despachador = "Despachador";
    public const string Operario = "Operario";

    /// <summary>Planificación y gestión de rutas, incidencias, tablero y reportes: administrador y despachador.</summary>
    public static bool PuedeOperar(ClaimsPrincipal usuario) =>
        usuario.IsInRole(Administrador) || usuario.IsInRole(Despachador);

    /// <summary>Administración del operador (comercios, usuarios, tarifas, flota…): solo el administrador.</summary>
    public static bool PuedeAdministrar(ClaimsPrincipal usuario) => usuario.IsInRole(Administrador);
}
