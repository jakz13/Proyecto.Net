namespace UltimaMilla.Frontend.Contracts.Modelos.Usuarios;

/// <summary>
/// Representa un usuario del operador o de un comercio asociado (UC-01, RF-31).
/// </summary>
public record UsuarioDto
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Guid PerfilId { get; init; }
    public string NombrePerfil { get; init; } = string.Empty;
    public Guid? OperadorId { get; init; }
    public Guid? ComercioId { get; init; }
    public string? NombreComercio { get; init; }
    public bool Activo { get; init; } = true;
    public DateTime FechaAlta { get; init; } = DateTime.UtcNow;
}
