using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Liquidaciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>Implementación simulada de ICuentaCorrienteApi para la demo sin backend (RF-34).</summary>
public class MockCuentaCorrienteApi : ICuentaCorrienteApi
{
    private static readonly Guid Comercio = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    public Task<CuentaCorrienteDto> ObtenerCuentaCorrienteAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(new CuentaCorrienteDto
        {
            ComercioOperadorId = Comercio,
            Saldo = 1830m,
            Movimientos = new()
            {
                new() { Id = Guid.NewGuid(), Fecha = DateTime.UtcNow.AddDays(-1), Concepto = "Envío entregado (liquidación 1)", Monto = 610m },
                new() { Id = Guid.NewGuid(), Fecha = DateTime.UtcNow.AddDays(-2), Concepto = "Envío entregado (liquidación 1)", Monto = 1220m },
                new() { Id = Guid.NewGuid(), Fecha = DateTime.UtcNow.AddDays(-9), Concepto = "Pago recibido", Monto = -2500m }
            }
        });

    public Task<IReadOnlyList<LiquidacionDto>> ListarLiquidacionesAsync(CancellationToken cancelacion = default) =>
        Task.FromResult<IReadOnlyList<LiquidacionDto>>(new List<LiquidacionDto>
        {
            new() { Id = Guid.NewGuid(), ComercioOperadorId = Comercio, Comercio = "Comercio demo", PeriodoDesde = DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), PeriodoHasta = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), CantidadEnvios = 3, Subtotal = 1500m, Iva = 330m, MontoTotal = 1830m, Estado = "Pendiente", FechaGeneracion = DateTime.UtcNow }
        });
}
