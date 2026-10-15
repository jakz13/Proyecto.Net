namespace UltimaMilla.Application.DTOs.Mobile;

public record BultoItemDto(
    Guid BultoId,
    Guid EnvioId,
    string CodigoBulto,
    string CodigoSeguimientoEnvio,
    string Destinatario,
    string DireccionDestino,
    decimal PesoKg
);

public record BultoSobranteDto(
    string CodigoBulto,
    string Motivo,
    Guid? EnvioId = null,
    Guid? OtraRutaId = null
);

public record ResultadoVerificacionCargaDto(
    Guid HojaDeRutaId,
    int TotalBultosAsignados,
    int TotalBultosEscaneados,
    int TotalCorrectos,
    int TotalFaltantes,
    int TotalSobrantes,
    bool TieneDiscrepancias,
    bool PuedeDespachar,
    List<BultoItemDto> BultosCorrectos,
    List<BultoItemDto> BultosFaltantes,
    List<BultoSobranteDto> BultosSobrantes
);
