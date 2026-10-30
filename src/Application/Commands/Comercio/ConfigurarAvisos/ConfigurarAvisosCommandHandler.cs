using System.Security.Cryptography;
using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

public class ConfigurarAvisosCommandHandler : IRequestHandler<ConfigurarAvisosCommand, ConfiguracionAvisosResultDto>
{
    private static readonly HashSet<string> EventosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "EnvioAdmitido",
        "EnDeposito",
        "EnTransito",
        "Entregado",
        "NoEntregado",
        "Reprogramado",
        "Extraviado"
    };

    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public ConfigurarAvisosCommandHandler(
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfiguracionAvisosResultDto> Handle(ConfigurarAvisosCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UrlWebhook))
            throw new DomainValidationException("La URL del webhook es obligatoria.");

        var urlLimpia = request.UrlWebhook.Trim();
        if (!Uri.TryCreate(urlLimpia, UriKind.Absolute, out var uriResultado) ||
            (uriResultado.Scheme != Uri.UriSchemeHttp && uriResultado.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException("La URL del webhook debe ser una dirección HTTP o HTTPS válida.");
        }

        if (request.Eventos is null || request.Eventos.Count == 0)
            throw new DomainValidationException("Debe seleccionar al menos un tipo de evento para recibir avisos automáticos.");

        if (request.Eventos.Any(string.IsNullOrWhiteSpace))
            throw new DomainValidationException("La lista de eventos contiene valores vacíos o no válidos.");

        var eventosNormalizados = request.Eventos
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (eventosNormalizados.Count == 0)
            throw new DomainValidationException("Debe indicar al menos un evento válido.");

        foreach (var evento in eventosNormalizados)
        {
            if (!EventosPermitidos.Contains(evento))
            {
                throw new DomainValidationException($"El tipo de evento '{evento}' no es válido para suscripción de avisos.");
            }
        }

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para configurar avisos automáticos.");
        }

        // Genera una clave de firma HMAC si el comercio no especificó una propia
        var claveFirma = string.IsNullOrWhiteSpace(request.ClaveFirma)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : request.ClaveFirma.Trim();

        // Obtiene las suscripciones previas para reemplazarlas de forma idempotente
        var suscripcionesExistentes = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);
        if (suscripcionesExistentes.Count > 0)
        {
            await _suscripcionRepository.RemoveRangeAsync(suscripcionesExistentes, cancellationToken);
        }

        // Crea las nuevas suscripciones por cada evento elegido
        foreach (var tipoEvento in eventosNormalizados)
        {
            var suscripcion = new SuscripcionAviso(
                Guid.NewGuid(),
                comercioOperadorId.Value,
                tipoEvento,
                urlLimpia,
                claveFirma);

            if (!request.Activa)
            {
                suscripcion.Desactivar();
            }

            await _suscripcionRepository.AddAsync(suscripcion, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfiguracionAvisosResultDto(
            comercioOperadorId.Value,
            urlLimpia,
            eventosNormalizados,
            claveFirma,
            request.Activa,
            DateTime.UtcNow,
            "Suscripción de avisos automáticos guardada exitosamente.");
    }
}

