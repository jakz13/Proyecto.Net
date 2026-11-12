using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.GestionTarifas;

/// <summary>
/// Comando para simular la cotización de un envío bajo la versión vigente del cuadro tarifario.
/// </summary>
public record SimularCotizacionTarifaCommand(
    decimal PesoKg,
    decimal VolumenM3,
    string ModalidadServicio,
    Guid? ZonaCoberturaId = null
) : IRequest<CotizacionSimuladaDto>;

