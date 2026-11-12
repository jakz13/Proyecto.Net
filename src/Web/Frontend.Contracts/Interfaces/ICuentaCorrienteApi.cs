using UltimaMilla.Frontend.Contracts.Modelos.Liquidaciones;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>Cuenta corriente y liquidaciones del comercio autenticado.</summary>
public interface ICuentaCorrienteApi
{
    Task<CuentaCorrienteDto> ObtenerCuentaCorrienteAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<LiquidacionDto>> ListarLiquidacionesAsync(CancellationToken cancelacion = default);
}
