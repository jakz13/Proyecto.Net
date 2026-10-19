using UltimaMilla.Frontend.Contracts.Modelos.Despacho;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de API para el control de salida y despacho físico de hojas de ruta por el operario de depósito (UC-09).
/// </summary>
public interface IDespachoApi
{
    /// <summary>
    /// Lista las hojas de ruta que se encuentran en estado Liberada o EnCarga listas para control de salida.
    /// </summary>
    Task<IReadOnlyList<HojaDeRutaDto>> ListarRutasPendientesDespachoAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Obtiene el detalle de escaneo y verificación de bultos de una hoja de ruta particular.
    /// </summary>
    Task<VerificacionDespachoDto?> ObtenerControlDespachoAsync(Guid hojaDeRutaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Confirma la salida del vehículo. Pasa a En Tránsito los bultos cargados y devuelve a Depósito los bultos faltantes.
    /// </summary>
    Task<ResultadoDespachoDto> ConfirmarDespachoAsync(ConfirmarDespachoDto dto, string operarioUsuario, CancellationToken cancelacion = default);

    /// <summary>
    /// Registra la rendición en depósito de una hoja de ruta previa para habilitar al repartidor.
    /// </summary>
    Task<bool> RendirRutaPreviaAsync(string numeroRuta, CancellationToken cancelacion = default);
}
