using MediatR;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Envios.ObtenerDetalleEnvio;

/// <summary>Detalle interno de un envío para el Backoffice: historial completo, intentos y pruebas de entrega.</summary>
public record ObtenerDetalleEnvioQuery(Guid EnvioId) : IRequest<DetalleEnvioDto>;

public record DetalleEnvioDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    EstadoEnvio EstadoActual,
    string ModalidadServicio,
    decimal Tarifa,
    string NombreDestinatario,
    string DireccionDestino,
    Guid? HojaDeRutaId,
    DateTime FechaAlta,
    IReadOnlyList<DetalleBultoDto> Bultos,
    IReadOnlyList<DetalleEventoDto> Eventos,
    IReadOnlyList<DetalleIntentoDto> Intentos);

public record DetalleBultoDto(Guid BultoId, string Identificacion, decimal PesoKg, string EstadoBulto, string? Prioridad);

public record DetalleEventoDto(
    DateTime Fecha,
    string TipoEvento,
    EstadoEnvio? EstadoAnterior,
    EstadoEnvio EstadoNuevo,
    string Origen,
    Guid? ResponsableUsuarioId,
    double? Latitud,
    double? Longitud,
    string? Detalle);

public record DetalleIntentoDto(
    Guid IntentoId,
    Guid ParadaId,
    string Resultado,
    DateTime Fecha,
    string OrigenDatos,
    Guid? MotivoNoEntregaId,
    double? Latitud,
    double? Longitud,
    PruebaEntregaDto? Prueba);

/// <summary>La firma y la foto solo se indican como presentes: el contenido se descarga por separado.</summary>
public record PruebaEntregaDto(
    string Tipo,
    string? NombreReceptor,
    string? DocumentoReceptor,
    bool TieneFirma,
    bool TieneFoto,
    DateTime FechaDispositivo);
