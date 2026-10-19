namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Parámetros para armar, validar y liberar una hoja de ruta por parte del despachador.
/// </summary>
public record ArmarHojaDeRutaDto
{
    public Guid RepartidorId { get; init; }
    public Guid VehiculoId { get; init; }
    public DateOnly Fecha { get; init; }
    public int ViajeNumero { get; init; } = 1;
    public string Turno { get; init; } = "Mañana";
    public string CriterioOrdenamiento { get; init; } = "FranjaHoraria"; // "FranjaHoraria", "OptimoGeografico", "Manual"
    public List<Guid> EnviosIds { get; init; } = new();
    public bool SimularColision { get; init; } = false;
}
