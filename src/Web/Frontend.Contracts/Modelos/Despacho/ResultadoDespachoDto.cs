namespace UltimaMilla.Frontend.Contracts.Modelos.Despacho;

/// <summary>
/// Resultado de la confirmación de despacho de la hoja de ruta.
/// </summary>
public record ResultadoDespachoDto
{
    public bool Exitoso { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public Guid HojaDeRutaId { get; init; }
    public string NumeroRuta { get; init; } = string.Empty;
    public int EnviosEnTransitoCount { get; init; }
    public int EnviosReintegradosDepositoCount { get; init; }
    public List<string> EnviosReintegrados { get; init; } = new();
    public List<string> AvisosAlDespachador { get; init; } = new();
    public List<string> Errores { get; init; } = new();
}
