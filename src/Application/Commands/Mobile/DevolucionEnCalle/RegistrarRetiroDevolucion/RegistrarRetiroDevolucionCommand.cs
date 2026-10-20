using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.DevolucionEnCalle.RegistrarRetiroDevolucion;

/// <summary>El repartidor retira el bulto de un envío en devolución y lo lleva de vuelta al depósito (UC-22).</summary>
public record RegistrarRetiroDevolucionCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid EnvioId,
    string IdempotencyKey,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null,
    string? Observaciones = null
) : IRequest<RetiroDevolucionResultDto>;

public record RetiroDevolucionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoDevolucion,
    int BultosEnRetorno,
    bool Idempotente,
    string Mensaje);
