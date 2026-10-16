namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Resultado del proceso de validación o armado de una hoja de ruta.
/// </summary>
public record ResultadoArmadoRutaDto
{
    public bool Exitoso { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public HojaDeRutaDto? HojaDeRuta { get; init; }
    public List<string> ErroresValidacion { get; init; } = new();
    public List<string> Advertencias { get; init; } = new();
    public bool ColisionDetectada { get; init; }
}
