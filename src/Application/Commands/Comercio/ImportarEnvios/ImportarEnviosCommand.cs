using MediatR;
using UltimaMilla.Application.Commands.AltaEnvio;

namespace UltimaMilla.Application.Commands.Comercio.ImportarEnvios;

/// <summary>
/// Alta masiva de envíos desde un archivo ya leído. La clave de idempotencia identifica el lote: reimportar el mismo
/// lote (completo o parcial) no duplica envíos, porque cada registro se reconoce por su referencia.
/// </summary>
public record ImportarEnviosCommand(
    string? ClaveIdempotencia,
    IReadOnlyList<FilaImportacion> Filas
) : IRequest<ResultadoImportacionDto>;

/// <summary>Un registro del archivo: o trae el alta lista para procesar o el error de lectura de la fila.</summary>
public record FilaImportacion(int Numero, AltaEnvioCommand? Alta, string? ErrorLectura = null);

public record ResultadoImportacionDto(
    Guid LoteId,
    string ClaveIdempotencia,
    bool Reimportacion,
    int TotalRegistros,
    int Creados,
    int Duplicados,
    int ConError,
    IReadOnlyList<ResultadoFilaDto> Filas);

public record ResultadoFilaDto(
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
