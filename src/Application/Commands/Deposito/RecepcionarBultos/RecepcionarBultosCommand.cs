using MediatR;

namespace UltimaMilla.Application.Commands.Deposito.RecepcionarBultos;

/// <summary>
/// Recepción de un envío en depósito: bultos escaneados, bultos con daños y observaciones
/// con las que el operario documenta las discrepancias.
/// </summary>
public record RecepcionarBultosCommand(
    Guid EnvioId,
    IReadOnlyList<string> CodigosBultosEscaneados,
    IReadOnlyList<string>? CodigosBultosDanados = null,
    string? Observaciones = null
) : IRequest<RecepcionResultDto>;

public record RecepcionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Estado,
    bool Recepcionado,
    bool Idempotente,
    bool TieneDiscrepancias,
    IReadOnlyList<string> Correctos,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Sobrantes,
    IReadOnlyList<string> Danados,
    string Mensaje);
