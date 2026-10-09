using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Incidencias;

public class CierreDiarioOperacion : Entity, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public string ResumenJson { get; private set; } = string.Empty;
    public DateTime EjecutadoEn { get; private set; }

    protected CierreDiarioOperacion() { }

    public CierreDiarioOperacion(
        Guid id,
        Guid operadorId,
        DateOnly fecha,
        string resumenJson) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del cierre diario no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(resumenJson))
            throw new DomainValidationException("El resumen del cierre diario no puede ser vacío.");

        OperadorId = operadorId;
        Fecha = fecha;
        ResumenJson = resumenJson.Trim();
        EjecutadoEn = DateTime.UtcNow;
    }
}
