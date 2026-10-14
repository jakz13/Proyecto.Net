namespace UltimaMilla.Frontend.Contracts.Modelos.Comercios;

/// <summary>
/// Representa los datos públicos y de gestión de un comercio cliente.
/// </summary>
public record ComercioDto
{
    public Guid Id { get; init; }
    public string RazonSocial { get; init; } = string.Empty;
    public string NombreFantasia { get; init; } = string.Empty;
    public string Rut { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public bool Activo { get; init; } = true;
    public DateTime FechaAlta { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Token de concurrencia optimista para evitar sobreescrituras concurrentes.
    /// </summary>
    public string SelloModificacion { get; init; } = string.Empty;
}
