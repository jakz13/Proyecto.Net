using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

/// <summary>
/// Ping de diagnóstico: hace una llamada HTTP real y firmada al webhook del comercio, por el mismo camino que usan los avisos
/// (mismas reglas de destino, mismo tiempo de espera, sin seguir redirecciones), y devuelve lo que el comercio respondió.
/// No crea un aviso ni cuenta en las métricas de entrega: es una prueba, no un evento del negocio.
/// </summary>
public class ProbarWebhookCommandHandler : IRequestHandler<ProbarWebhookCommand, ProbarWebhookResultDto>
{
    /// <summary>Clave con la que se firma el ping cuando el comercio todavía no configuró ninguna suscripción.</summary>
    private const string ClaveFirmaDeDemostracion = "demo-firma-hmac-sha256";

    private readonly ISuscripcionAvisoRepository _suscripcionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IWebhookSender _webhookSender;

    public ProbarWebhookCommandHandler(
        ISuscripcionAvisoRepository suscripcionRepository,
        ICurrentTenant currentTenant,
        IWebhookSender webhookSender)
    {
        _suscripcionRepository = suscripcionRepository;
        _currentTenant = currentTenant;
        _webhookSender = webhookSender;
    }

    public async Task<ProbarWebhookResultDto> Handle(ProbarWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var comercioOperadorId = _currentTenant.ComercioOperadorId;
        if (!comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Se requiere una sesión activa de comercio para probar el webhook.");
        }

        var suscripciones = await _suscripcionRepository.ListarPorComercioOperadorAsync(comercioOperadorId.Value, cancellationToken);

        // Se firma con la clave real del comercio para que el receptor pueda validar el ping igual que un aviso verdadero
        var claveFirma = suscripciones.Count > 0 ? suscripciones[0].ClaveFirma : ClaveFirmaDeDemostracion;

        string urlDestino;
        if (!string.IsNullOrWhiteSpace(request.UrlWebhookOpcional))
        {
            urlDestino = request.UrlWebhookOpcional.Trim();
        }
        else
        {
            if (suscripciones.Count == 0)
            {
                throw new DomainValidationException("No hay una URL de webhook configurada para probar. Guarde una configuración primero.");
            }

            urlDestino = suscripciones[0].UrlWebhook;
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
            mensaje = "Mensaje de prueba de conectividad y firma HMAC emitido por la Plataforma Última Milla.",
            datosPrueba = new
            {
                envioCodigo = "TRK-PRUEBA-0001",
                estado = "Admitido",
                destinatario = "Comercio Receptor Test"
            }
        };

        var payloadJson = JsonSerializer.Serialize(datosPayload, new JsonSerializerOptions { WriteIndented = true });
        var firma = FirmaWebhook.Calcular(claveFirma, payloadJson);

        // El puerto no lanza por fallas de red: devuelve el resultado, sea éxito, código de error del comercio o destino no alcanzable
        var resultado = await _webhookSender.EnviarAsync(urlDestino, payloadJson, firma, Guid.NewGuid(), cancellationToken);

        var mensaje = resultado.Exitoso
            ? $"'{urlDestino}' respondió {resultado.Detalle}. Verifique en su sistema que el mensaje llegó y que la firma se validó."
            : $"El envío a '{urlDestino}' no se completó: {resultado.Detalle}";

        return new ProbarWebhookResultDto(
            Exitoso: resultado.Exitoso,
            CodigoHttp: resultado.CodigoHttp ?? 0,
            Mensaje: mensaje,
            PayloadEnviado: payloadJson,
            FirmaGenerada: firma,
            Timestamp: timestampUtc);
    }
}
