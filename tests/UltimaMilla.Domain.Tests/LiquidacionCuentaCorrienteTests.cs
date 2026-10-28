using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

public class LiquidacionCuentaCorrienteTests
{
    private static Liquidacion NuevaLiquidacion() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), 1220m);

    [Fact]
    public void Liquidacion_RecorreElCicloPendienteAprobadaPagada()
    {
        var liquidacion = NuevaLiquidacion();
        Assert.Equal(Liquidacion.EstadoPendiente, liquidacion.Estado);

        liquidacion.Aprobar();
        Assert.Equal(Liquidacion.EstadoAprobada, liquidacion.Estado);

        liquidacion.Pagar();
        Assert.Equal(Liquidacion.EstadoPagada, liquidacion.Estado);
    }

    [Fact]
    public void Liquidacion_NoSePuedePagarSinAprobar()
    {
        Assert.Throws<DomainValidationException>(() => NuevaLiquidacion().Pagar());
    }

    [Fact]
    public void Liquidacion_NoSePuedeAprobarDosVeces()
    {
        var liquidacion = NuevaLiquidacion();
        liquidacion.Aprobar();

        Assert.Throws<DomainValidationException>(liquidacion.Aprobar);
    }

    [Fact]
    public void Liquidacion_PagadaNoSePuedeAnular()
    {
        var liquidacion = NuevaLiquidacion();
        liquidacion.Aprobar();
        liquidacion.Pagar();

        Assert.Throws<DomainValidationException>(liquidacion.Anular);
    }

    [Fact]
    public void Liquidacion_PendienteSePuedeAnular()
    {
        var liquidacion = NuevaLiquidacion();

        liquidacion.Anular();

        Assert.Equal(Liquidacion.EstadoAnulada, liquidacion.Estado);
    }

    [Fact]
    public void Liquidacion_ConPeriodoInvertido_LanzaDomainValidation()
    {
        var hoy = DateTime.UtcNow;

        Assert.Throws<DomainValidationException>(() => new Liquidacion(Guid.NewGuid(), Guid.NewGuid(), hoy, hoy.AddDays(-1), 10m));
    }

    [Fact]
    public void CuentaCorriente_ElSaldoSumaCargosYRestaPagos()
    {
        var cuenta = new CuentaCorriente(Guid.NewGuid(), Guid.NewGuid());
        var liquidacionId = Guid.NewGuid();

        cuenta.RegistrarMovimiento("Envío TRK-1", 100m, Guid.NewGuid(), liquidacionId);
        cuenta.RegistrarMovimiento("Envío TRK-2", 150m, Guid.NewGuid(), liquidacionId);
        cuenta.RegistrarMovimiento("Pago", -250m, liquidacionId: liquidacionId);

        Assert.Equal(0m, cuenta.Saldo);
        Assert.Equal(3, cuenta.Movimientos.Count);
    }

    [Fact]
    public void CuentaCorriente_CadaMovimientoRenuevaElSelloDeConcurrencia()
    {
        var cuenta = new CuentaCorriente(Guid.NewGuid(), Guid.NewGuid());
        var antes = cuenta.SelloModificacion;

        cuenta.RegistrarMovimiento("Envío TRK-1", 100m);

        Assert.NotEqual(antes, cuenta.SelloModificacion);
    }

    [Fact]
    public void CuentaCorriente_RechazaMovimientosEnCeroOSinConcepto()
    {
        var cuenta = new CuentaCorriente(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainValidationException>(() => cuenta.RegistrarMovimiento("Ajuste", 0m));
        Assert.Throws<DomainValidationException>(() => cuenta.RegistrarMovimiento(" ", 10m));
        Assert.Equal(0m, cuenta.Saldo);
    }
}
