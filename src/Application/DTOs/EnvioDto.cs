namespace UltimaMilla.Application.DTOs;

public record EnvioDto(
    Guid Id,
    Guid ComercioId,
    string DireccionDestino,
    string NombreDestinatario,
    DateTime FechaCreacion,
    string Estado,
    string? CodigoSeguimiento = null,
    string? HojaDeRutaNumero = null,
    string? RepartidorNombre = null,
    string? ComercioNombre = null,
    decimal? PesoKg = null
);
