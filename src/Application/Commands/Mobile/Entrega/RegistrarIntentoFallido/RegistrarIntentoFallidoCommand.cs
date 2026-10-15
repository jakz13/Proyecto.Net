using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;

public record RegistrarIntentoFallidoCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid EnvioId,
    string IdempotencyKey,
    Guid? MotivoNoEntregaId,
    string? MotivoCodigo,
    string Observaciones,
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
) : IRequest<ResultadoIntentoFallidoDto>;
