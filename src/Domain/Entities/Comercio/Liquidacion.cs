using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class Liquidacion : Entity, IMustHaveComercioTenant
{
    public Guid ComercioOperadorId { get; private set; }
    public DateTime PeriodoDesde { get; private set; }
    public DateTime PeriodoHasta { get; private set; }
    public decimal MontoTotal { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public DateTime FechaGeneracion { get; private set; }

    protected Liquidacion() { }

    public Liquidacion(
        Guid id,
        Guid comercioOperadorId,
        DateTime periodoDesde,
        DateTime periodoHasta,
        decimal montoTotal,
        string estado = "Pendiente") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la liquidación no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (periodoHasta < periodoDesde)
            throw new DomainValidationException("La fecha final del período no puede ser anterior a la de inicio.");

        ComercioOperadorId = comercioOperadorId;
        PeriodoDesde = periodoDesde;
        PeriodoHasta = periodoHasta;
        MontoTotal = montoTotal;
        Estado = string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Trim();
        FechaGeneracion = DateTime.UtcNow;
    }

    public void Aprobar()
    {
        Estado = "Aprobada";
    }

    public void Pagar()
    {
        Estado = "Pagada";
    }

    public void Anular()
    {
        Estado = "Anulada";
    }
}
