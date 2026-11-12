using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaParcial;

/// <summary>
/// Comando para registrar la entrega parcial o por escaneo individual de bultos en una parada.
/// Funciona sin conectividad previa y aplica resolución de conflictos a nivel de bulto durante la sincronización.
/// </summary>
public record RegistrarEntregaParcialCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    Guid EnvioId,
    string IdempotencyKey,
    IReadOnlyList<string> CodigosBultosEntregados,
    IReadOnlyList<BultoNoEntregadoRequestDto> BultosNoEntregados,
    string NombreReceptor,
    string DocumentoReceptor,
    string? FirmaDigitalBase64 = null,
    string? FotoEvidenciaBase64 = null,
    double? Latitud = null,
    double? Longitud = null,
    DateTime? FechaHoraDispositivoUtc = null
) : IRequest<EntregaParcialResultDto>;

