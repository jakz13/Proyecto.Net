using MediatR;

namespace UltimaMilla.Application.Commands.Deposito.RendirHojaDeRuta;

/// <summary>
/// Rendición del repartidor al regresar a depósito: bultos que devuelve físicamente, motivo interno para los
/// envíos que volvieron sin intento de entrega y observaciones. En una hoja mixta, <c>RetirosRecibidosIds</c> son los retiros
/// en domicilio cuyos bultos el operario recibió en depósito; si no se informa (<c>null</c>) se reciben todos los ya retirados.
/// </summary>
public record RendirHojaDeRutaCommand(
    Guid HojaDeRutaId,
    IReadOnlyList<string> CodigosBultosDevueltos,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null,
    IReadOnlyList<Guid>? RetirosRecibidosIds = null
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
    string Mensaje,
    int RetirosRecibidos = 0,
    int RetirosSinRecibir = 0);
