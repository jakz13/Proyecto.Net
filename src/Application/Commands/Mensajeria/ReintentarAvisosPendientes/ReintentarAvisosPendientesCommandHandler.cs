using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.ReintentarAvisosPendientes;

public class ReintentarAvisosPendientesCommandHandler : IRequestHandler<ReintentarAvisosPendientesCommand, ResultadoEntregaAvisos>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly AvisoComercioEntregador _entregador;
    private readonly IClock _clock;

    public ReintentarAvisosPendientesCommandHandler(IAvisoComercioRepository avisoRepository, AvisoComercioEntregador entregador, IClock clock)
    {
        _avisoRepository = avisoRepository;
        _entregador = entregador;
        _clock = clock;
    }

    public async Task<ResultadoEntregaAvisos> Handle(ReintentarAvisosPendientesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TamanoLote <= 0)
            throw new DomainValidationException("El tamaño del lote de avisos debe ser mayor a cero.");

        var pendientes = await _avisoRepository.ListarPendientesDeEntregaAsync(_clock.UtcNow, request.TamanoLote, cancellationToken);
        return await _entregador.EntregarAsync(pendientes, cancellationToken);
    }
}
