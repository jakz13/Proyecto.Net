using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para la gestión de incidencias operativas de la jornada (UC-11).
/// Permite registro, seguimiento mediante bitácora cronológica y cierre formal.
/// </summary>
public interface IIncidenciasApi
{
    Task<IReadOnlyList<IncidenciaDto>> ListarIncidenciasAsync(FiltroIncidenciasDto? filtro = null, CancellationToken cancelacion = default);

    Task<IncidenciaDto?> ObtenerIncidenciaPorIdAsync(Guid id, CancellationToken cancelacion = default);

    Task<IncidenciaDto> RegistrarIncidenciaAsync(RegistrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default);

    Task<bool> AgregarSeguimientoAsync(AgregarSeguimientoIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default);

    Task<bool> CerrarIncidenciaAsync(CerrarIncidenciaDto dto, string despachadorUsuario, CancellationToken cancelacion = default);

    Task<IReadOnlyList<EnvioDisponibleIncidenciaDto>> ListarEnviosDisponiblesAsync(CancellationToken cancelacion = default);

    Task<IReadOnlyList<RutaDisponibleIncidenciaDto>> ListarRutasDisponiblesAsync(CancellationToken cancelacion = default);
}
