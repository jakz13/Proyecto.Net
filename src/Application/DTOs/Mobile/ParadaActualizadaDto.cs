namespace UltimaMilla.Application.DTOs.Mobile;

public record ParadaActualizadaDto(
    Guid HojaDeRutaId,
    Guid ParadaId,
    string EstadoAnterior,
    string NuevoEstado,
    DateTime Timestamp,
    string Mensaje
);
