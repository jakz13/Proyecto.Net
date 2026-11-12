using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionZonas;

/// <summary>
/// Comando para validar una lista de franjas horarias y detectar superposiciones inconsistentes.
/// </summary>
public record ValidarFranjasHorariasCommand(
    IReadOnlyList<CrearFranjaHorariaDto>? FranjasHorarias
) : IRequest;

