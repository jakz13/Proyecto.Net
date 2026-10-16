using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;

public record RegistrarEntregaExitosaCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid EnvioId,
    string IdempotencyKey,
    string NombreReceptor,
    string DocumentoReceptor,
    string? FirmaDigitalBase64 = null,
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
) : IRequest<EntregaExitosaResultDto>;
