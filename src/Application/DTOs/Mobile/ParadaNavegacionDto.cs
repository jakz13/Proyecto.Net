namespace UltimaMilla.Application.DTOs.Mobile;

public record DestinatarioContactoDto(
    string Nombre,
    string? Telefono,
    string? Email,
    string? ReferenciaEntrega
);

public record ResumenBultosParadaDto(
    int CantidadBultos,
    List<string> CodigosBultos,
    decimal PesoTotalKg
);

public record ParadaNavegacionDto(
    Guid ParadaId,
    Guid EnvioId,
    int Orden,
    string EstadoParada,
    string CodigoSeguimiento,
    string DireccionFormateada,
    double? Latitud,
    double? Longitud,
    string? VentanaHoraria,
    DestinatarioContactoDto Destinatario,
    ResumenBultosParadaDto ResumenBultos,
    bool EsParadaActual
);
