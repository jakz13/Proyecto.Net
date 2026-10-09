using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class MovimientoCuentaCorriente : Entity
{
    public Guid CuentaCorrienteId { get; init; }
    public DateTime Fecha { get; init; }
    public string Concepto { get; init; } = string.Empty;
    public decimal Monto { get; init; }
    public Guid? EnvioId { get; init; }
    public Guid? LiquidacionId { get; init; }

    protected MovimientoCuentaCorriente() { }

    public MovimientoCuentaCorriente(
        Guid id,
        Guid cuentaCorrienteId,
        string concepto,
        decimal monto,
        Guid? envioId = null,
        Guid? liquidacionId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del movimiento no puede ser vacío.");

        if (cuentaCorrienteId == Guid.Empty)
            throw new DomainValidationException("El identificador de la cuenta corriente no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(concepto))
            throw new DomainValidationException("El concepto del movimiento no puede ser vacío.");

        if (monto == 0)
            throw new DomainValidationException("El monto del movimiento no puede ser cero.");

        CuentaCorrienteId = cuentaCorrienteId;
        Concepto = concepto.Trim();
        Monto = monto;
        EnvioId = envioId;
        LiquidacionId = liquidacionId;
        Fecha = DateTime.UtcNow;
    }
}
