using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class CuadroTarifario : AggregateRoot, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public int Version { get; private set; }
    public DateTime VigenteDesde { get; private set; }
    public DateTime? VigenteHasta { get; private set; }

    private readonly List<ReglaTarifa> _reglasTarifa = new();
    public IReadOnlyCollection<ReglaTarifa> ReglasTarifa => _reglasTarifa.AsReadOnly();

    protected CuadroTarifario() { }

    public CuadroTarifario(
        Guid id,
        Guid operadorId,
        int version,
        DateTime vigenteDesde,
        DateTime? vigenteHasta = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del cuadro tarifario no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (version <= 0)
            throw new DomainValidationException("La versión del cuadro tarifario debe ser mayor a cero.");

        if (vigenteHasta.HasValue && vigenteHasta.Value < vigenteDesde)
            throw new DomainValidationException("La fecha de fin de vigencia no puede ser anterior a la de inicio.");

        OperadorId = operadorId;
        Version = version;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }

    public void AgregarRegla(ReglaTarifa regla)
    {
        ArgumentNullException.ThrowIfNull(regla);

        if (regla.CuadroTarifarioId != Id)
            throw new DomainValidationException("La regla no pertenece a este cuadro tarifario.");

        _reglasTarifa.Add(regla);
    }

    public void CerrarVigencia(DateTime fechaFin)
    {
        if (fechaFin < VigenteDesde)
            throw new DomainValidationException("La fecha de fin de vigencia no puede ser anterior a la de inicio.");

        VigenteHasta = fechaFin;
    }
}
