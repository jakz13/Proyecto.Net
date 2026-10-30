namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Resultado tras confirmar la recepción del bulto devuelto por parte del comercio (UC-32).
/// </summary>
public record ConfirmarDevolucionResultDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoDevolucion,
    bool Idempotente,
    string Mensaje);

