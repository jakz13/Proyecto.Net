using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;

/// <summary>Solicitud de retiro desde el portal del comercio.</summary>
public class SolicitarRetiroCommandHandler : IRequestHandler<SolicitarRetiroCommand, SolicitarRetiroResultDto>
{
    private readonly SolicitarRetiroProcesador _procesador;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IComercioOperadorRepository? _comercioOperadorRepository;

    public SolicitarRetiroCommandHandler(
        SolicitarRetiroProcesador procesador,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork,
        IComercioOperadorRepository? comercioOperadorRepository = null)
    {
        _procesador = procesador;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
        _comercioOperadorRepository = comercioOperadorRepository;
    }

    public async Task<SolicitarRetiroResultDto> Handle(SolicitarRetiroCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Un usuario de comercio solo puede solicitar retiros de su propio comercio, sin importar lo que traiga el pedido
        if (_currentTenant.ComercioOperadorId.HasValue)
        {
            if (_comercioOperadorRepository != null)
            {
                var relacion = await _comercioOperadorRepository.GetByIdAsync(_currentTenant.ComercioOperadorId.Value, cancellationToken);
                if (relacion != null && !relacion.EstaActivo)
                {
                    throw new DomainValidationException("El comercio se encuentra dado de baja o inactivo. No se permite solicitar retiros.");
                }
            }

            request = request with { ComercioId = _currentTenant.ComercioOperadorId.Value };
        }

        var resultado = await _procesador.ProcesarAsync(_currentTenant.OperadorId, request, cancellationToken);
        if (!resultado.Duplicado)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultado;
    }
}

/// <summary>Solicitud de retiro por API: se autentica con la clave del comercio, igual que el alta de envíos por API.</summary>
public class SolicitarRetiroPorApiCommandHandler : IRequestHandler<SolicitarRetiroPorApiCommand, SolicitarRetiroResultDto>
{
    private readonly IComercioOperadorRepository _comercioOperadorRepository;
    private readonly SolicitarRetiroProcesador _procesador;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant? _currentTenant;

    public SolicitarRetiroPorApiCommandHandler(
        IComercioOperadorRepository comercioOperadorRepository,
        SolicitarRetiroProcesador procesador,
        IUnitOfWork unitOfWork,
        ICurrentTenant? currentTenant = null)
    {
        _comercioOperadorRepository = comercioOperadorRepository;
        _procesador = procesador;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
    }

    public async Task<SolicitarRetiroResultDto> Handle(SolicitarRetiroPorApiCommand request, CancellationToken cancellationToken)
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

        var solicitud = new SolicitarRetiroCommand(
            comercioOperador.Id,
            request.Contacto,
            request.Direccion,
            request.Bultos,
            request.Motivo,
            request.FranjaHorariaId,
            request.FechaSolicitada,
            request.ReferenciaExterna,
            Retiro.OrigenApi);

        var resultado = await _procesador.ProcesarAsync(comercioOperador.OperadorId, solicitud, cancellationToken);
        if (!resultado.Duplicado)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return resultado;
    }
}
