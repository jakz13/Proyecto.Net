namespace UltimaMilla.Application.DTOs;

public record EnvioDto(
    Guid Id,
    Guid ComercioId,
    string DireccionDestino,
    string NombreDestinatario,
    DateTime FechaCreacion,
    string Estado
);
