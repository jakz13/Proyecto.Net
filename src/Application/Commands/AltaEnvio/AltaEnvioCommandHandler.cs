using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

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
        {
            if (_comercioOperadorRepository != null)
            {
                var relacion = await _comercioOperadorRepository.GetByIdAsync(_currentTenant.ComercioOperadorId.Value, cancellationToken);
                if (relacion != null && !relacion.EstaActivo)
                {
                    throw new DomainValidationException("El comercio se encuentra dado de baja o inactivo. No se permite realizar altas de envíos.");
                }
            }

            request = request with { ComercioId = _currentTenant.ComercioOperadorId.Value };
        }

        var resultado = await _procesador.ProcesarAsync(_currentTenant.OperadorId, request, null, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return resultado;
    }
}
