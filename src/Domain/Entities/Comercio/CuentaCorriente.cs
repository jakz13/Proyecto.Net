using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class CuentaCorriente : AggregateRoot, IMustHaveComercioTenant, IHasConcurrencyToken
{
    public Guid ComercioOperadorId { get; private set; }
    public decimal Saldo { get; private set; }
    public string Moneda { get; private set; } = string.Empty;
    public Guid SelloModificacion { get; private set; }

    private readonly List<MovimientoCuentaCorriente> _movimientos = new();
    public IReadOnlyCollection<MovimientoCuentaCorriente> Movimientos => _movimientos.AsReadOnly();

    protected CuentaCorriente() { }

    public CuentaCorriente(
        Guid id,
        Guid comercioOperadorId,
        string moneda = "UYU",
        decimal saldoInicial = 0) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la cuenta corriente no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(moneda))
            throw new DomainValidationException("La moneda no puede ser vacía.");

        ComercioOperadorId = comercioOperadorId;
        Moneda = moneda.Trim().ToUpperInvariant();
        Saldo = saldoInicial;
        SelloModificacion = Guid.NewGuid();
    }

    public void RegistrarMovimiento(string concepto, decimal monto, Guid? envioId = null, Guid? liquidacionId = null)
    {
        if (string.IsNullOrWhiteSpace(concepto))
            throw new DomainValidationException("El concepto del movimiento no puede ser vacío.");

        if (monto == 0)
            throw new DomainValidationException("El monto del movimiento no puede ser cero.");

        var movimiento = new MovimientoCuentaCorriente(
            Guid.NewGuid(),
            Id,
            concepto,
            monto,
            envioId,
            liquidacionId);

        _movimientos.Add(movimiento);
        Saldo += monto;
        SelloModificacion = Guid.NewGuid();
    }
}
