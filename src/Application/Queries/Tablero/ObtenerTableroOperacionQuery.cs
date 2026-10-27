using MediatR;

namespace UltimaMilla.Application.Queries.Tablero;

/// <summary>
/// Tablero de operación en vivo del despachador (UC-12, RF-29): hojas de ruta en ejecución del operador de la sesión,
/// última posición conocida de cada unidad y envíos en riesgo de incumplir su ventana comprometida.
/// </summary>
public record ObtenerTableroOperacionQuery : IRequest<TableroOperacionDto>;

/// <summary>Resumen consolidado del tablero. Los totales se calculan sobre las hojas de ruta en ejecución.</summary>
public record TableroOperacionDto(
    int TotalRutasEnEjecucion,
    int TotalRepartidoresEnCalle,
    int TotalEnviosEnTransito,
    int TotalEnviosEntregados,
    int TotalEnviosEnRiesgo,
    DateTime UltimaSincronizacion,
    IReadOnlyList<VehiculoEnVivoDto> Vehiculos,
    IReadOnlyList<EnvioRiesgoDto> AlertasRiesgo);

/// <summary>
/// Estado de una hoja de ruta en ejecución con su unidad y su repartidor.
/// La posición es la última conocida y puede no existir (<see cref="Latitud"/> nulo): la plataforma no recibe telemetría GPS continua.
/// </summary>
public record VehiculoEnVivoDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    Guid RepartidorId,
    string RepartidorNombre,
    string RepartidorTelefono,
    Guid VehiculoId,
    string VehiculoMatricula,
    string VehiculoTipo,
    double? Latitud,
    double? Longitud,
    string? DireccionAproximada,
    string? OrigenPosicion,
    DateTime? UltimaActualizacion,
    string EstadoOperativo,
    int ParadasCompletadas,
    int TotalParadas,
    string? ParadaActualCodigo,
    string? ParadaActualDireccion,
    bool TieneAlertasRiesgo,
    int CantidadEnviosEnRiesgo,
    IReadOnlyList<EnvioRiesgoDto> EnviosEnRiesgo,
    IReadOnlyList<ParadaEnVivoDto> Paradas);

public record ParadaEnVivoDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    int Orden,
    string Destinatario,
    string Direccion,
    double? Latitud,
    double? Longitud,
    string? FranjaHoraria,
    string Estado,
    bool EnRiesgo,
    string? MotivoRiesgo);

/// <summary>
/// Envío pendiente cuya ventana comprometida está vencida o no alcanza para las paradas que le restan a la ruta.
/// <see cref="HoraLimiteVentana"/> es hora local de la operación; <see cref="MinutosParaCierreVentana"/> es negativo si ya venció.
/// </summary>
public record EnvioRiesgoDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    string NumeroRuta,
    string CodigoSeguimiento,
    string Destinatario,
    string Direccion,
    string FranjaHoraria,
    DateTime HoraLimiteVentana,
    int MinutosParaCierreVentana,
    string NivelRiesgo,
    string CausaRiesgo,
    string RepartidorNombre,
    string RepartidorTelefono,
    string VehiculoMatricula,
    double? Latitud,
    double? Longitud);
