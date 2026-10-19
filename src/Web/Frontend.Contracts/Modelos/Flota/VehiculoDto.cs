namespace UltimaMilla.Frontend.Contracts.Modelos.Flota;

/// <summary>
/// Representa un vehículo de la flota del operador para planificación y ejecución de entregas (RF-06).
/// </summary>
public record VehiculoDto
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string OperadorId { get; init; } = string.Empty;
    public string Patente { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public decimal CapacidadPeso { get; init; }
    public decimal CapacidadVolumen { get; init; }
    public bool Activo { get; init; } = true;
    public Guid? RepartidorAsignadoId { get; init; }
    public string? RepartidorAsignadoNombre { get; init; }
}
