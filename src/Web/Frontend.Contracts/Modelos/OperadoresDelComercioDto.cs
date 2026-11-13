namespace UltimaMilla.Frontend.Contracts.Modelos;

/// <summary>
/// Operadores logísticos con los que trabaja el comercio de la sesión (mismo RUT vinculado a varios operadores)
/// y cuál es el de la sesión actual.
/// </summary>
public record OperadoresDelComercioDto
{
    public string OperadorActualId { get; init; } = string.Empty;
    public bool OperadorActualDadoDeBaja { get; init; }
    public List<IdentidadVisualDto> Operadores { get; init; } = new();
}

/// <summary>Sesión nueva del mismo usuario de comercio, operando con otro de sus operadores.</summary>
public record SesionOperadorDto
{
    public string Token { get; init; } = string.Empty;
    public string OperadorId { get; init; } = string.Empty;
    public DateTime ExpiraUtc { get; init; }
}
