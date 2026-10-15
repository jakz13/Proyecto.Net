using MediatR;
using UltimaMilla.Application.DTOs.Mobile;

namespace UltimaMilla.Application.Commands.Mobile.Navegacion.SeleccionarParadaActual;

public record SeleccionarParadaActualCommand(
    Guid HojaDeRutaId,
    Guid ParadaId,
    double? LatitudActual = null,
    double? LongitudActual = null,
    DateTime? Timestamp = null,
    string Accion = "IniciarNavegacion" // "IniciarNavegacion" o "RegistrarArribo"
) : IRequest<ParadaActualizadaDto>;
