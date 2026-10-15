namespace UltimaMilla.Application.DTOs.Mobile;

public record ResultadoIntentoFallidoDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoFinalResuelto,
    int IntentosAcumulados,
    int IntentosRestantes,
    int MaxIntentosPermitidos,
    string Motivo,
    DateTime TimestampUtc,
    bool Idempotente,
    string Mensaje
);
