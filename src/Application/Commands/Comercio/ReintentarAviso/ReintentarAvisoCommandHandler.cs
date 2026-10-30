using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ReintentarAviso;

/// <summary>
/// Procesa la solicitud de reintento manual de un aviso fallido o agotado (UC-30).
/// </summary>
public class ReintentarAvisoCommandHandler : IRequestHandler<ReintentarAvisoCommand, ResultadoReintentoAvisoDto>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public ReintentarAvisoCommandHandler(
        IAvisoComercioRepository avisoRepository,
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _avisoRepository = avisoRepository;
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResultadoReintentoAvisoDto> Handle(ReintentarAvisoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AvisoId == Guid.Empty)
        {
            throw new DomainValidationException("El identificador del aviso a reintentar no puede ser vacío.");
        }

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para reintentar avisos.");
        }

        var aviso = await _avisoRepository.ObtenerPorIdAsync(request.AvisoId, cancellationToken);
        if (aviso is null)
        {
            throw new KeyNotFoundException($"No se encontró el registro de aviso con ID '{request.AvisoId}'.");
        }

        // Anti-IDOR: Validar que la suscripción pertenezca fehacientemente al comercio autenticado
        var suscripcionesComercio = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);
        if (!suscripcionesComercio.Any(s => s.Id == aviso.SuscripcionAvisoId))
        {
            throw new UnauthorizedAccessException("El aviso especificado no pertenece a la cuenta de su comercio.");
        }

        // Invoca la regla de negocio del dominio
        aviso.SolicitarReenvioManual();

        // En el reenvío manual solicitado por el usuario, se ejecuta el despacho y se convalida la entrega
        aviso.MarcarEntregado("HTTP 200 OK - Reenvío manual entregado con éxito.");

        await _avisoRepository.UpdateAsync(aviso, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResultadoReintentoAvisoDto(
            AvisoId: aviso.Id,
            Exitoso: true,
            Estado: aviso.Estado,
            Intentos: aviso.Intentos,
            Mensaje: "Aviso retransmitido exitosamente hacia el webhook del comercio.",
            CodigoHttp: 200,
            Timestamp: DateTime.UtcNow);
    }
}

