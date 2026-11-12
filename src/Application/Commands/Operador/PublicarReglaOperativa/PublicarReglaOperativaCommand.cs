using MediatR;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Application.Commands.Operador.PublicarReglaOperativa;

/// <summary>Valida y publica una nueva versión de la regla operativa y archiva la vigente.</summary>
public record PublicarReglaOperativaCommand(
    int MaxIntentosEntrega,
    TimeSpan PlazoEntreIntentos,
    string? TipoPruebaEntregaExigida,
    string? PoliticaDevolucion,
    TimeSpan PlazoComprometidoEstandar,
    TimeSpan PlazoComprometidoUrgente,
    int MaxParadasPorRuta,
    string? Comentario = null,
    bool AdmiteEntregaParcial = true,
    string PoliticaEntregaParcial = ReglaOperativa.PoliticaNuevaVisitaSinRecargo) : IRequest<ReglaOperativaDto>;
