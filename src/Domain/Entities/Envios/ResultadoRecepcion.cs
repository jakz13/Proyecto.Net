namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>Resultado de contrastar los bultos escaneados en depósito contra los declarados por el comercio.</summary>
public sealed record ResultadoRecepcion(
    bool Recepcionado,
    IReadOnlyList<string> Correctos,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Sobrantes,
    IReadOnlyList<string> Danados)
{
    public bool TieneDiscrepancias => Faltantes.Count > 0 || Sobrantes.Count > 0 || Danados.Count > 0;
}
