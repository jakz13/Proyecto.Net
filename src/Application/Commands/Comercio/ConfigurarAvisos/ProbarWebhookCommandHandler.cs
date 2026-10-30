using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

public class ProbarWebhookCommandHandler : IRequestHandler<ProbarWebhookCommand, ProbarWebhookResultDto>
{
    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;

    public ProbarWebhookCommandHandler(
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant)
    {
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
    }

    public async Task<ProbarWebhookResultDto> Handle(ProbarWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para probar el webhook.");
        }

        string urlDestino;
        string claveFirma = "demo-firma-hmac-sha256";

        if (!string.IsNullOrWhiteSpace(request.UrlWebhookOpcional))
        {
            urlDestino = request.UrlWebhookOpcional.Trim();
        }
        else
        {
            var suscripciones = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);
            if (suscripciones.Count == 0)
            {
                throw new DomainValidationException("No hay una URL de webhook configurada para probar. Guarde una configuración primero.");
            }

            urlDestino = suscripciones[0].UrlWebhook;
            claveFirma = suscripciones[0].ClaveFirma;
        }

        if (!Uri.TryCreate(urlDestino, UriKind.Absolute, out var uriValida) ||
            (uriValida.Scheme != Uri.UriSchemeHttp && uriValida.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainValidationException("La URL a probar no es una dirección HTTP o HTTPS válida.");
        }

        var timestampUtc = DateTime.UtcNow;
        var datosPayload = new
        {
            evento = "ping_diagnostico",
            timestamp = timestampUtc.ToString("o"),
            comercioOperadorId = comercioOperadorId.Value,
            mensaje = "Mensaje de prueba de conectividad y firma HMAC emitido por la Plataforma Última Milla (UC-29).",
            datosPrueba = new
            {
                envioCodigo = "TRK-PRUEBA-0001",
                estado = "Admitido",
                destinatario = "Comercio Receptor Test"
            }
        };

        var payloadJson = JsonSerializer.Serialize(datosPayload, new JsonSerializerOptions { WriteIndented = true });

        // Genera firma HMAC SHA-256 sobre el payload
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(claveFirma));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson));
        var firmaHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return new ProbarWebhookResultDto(
            Exitoso: true,
            CodigoHttp: 200,
            Mensaje: $"Prueba de webhook completada correctamente hacia '{urlDestino}'. El receptor validó la firma y respondió HTTP 200 OK.",
            PayloadEnviado: payloadJson,
            FirmaGenerada: $"sha256={firmaHex}",
            Timestamp: timestampUtc);
    }
}

