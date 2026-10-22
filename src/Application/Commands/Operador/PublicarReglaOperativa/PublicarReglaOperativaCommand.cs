using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Commands.Operador.PublicarReglaOperativa;

/// <summary>UC-04: publica una nueva versión de la regla operativa y archiva la vigente.</summary>
public record PublicarReglaOperativaCommand(
    int MaxIntentosEntrega,
    TimeSpan PlazoEntreIntentos,
    string? TipoPruebaEntregaExigida,
    string? PoliticaDevolucion,
    TimeSpan PlazoComprometidoEstandar,
    TimeSpan PlazoComprometidoUrgente,
    int MaxParadasPorRuta) : IRequest<ReglaOperativaDto>;