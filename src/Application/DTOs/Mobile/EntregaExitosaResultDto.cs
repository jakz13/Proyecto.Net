namespace UltimaMilla.Application.DTOs.Mobile;

public record EntregaExitosaResultDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoParada,
    DateTime FechaHoraEntregaUtc,
    bool Idempotente,
    string Mensaje
);
