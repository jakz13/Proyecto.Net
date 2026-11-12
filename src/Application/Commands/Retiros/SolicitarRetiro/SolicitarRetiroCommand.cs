using MediatR;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Domain.Entities.Retiros;

namespace UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;

/// <summary>
/// El comercio solicita el retiro en domicilio de uno o más bultos: indica los bultos, el domicilio, la franja horaria y el motivo.
/// La referencia externa (opcional) es el identificador de la solicitud en el sistema del comercio y la hace idempotente.
/// </summary>
public record SolicitarRetiroCommand(
    Guid ComercioId,
    ContactoRetiroDto Contacto,
    DireccionAltaDto Direccion,
    IReadOnlyList<BultoRetiroAltaDto> Bultos,
    string Motivo,
    Guid? FranjaHorariaId = null,
    DateOnly? FechaSolicitada = null,
    string? ReferenciaExterna = null,
    string Origen = Retiro.OrigenPortal
) : IRequest<SolicitarRetiroResultDto>;

/// <summary>Quién entrega los bultos en el domicilio (normalmente el comprador).</summary>
public record ContactoRetiroDto(string Nombre, string? Telefono = null, string? Email = null);

/// <summary>Bulto a retirar: descripción libre, peso en kg y dimensiones en centímetros.</summary>
public record BultoRetiroAltaDto(string? Descripcion, decimal Peso, decimal Alto, decimal Ancho, decimal Profundo);

public record SolicitarRetiroResultDto(
    Guid Id,
    string CodigoSeguimiento,
    string Estado,
    decimal Tarifa,
    IReadOnlyList<string> IdentificacionesBultos,
    bool Duplicado = false);

/// <summary>Solicitud de retiro desde el sistema del comercio, autenticada por su clave de API y no por una sesión de usuario.</summary>
public record SolicitarRetiroPorApiCommand(
    string? ClaveApi,
    ContactoRetiroDto Contacto,
    DireccionAltaDto Direccion,
    IReadOnlyList<BultoRetiroAltaDto> Bultos,
    string Motivo,
    Guid? FranjaHorariaId = null,
    DateOnly? FechaSolicitada = null,
    string? ReferenciaExterna = null
) : IRequest<SolicitarRetiroResultDto>;
