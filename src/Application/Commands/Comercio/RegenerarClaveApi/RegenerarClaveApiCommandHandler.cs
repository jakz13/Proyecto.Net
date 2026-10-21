using System.Security.Cryptography;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;

namespace UltimaMilla.Application.Commands.Comercio.RegenerarClaveApi;

public class RegenerarClaveApiCommandHandler : IRequestHandler<RegenerarClaveApiCommand, ClaveApiGeneradaDto>
{
    private readonly IComercioOperadorRepository _comercioOperadorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public RegenerarClaveApiCommandHandler(
        IComercioOperadorRepository comercioOperadorRepository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _comercioOperadorRepository = comercioOperadorRepository;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<ClaveApiGeneradaDto> Handle(RegenerarClaveApiCommand request, CancellationToken cancellationToken)
    {
        var comercioOperadorId = _currentTenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario no pertenece a un comercio.");

        var comercioOperador = await _comercioOperadorRepository.GetByIdAsync(comercioOperadorId, cancellationToken)
            ?? throw new NotFoundException("No se encontró la relación del comercio con el operador.");
        if (comercioOperador.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el comercio no corresponde al operador actual.");

        var clave = "um_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        comercioOperador.RegenerarClaveApi(ComercioOperador.CalcularHuellaClaveApi(clave));
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ClaveApiGeneradaDto(clave, "Guarde esta clave ahora: no volverá a mostrarse. La clave anterior ya no es válida.");
    }
}
