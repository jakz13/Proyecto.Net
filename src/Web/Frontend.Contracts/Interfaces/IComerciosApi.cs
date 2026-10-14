using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para el ABM y gestión de comercios clientes (UC-01).
/// Consumido principalmente por el Backoffice del Operador.
/// </summary>
public interface IComerciosApi
{
    /// <summary>
    /// Lista los comercios asociados al operador aplicando filtros de búsqueda y paginación (UC-01).
    /// </summary>
    Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(FiltroComerciosDto filtro, CancellationToken cancelacion = default);

    /// <summary>
    /// Sobrecarga básica para paginación sin filtros adicionales.
    /// </summary>
    Task<ResultadoPaginadoDto<ComercioDto>> ListarComerciosAsync(int pagina = 1, int tamanoPagina = 10, CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de un comercio por su identificador.
    /// </summary>
    Task<ComercioDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra un nuevo comercio para el operador en sesión.
    /// </summary>
    Task<ComercioDto> CrearComercioAsync(CrearComercioDto nuevoComercio, CancellationToken cancelacion = default);

    /// <summary>
    /// Actualiza los datos de un comercio validando el sello de modificación para concurrencia optimista.
    /// Lanza excepción de conflicto si el sello no coincide (HTTP 409).
    /// </summary>
    Task<ComercioDto> ActualizarComercioAsync(Guid id, ActualizarComercioDto comercioActualizado, CancellationToken cancelacion = default);

    /// <summary>
    /// Modifica el estado activo/inactivo (alta/baja lógica) de un comercio.
    /// </summary>
    Task CambiarEstadoComercioAsync(Guid id, bool activo, CancellationToken cancelacion = default);
}
