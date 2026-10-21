namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Resultado del procesamiento de un archivo de importación masiva de envíos (UC-25, RF-07).
/// </summary>
public record ResultadoImportacionDto(
    Guid LoteId,
    string ClaveIdempotencia,
    bool Reimportacion,
    int TotalRegistros,
    int Creados,
    int Duplicados,
    int ConError,
    IReadOnlyList<ResultadoFilaImportacionDto> Filas);

/// <summary>
/// Estado y datos calculados de una fila individual del archivo de importación masiva.
/// </summary>
public record ResultadoFilaImportacionDto(
    int Fila,
    string Estado,
    string? CodigoSeguimiento = null,
    decimal? Tarifa = null,
    string? Error = null)
{
    public const string Creado = "Creado";
    public const string Duplicado = "Duplicado";
    public const string ConError = "Error";
}
