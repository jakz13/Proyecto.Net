using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.Navegacion.RegistrarEventoGeocerca;

/// <summary>
/// Comando para registrar la entrada (LLEGADA_GEOCERCA) o salida (SALIDA_GEOCERCA) automática
/// de la geocerca de una parada desde la aplicación móvil del repartidor (Sección 7.3).
/// </summary>
public record RegistrarEventoGeocercaCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid EnvioId,
    string TipoEvento,
    double Latitud,
    double Longitud,
    double DistanciaMetros,
    int? TiempoPermanenciaSegundos = null,
    DateTime? TimestampUtc = null,
    string? IdempotencyKey = null
) : IRequest<EventoGeocercaResultDto>;

public record EventoGeocercaResultDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string TipoEvento,
    string DescripcionPublica,
    double DistanciaMetros,
    int? TiempoPermanenciaSegundos,
    DateTime TimestampUtc,
    bool Idempotente
);

