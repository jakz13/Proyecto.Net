using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Queries.Mensajeria.ListarAvisosEnColaDeFallidos;

public class ListarAvisosEnColaDeFallidosQueryHandler : IRequestHandler<ListarAvisosEnColaDeFallidosQuery, IReadOnlyList<AvisoEnColaDeFallidosDto>>
{
    /// <summary>Tope de la lista: la cola es para decidir caso por caso, no para paginar históricos.</summary>
    public const int Limite = 200;

    private readonly IAvisoComercioRepository _avisoRepository;

    public ListarAvisosEnColaDeFallidosQueryHandler(IAvisoComercioRepository avisoRepository)
    {
        _avisoRepository = avisoRepository;
    }

    public async Task<IReadOnlyList<AvisoEnColaDeFallidosDto>> Handle(ListarAvisosEnColaDeFallidosQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // El repositorio ya acota por operador y comercio (filtros globales): no hay forma de ver avisos de otro inquilino
        if (request.AvisoId is { } id)
        {
            var aviso = await _avisoRepository.GetAvisoByIdAsync(id, cancellationToken);
            return aviso is not null && AvisoComercio.EsEstadoDeColaDeFallidos(aviso.Estado)
                ? new[] { Mapear(aviso) }
                : Array.Empty<AvisoEnColaDeFallidosDto>();
        }

        var agotados = await _avisoRepository.ListarHistorialAvisosAsync(null, "Agotado", Limite, cancellationToken);
        var fallidos = await _avisoRepository.ListarHistorialAvisosAsync(null, "Fallido", Limite, cancellationToken);

        return agotados.Concat(fallidos)
            .OrderByDescending(a => a.Timestamp)
            .Take(Limite)
            .Select(Mapear)
            .ToList();
    }

    private static AvisoEnColaDeFallidosDto Mapear(AvisoComercio a) =>
        new(a.Id, a.EnvioId, a.Payload, a.UltimaRespuestaHttp, a.Intentos, a.Timestamp);
}
