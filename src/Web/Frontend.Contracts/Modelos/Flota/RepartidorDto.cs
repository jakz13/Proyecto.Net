namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Representa a un repartidor registrado en el operador logístico (RF-06).
/// </summary>
public record RepartidorDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UsuarioId { get; init; } = Guid.NewGuid();
    public string OperadorId { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Documento { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Estado { get; init; } = "Disponible";
    public Guid? VehiculoAsignadoId { get; init; }
    public string? VehiculoPatente { get; init; }
    public string? VehiculoTipo { get; init; }
    public string? VehiculoCapacidad { get; init; }
}
