using MediatR;

namespace UltimaMilla.Application.Commands.Retiros.RegistrarRetiroMovil;

/// <summary>
/// El repartidor registra el retiro en el domicilio: identificación de los bultos por escaneo, fotografía de su estado y firma de
/// quien los entrega. Funciona sin conectividad: la app lo encola y lo envía al recuperarla, con una clave de idempotencia.
/// </summary>
public record RegistrarRetiroRealizadoCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid RetiroId,
    string IdempotencyKey,
    IReadOnlyList<string> CodigosBultosEscaneados,
    string? FotoEstadoBase64,
    string? FirmaBase64,
    string NombreEntrega,
    string? DocumentoEntrega,
    double? Latitud,
    double? Longitud,
    DateTime? FechaHoraDispositivoUtc) : IRequest<RetiroMovilResultDto>;

/// <summary>El repartidor no pudo retirar los bultos: registra un motivo del catálogo del operador.</summary>
public record RegistrarRetiroNoRealizadoCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid RetiroId,
    string IdempotencyKey,
    string? MotivoCodigo,
    Guid? MotivoNoEntregaId,
    string? Observaciones,
    double? Latitud,
    double? Longitud,
    DateTime? FechaHoraDispositivoUtc) : IRequest<RetiroMovilResultDto>;

public record RetiroMovilResultDto(
    Guid RetiroId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoRetiro,
    string EstadoParada,
    bool Idempotente,
    bool ConflictoResuelto,
    string Mensaje);
