using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class LoteImportacion : AggregateRoot, IMustHaveComercioTenant
{
    public Guid ComercioOperadorId { get; private set; }
    public string ClaveIdempotencia { get; private set; } = string.Empty;
    public DateTime FechaProcesamiento { get; private set; }
    public int CantidadRegistros { get; private set; }
    public int CantidadExitosos { get; private set; }
    public int CantidadConError { get; private set; }

    protected LoteImportacion() { }

    public LoteImportacion(
        Guid id,
        Guid comercioOperadorId,
        string claveIdempotencia,
        int cantidadRegistros) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del lote de importación no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(claveIdempotencia))
            throw new DomainValidationException("La clave de idempotencia no puede ser vacía.");

        if (cantidadRegistros <= 0)
            throw new DomainValidationException("La cantidad de registros del lote debe ser mayor a cero.");

        ComercioOperadorId = comercioOperadorId;
        ClaveIdempotencia = claveIdempotencia.Trim();
        CantidadRegistros = cantidadRegistros;
        FechaProcesamiento = DateTime.UtcNow;
    }

    public void RegistrarResultado(int exitosos, int conError)
    {
        if (exitosos < 0 || conError < 0)
            throw new DomainValidationException("Las cantidades procesadas no pueden ser negativas.");

        if (exitosos + conError > CantidadRegistros)
            throw new DomainValidationException("La suma de procesados no puede superar el total de registros.");

        CantidadExitosos = exitosos;
        CantidadConError = conError;
    }
}
