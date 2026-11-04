using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionTarifas;

/// <summary>
/// Comando para publicar una nueva versión del cuadro tarifario con sus reglas de precio (UC-03, RF-03).
/// </summary>
public record PublicarVersionCuadroTarifarioCommand(
    DateTime VigenteDesde,
    string? Comentario,
    IReadOnlyList<CrearReglaTarifaDto> ReglasTarifa
) : IRequest<CuadroTarifarioDetalleDto>;

