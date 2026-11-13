using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Ports;

namespace UltimaMilla.Application.Commands.AltaEnvio;

public class AltaEnvioCommandHandler : IRequestHandler<AltaEnvioCommand, AltaEnvioResultDto>
{
    private readonly AltaEnvioProcesador _procesador;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IComercioOperadorRepository? _comercioOperadorRepository;

    public AltaEnvioCommandHandler(
        AltaEnvioProcesador procesador,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork,
        IComercioOperadorRepository? comercioOperadorRepository = null)
    {
        _procesador = procesador;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
        _comercioOperadorRepository = comercioOperadorRepository;
    }

    public async Task<AltaEnvioResultDto> Handle(AltaEnvioCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Un usuario de comercio solo puede dar de alta envíos de su propio comercio, sin importar lo que traiga el pedido
        if (_currentTenant.ComercioOperadorId.HasValue)
            request = request with { ComercioId = _currentTenant.ComercioOperadorId.Value };

        await ComercioOperadorActivoGuard.AsegurarActivoAsync(
            _comercioOperadorRepository,
            _currentTenant.ComercioOperadorId ?? request.ComercioId,
            cancellationToken);

        var resultado = await _procesador.ProcesarAsync(_currentTenant.OperadorId, request, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return resultado;
    }
}
