using UltimaMilla.Frontend.Contracts.Modelos.Flota;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de API para la gestión integral de vehículos y repartidores (UC-07, RF-06).
/// </summary>
public interface IFlotaApi
{
    // Vehículos
    Task<IReadOnlyList<VehiculoDto>> ListarVehiculosAsync(bool? soloActivos = null, CancellationToken cancelacion = default);
    Task<VehiculoDto?> ObtenerVehiculoPorIdAsync(Guid id, CancellationToken cancelacion = default);
    Task<VehiculoDto> CrearVehiculoAsync(CrearVehiculoDto dto, CancellationToken cancelacion = default);
    Task<VehiculoDto> ActualizarVehiculoAsync(ActualizarVehiculoDto dto, CancellationToken cancelacion = default);
    Task CambiarEstadoVehiculoAsync(Guid id, bool activo, CancellationToken cancelacion = default);

    // Repartidores
    Task<IReadOnlyList<RepartidorDto>> ListarRepartidoresAsync(string? estado = null, CancellationToken cancelacion = default);
    Task<RepartidorDto?> ObtenerRepartidorPorIdAsync(Guid id, CancellationToken cancelacion = default);
    Task<RepartidorDto> CrearRepartidorAsync(CrearRepartidorDto dto, CancellationToken cancelacion = default);
    Task<RepartidorDto> ActualizarRepartidorAsync(ActualizarRepartidorDto dto, CancellationToken cancelacion = default);
    Task AsignarVehiculoAsync(Guid repartidorId, Guid? vehiculoId, CancellationToken cancelacion = default);
    Task CambiarEstadoRepartidorAsync(Guid repartidorId, string nuevoEstado, CancellationToken cancelacion = default);
}
