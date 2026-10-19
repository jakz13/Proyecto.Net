using MediatR;

namespace UltimaMilla.Application.Commands.AltaEnvio;

/// <summary>Alta individual de un envío: destinatario, dirección, bultos y modalidad de servicio.</summary>
public record AltaEnvioCommand(
    Guid ComercioId,
    string ModalidadServicio,
    DestinatarioAltaDto Destinatario,
    DireccionAltaDto Direccion,
    IReadOnlyList<BultoAltaDto> Bultos,
    Guid? FranjaHorariaId = null
) : IRequest<AltaEnvioResultDto>;

public record DestinatarioAltaDto(
    string Nombre,
    string? Documento = null,
    string? Email = null,
    string? Telefono = null);

public record DireccionAltaDto(
    string Calle,
    string Numero,
    string Ciudad,
    Guid? ZonaCoberturaId = null,
    double? Latitud = null,
    double? Longitud = null,
    string? Referencia = null);

/// <summary>Medidas del bulto: peso en kg y dimensiones en centímetros.</summary>
public record BultoAltaDto(decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);

public record AltaEnvioResultDto(
    Guid Id,
    string CodigoSeguimiento,
    string Estado,
    decimal Tarifa,
    IReadOnlyList<string> IdentificacionesBultos);
