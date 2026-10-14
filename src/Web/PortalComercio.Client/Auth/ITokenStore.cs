namespace UltimaMilla.PortalComercio.Client.Auth;

/// <summary>
/// Contrato para el almacenamiento persistente o en memoria del token JWT de sesión.
/// </summary>
public interface ITokenStore
{
    Task<string?> ObtenerTokenAsync();
    Task GuardarTokenAsync(string token);
    Task EliminarTokenAsync();
}
