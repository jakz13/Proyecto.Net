using MediatR;

namespace UltimaMilla.Application.Commands.Mobile.CargaVehiculo.IniciarRecorrido;

/// <summary>
/// Comando para iniciar el recorrido de una hoja de ruta despachada.
/// Transiciona la hoja de ruta de 'Despachada' a 'EnTransito' y habilita la confirmación de entregas en calle.
/// </summary>
public record IniciarRecorridoCommand(
    Guid HojaDeRutaId,
    string? IdempotencyKey = null
) : IRequest<IniciarRecorridoResultDto>;

/// <summary>
/// Resultado del inicio de recorrido de la hoja de ruta.
/// </summary>
public record IniciarRecorridoResultDto(
    Guid HojaDeRutaId,
    string Estado,
    DateTime FechaInicioUtc,
    bool YaIniciada,
    string Mensaje
);

/// <summary>
/// Contrato de solicitud HTTP para iniciar recorrido.
/// </summary>
public record IniciarRecorridoRequest(
    string? IdempotencyKey = null
);

