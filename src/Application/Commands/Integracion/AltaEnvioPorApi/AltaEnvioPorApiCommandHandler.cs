using MediatR;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;

namespace UltimaMilla.Application.Commands.Integracion.AltaEnvioPorApi;

public class AltaEnvioPorApiCommandHandler : IRequestHandler<AltaEnvioPorApiCommand, AltaEnvioResultDto>
{
    private readonly IComercioOperadorRepository _comercioOperadorRepository;
    private readonly AltaEnvioProcesador _procesador;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant? _currentTenant;

    public AltaEnvioPorApiCommandHandler(
        IComercioOperadorRepository comercioOperadorRepository,
        AltaEnvioProcesador procesador,
        IUnitOfWork unitOfWork,
        ICurrentTenant? currentTenant = null)
    {
        _comercioOperadorRepository = comercioOperadorRepository;
        _procesador = procesador;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
    }

    public async Task<AltaEnvioResultDto> Handle(AltaEnvioPorApiCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Mismo rechazo para clave ausente, desconocida o de un comercio suspendido: no se revela cuál es el caso
        if (string.IsNullOrWhiteSpace(request.ClaveApi))
            throw new UnauthorizedAccessException("Clave de API inválida.");

        var comercioOperador = await _comercioOperadorRepository.GetByHuellaClaveApiAsync(
                ComercioOperador.CalcularHuellaClaveApi(request.ClaveApi), cancellationToken);
        if (comercioOperador is null || !comercioOperador.EstaActivo)
            throw new UnauthorizedAccessException("Clave de API inválida.");

        // El canal por clave de API autentica al comercio y fija el operador dueño para el resto de la operación
        _currentTenant?.EstablecerOperadorDeProceso(comercioOperador.OperadorId);

        var alta = new AltaEnvioCommand(
            comercioOperador.Id,
            request.ModalidadServicio,
            request.Destinatario,
            request.Direccion,
            request.Bultos,
            request.FranjaHorariaId,
            request.ReferenciaExterna);

        var resultado = await _procesador.ProcesarAsync(comercioOperador.OperadorId, alta, null, cancellationToken);
        if (!resultado.Duplicado)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultado;
    }
}
