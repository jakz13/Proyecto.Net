using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Comando para definir una nueva zona de cobertura y asociar sus franjas horarias.
/// </summary>
public record CrearZonaCoberturaCommand(
    string Nombre,
    string PoligonoGeografico,
    string ColorHex,
    IReadOnlyList<CrearFranjaHorariaDto>? FranjasHorarias
) : IRequest<ZonaCoberturaDetalleDto>;

