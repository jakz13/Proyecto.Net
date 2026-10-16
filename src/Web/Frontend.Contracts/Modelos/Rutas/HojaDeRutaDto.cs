namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Representa una hoja de ruta con sus recursos asignados y lista de paradas ordenadas.
/// </summary>
public record HojaDeRutaDto
{
    public Guid Id { get; init; }
    public string NumeroRuta { get; init; } = string.Empty;
    public DateOnly Fecha { get; init; }
    public int ViajeNumero { get; init; } = 1;
    public string Turno { get; init; } = "Mañana"; // "Mañana", "Tarde", "Jornada Completa"
    public string Estado { get; init; } = "Liberada"; // "Borrador", "Liberada", "EnCarga", "Despachada", "EnTransito", "Finalizada"
    
    public Guid RepartidorId { get; init; }
    public string RepartidorNombre { get; init; } = string.Empty;

    public Guid VehiculoId { get; init; }
    public string VehiculoMatricula { get; init; } = string.Empty;
    public string VehiculoModelo { get; init; } = string.Empty;
    public decimal CapacidadPesoMaxKg { get; init; }
    public decimal CapacidadVolumenMaxM3 { get; init; }

    public decimal PesoTotalKg { get; init; }
    public decimal VolumenTotalM3 { get; init; }
    public int CantidadParadas { get; init; }

    public decimal PorcentajeOcupacionPeso => CapacidadPesoMaxKg > 0 
        ? Math.Min(100, Math.Round((PesoTotalKg / CapacidadPesoMaxKg) * 100, 1)) 
        : 0;

    public decimal PorcentajeOcupacionVolumen => CapacidadVolumenMaxM3 > 0 
        ? Math.Min(100, Math.Round((VolumenTotalM3 / CapacidadVolumenMaxM3) * 100, 1)) 
        : 0;

    public List<ParadaRutaDto> Paradas { get; init; } = new();

    public DateTime FechaCreacion { get; init; }
    public DateTime? FechaLiberacion { get; init; }
    public DateTime? FechaDespacho { get; init; }
    public string? OperarioDespacho { get; init; }
    public string CriterioOrdenamiento { get; init; } = "FranjaHoraria";
    public string SelloModificacion { get; init; } = string.Empty;
}
