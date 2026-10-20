using MediatR;

namespace UltimaMilla.Application.Commands.Comercio.ConfirmarDevolucion;

/// <summary>El comercio confirma que recibió el bulto de un envío devuelto (UC-32).</summary>
public record ConfirmarDevolucionCommand(Guid EnvioId) : IRequest<ConfirmarDevolucionResultDto>;

public record ConfirmarDevolucionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoDevolucion,
    bool Idempotente,
    string Mensaje);
