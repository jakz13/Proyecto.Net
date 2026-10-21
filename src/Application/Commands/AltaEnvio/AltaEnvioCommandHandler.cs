using MediatR;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Commands.AltaEnvio;

public class AltaEnvioCommandHandler : IRequestHandler<AltaEnvioCommand, AltaEnvioResultDto>
{
    private readonly AltaEnvioProcesador _procesador;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public AltaEnvioCommandHandler(AltaEnvioProcesador procesador, ICurrentTenant currentTenant, IUnitOfWork unitOfWork)
    {
        _procesador = procesador;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<AltaEnvioResultDto> Handle(AltaEnvioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Un usuario de comercio solo puede dar de alta envíos de su propio comercio, sin importar lo que traiga el pedido
        if (_currentTenant.ComercioOperadorId.HasValue)
            request = request with { ComercioId = _currentTenant.ComercioOperadorId.Value };

        var resultado = await _procesador.ProcesarAsync(_currentTenant.OperadorId, request, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return resultado;
    }
}
