namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Representa un rol o perfil del sistema.
/// </summary>
public record PerfilDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
}
