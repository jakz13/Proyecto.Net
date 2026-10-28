using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Ports;

/// <summary>Mensaje del outbox listo para publicarse en el broker.</summary>
public record MensajeSaliente(
    Guid MessageId,
    string AgregadoOrigen,
    Guid AgregadoId,
    string TipoEvento,
    string Payload,
    string? CorrelationId);

/// <summary>Puerto de publicación en la cola de mensajes. Debe lanzar excepción si el broker no confirma la recepción.</summary>
public interface IMessagePublisher
{
    Task PublicarAsync(MensajeSaliente mensaje, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de una llamada HTTP saliente hacia el sistema de un comercio.</summary>
public record ResultadoWebhook(bool Exitoso, int? CodigoHttp, string Detalle);

/// <summary>Puerto de llamadas HTTP salientes firmadas hacia los webhooks de los comercios. Nunca debe lanzar por fallas de red.</summary>
public interface IWebhookSender
{
    Task<ResultadoWebhook> EnviarAsync(string urlWebhook, string payload, string firma, Guid avisoId, CancellationToken cancellationToken = default);
}

/// <summary>Datos mínimos para notificar un cambio de estado al destinatario (UC-35).</summary>
public record NotificacionDestinatario(
    Guid EnvioId,
    string CodigoSeguimiento,
    string NombreDestinatario,
    string Canal,
    string Contacto,
    EstadoEnvio Estado);

/// <summary>Puerto de envío de notificaciones al destinatario por correo o mensajería.</summary>
public interface INotificacionDestinatarioService
{
    Task EnviarAsync(NotificacionDestinatario notificacion, CancellationToken cancellationToken = default);
}
