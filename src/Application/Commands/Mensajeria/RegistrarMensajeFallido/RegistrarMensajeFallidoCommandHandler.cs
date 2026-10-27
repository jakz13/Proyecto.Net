using MediatR;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.RegistrarMensajeFallido;

public class RegistrarMensajeFallidoCommandHandler : IRequestHandler<RegistrarMensajeFallidoCommand, Guid>
{
    private readonly IMensajeFallidoRepository _mensajeFallidoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrarMensajeFallidoCommandHandler(IMensajeFallidoRepository mensajeFallidoRepository, IUnitOfWork unitOfWork)
    {
        _mensajeFallidoRepository = mensajeFallidoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RegistrarMensajeFallidoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TipoEvento))
            throw new DomainValidationException("El tipo de mensaje fallido no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(request.Payload))
            throw new DomainValidationException("El payload del mensaje fallido no puede ser vacío.");

        // Un reintento manual (UC-42) que vuelve a fallar actualiza el registro original en lugar de duplicarlo
        if (string.Equals(request.AgregadoOrigen, EventoEnvioPublicado.OrigenColaFallidos, StringComparison.Ordinal))
        {
            var existente = await _mensajeFallidoRepository.GetByIdAsync(request.AgregadoId, cancellationToken);
            if (existente is not null)
            {
                existente.RegistrarReintentoFallido(request.Excepcion);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return existente.Id;
            }
        }

        var mensaje = new MensajeFallidoCola(
            Guid.NewGuid(),
            request.TipoEvento,
            request.Payload,
            request.Excepcion,
            "Fallido",
            request.ColaOrigen,
            PayloadMensaje.OperadorIdDe(request.Payload));

        // El registro nace con un intento; se reflejan los restantes para que la cantidad visible sea la real
        for (var i = 1; i < request.Intentos; i++)
            mensaje.RegistrarReintentoFallido(request.Excepcion);

        await _mensajeFallidoRepository.AddAsync(mensaje, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return mensaje.Id;
    }
}
