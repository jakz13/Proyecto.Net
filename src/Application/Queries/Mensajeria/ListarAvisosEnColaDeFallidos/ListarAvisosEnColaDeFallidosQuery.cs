using MediatR;

namespace UltimaMilla.Application.Queries.Mensajeria.ListarAvisosEnColaDeFallidos;

/// <summary>
/// Avisos a comercios cuyos reintentos automáticos se agotaron (o quedaron fallidos): son parte de la cola de fallidos (letra 6.9),
/// donde el administrador los consulta, reenvía o descarta junto con el resto de los mensajes. Con <paramref name="AvisoId"/> devuelve
/// solo ese aviso, y únicamente si sigue esperando una decisión.
/// </summary>
public record ListarAvisosEnColaDeFallidosQuery(Guid? AvisoId = null) : IRequest<IReadOnlyList<AvisoEnColaDeFallidosDto>>;

public record AvisoEnColaDeFallidosDto(
    Guid Id,
    Guid EnvioId,
    string Payload,
    string? UltimaRespuestaHttp,
    int Intentos,
    DateTime Timestamp);
