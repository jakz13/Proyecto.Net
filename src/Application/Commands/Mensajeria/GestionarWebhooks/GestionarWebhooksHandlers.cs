using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;

public class CrearSuscripcionAvisoCommandHandler : IRequestHandler<CrearSuscripcionAvisoCommand, SuscripcionAvisoDto>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly IComercioOperadorRepository _comercioOperadorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public CrearSuscripcionAvisoCommandHandler(
        IAvisoComercioRepository avisoRepository,
        IComercioOperadorRepository comercioOperadorRepository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _avisoRepository = avisoRepository;
        _comercioOperadorRepository = comercioOperadorRepository;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<SuscripcionAvisoDto> Handle(CrearSuscripcionAvisoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ComercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de la relación comercio-operador es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.UrlWebhook)
            || !Uri.TryCreate(request.UrlWebhook.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException("La URL del webhook debe ser una URL HTTP o HTTPS válida.");
        }

        if (string.IsNullOrWhiteSpace(request.ClaveFirma) || request.ClaveFirma.Trim().Length < 8)
            throw new DomainValidationException("La clave secreta de firma HMAC debe tener al menos 8 caracteres.");

        // Anti-IDOR: verificar que el comercio-operador exista y pertenezca al operador actual
        var comercioOperador = await _comercioOperadorRepository.GetByIdAsync(request.ComercioOperadorId, cancellationToken)
            ?? throw new NotFoundException($"La relación comercio-operador '{request.ComercioOperadorId}' no existe.");

        if (comercioOperador.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("No tiene autorización para gestionar webhooks de un comercio de otro operador.");

        var suscripcion = new SuscripcionAviso(
            Guid.NewGuid(),
            comercioOperador.Id,
            request.TipoEvento,
            request.UrlWebhook.Trim(),
            request.ClaveFirma.Trim());

        await _avisoRepository.AddSuscripcionAsync(suscripcion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SuscripcionAvisoDto(
            suscripcion.Id,
            suscripcion.ComercioOperadorId,
            suscripcion.TipoEvento,
            suscripcion.UrlWebhook,
            suscripcion.Activa);
    }
}

public class ListarSuscripcionesAvisoQueryHandler : IRequestHandler<ListarSuscripcionesAvisoQuery, IReadOnlyList<SuscripcionAvisoDto>>
{
    private readonly IAvisoComercioRepository _avisoRepository;

    public ListarSuscripcionesAvisoQueryHandler(IAvisoComercioRepository avisoRepository)
    {
        _avisoRepository = avisoRepository;
    }

    public async Task<IReadOnlyList<SuscripcionAvisoDto>> Handle(ListarSuscripcionesAvisoQuery request, CancellationToken cancellationToken)
    {
        var suscripciones = await _avisoRepository.ListarSuscripcionesAsync(request.ComercioOperadorId, cancellationToken);
        return suscripciones
            .Select(s => new SuscripcionAvisoDto(s.Id, s.ComercioOperadorId, s.TipoEvento, s.UrlWebhook, s.Activa))
            .ToList();
    }
}

public class DesactivarSuscripcionAvisoCommandHandler : IRequestHandler<DesactivarSuscripcionAvisoCommand, SuscripcionAvisoDto>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DesactivarSuscripcionAvisoCommandHandler(
        IAvisoComercioRepository avisoRepository,
        IUnitOfWork unitOfWork)
    {
        _avisoRepository = avisoRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SuscripcionAvisoDto> Handle(DesactivarSuscripcionAvisoCommand request, CancellationToken cancellationToken)
    {
        if (request.SuscripcionId == Guid.Empty)
            throw new DomainValidationException("El identificador de la suscripción no puede ser vacío.");

        var suscripcion = await _avisoRepository.GetSuscripcionParaEdicionAsync(request.SuscripcionId, cancellationToken)
            ?? throw new NotFoundException($"La suscripción '{request.SuscripcionId}' no fue encontrada.");

        if (request.ComercioOperadorId.HasValue
            && request.ComercioOperadorId.Value != Guid.Empty
            && suscripcion.ComercioOperadorId != request.ComercioOperadorId.Value)
        {
            throw new UnauthorizedAccessException("La suscripción indicada no pertenece al comercio especificado.");
        }

        suscripcion.Desactivar();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SuscripcionAvisoDto(
            suscripcion.Id,
            suscripcion.ComercioOperadorId,
            suscripcion.TipoEvento,
            suscripcion.UrlWebhook,
            suscripcion.Activa);
    }
}

public class ListarHistorialAvisosQueryHandler : IRequestHandler<ListarHistorialAvisosQuery, IReadOnlyList<AvisoComercioHistorialDto>>
{
    private readonly IAvisoComercioRepository _avisoRepository;

    public ListarHistorialAvisosQueryHandler(IAvisoComercioRepository avisoRepository)
    {
        _avisoRepository = avisoRepository;
    }

    public async Task<IReadOnlyList<AvisoComercioHistorialDto>> Handle(ListarHistorialAvisosQuery request, CancellationToken cancellationToken)
    {
        var avisos = await _avisoRepository.ListarHistorialAvisosAsync(
            request.ComercioOperadorId,
            request.Estado,
            request.Limite,
            cancellationToken);

        return avisos
            .Select(a => new AvisoComercioHistorialDto(
                a.Id,
                a.SuscripcionAvisoId,
                a.EnvioId,
                a.Estado,
                a.Intentos,
                a.ProximoReintento,
                a.UltimaRespuestaHttp,
                a.Timestamp))
            .ToList();
    }
}

public class ReenviarAvisoManualCommandHandler : IRequestHandler<ReenviarAvisoManualCommand, AvisoComercioHistorialDto>
{
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly AvisoComercioEntregador _entregador;
    private readonly ICurrentTenant _currentTenant;

    public ReenviarAvisoManualCommandHandler(
        IAvisoComercioRepository avisoRepository,
        AvisoComercioEntregador entregador,
        ICurrentTenant currentTenant)
    {
        _avisoRepository = avisoRepository;
        _entregador = entregador;
        _currentTenant = currentTenant;
    }

    public async Task<AvisoComercioHistorialDto> Handle(ReenviarAvisoManualCommand request, CancellationToken cancellationToken)
    {
        if (request.AvisoId == Guid.Empty)
            throw new DomainValidationException("El identificador del aviso no puede ser vacío.");

        var aviso = await _avisoRepository.GetAvisoByIdAsync(request.AvisoId, cancellationToken)
            ?? throw new NotFoundException($"El aviso '{request.AvisoId}' no fue encontrado.");

        // Un usuario de comercio solo puede reenviar avisos de sus propias suscripciones (segundo nivel de aislamiento)
        if (_currentTenant.ComercioOperadorId is { } comercioPropio)
        {
            var suscripcion = await _avisoRepository.GetSuscripcionParaEdicionAsync(aviso.SuscripcionAvisoId, cancellationToken);
            if (suscripcion is null || suscripcion.ComercioOperadorId != comercioPropio)
                throw new UnauthorizedAccessException("El aviso indicado no pertenece al comercio autenticado.");
        }

        aviso.PrepararReenvioManual();
        await _entregador.EntregarAsync(new[] { aviso }, cancellationToken);

        return new AvisoComercioHistorialDto(
            aviso.Id,
            aviso.SuscripcionAvisoId,
            aviso.EnvioId,
            aviso.Estado,
            aviso.Intentos,
            aviso.ProximoReintento,
            aviso.UltimaRespuestaHttp,
            aviso.Timestamp);
    }
}

