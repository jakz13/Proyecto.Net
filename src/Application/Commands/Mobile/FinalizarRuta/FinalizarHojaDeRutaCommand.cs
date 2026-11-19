using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.FinalizarRuta;

/// <summary>
/// El repartidor da por terminado su recorrido desde la app y declara lo que devuelve al depósito.
/// No rinde la hoja: la rendición (verificación física de lo que vuelve) la hace el operario en el depósito.
/// </summary>
public record FinalizarHojaDeRutaCommand(
    Guid HojaDeRutaId,
    IReadOnlyList<string> CodigosBultosDevueltos,
    string? Observaciones = null
) : IRequest<FinalizacionResultDto>;

public record FinalizacionResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    int BultosDeclaradosADevolver,
    bool Idempotente,
    string Mensaje);
