using MediatR;

namespace UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;

/// <summary>
/// Rendición del repartidor al regresar a depósito: bultos que devuelve físicamente, motivo interno para los
/// envíos que volvieron sin intento de entrega y observaciones.
/// </summary>
public record RendirHojaDeRutaCommand(
    Guid HojaDeRutaId,
    IReadOnlyList<string> CodigosBultosDevueltos,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null
) : IRequest<RendicionResultDto>;

public record RendicionResultDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    int BultosEntregados,
    int BultosDevueltos,
    int BultosPendientes,
    IReadOnlyList<string> EnviosReintegradosADeposito,
    IReadOnlyList<string> EnviosEnDevolucion,
    IReadOnlyList<string> EnviosPendientes,
    IReadOnlyList<string> CodigosSobrantes,
    bool Idempotente,
    string Mensaje);
