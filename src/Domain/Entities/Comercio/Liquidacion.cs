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

    public const string EstadoPendiente = "Pendiente";
    public const string EstadoAprobada = "Aprobada";
    public const string EstadoPagada = "Pagada";
    public const string EstadoAnulada = "Anulada";

    public void Aprobar()
    {
        if (Estado != EstadoPendiente)
            throw new DomainValidationException($"Solo se puede aprobar una liquidación pendiente (estado actual: '{Estado}').");

        Estado = EstadoAprobada;
    }

    public void Pagar()
    {
        if (Estado != EstadoAprobada)
            throw new DomainValidationException($"Solo se puede pagar una liquidación aprobada (estado actual: '{Estado}').");

        Estado = EstadoPagada;
    }

    public void Anular()
    {
        if (Estado == EstadoPagada)
            throw new DomainValidationException("No se puede anular una liquidación ya pagada.");

        Estado = EstadoAnulada;
    }
}
