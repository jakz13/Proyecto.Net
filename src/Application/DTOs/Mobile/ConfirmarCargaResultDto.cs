namespace UltimaMilla.Application.DTOs.Mobile;

public record ConfirmarCargaResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    DateTime FechaDespacho,
    int TotalBultosCargados,
    int TotalEnviosEnTransito,
    int TotalEnviosDesasignados,
    bool Idempotente,
    string Mensaje
);
