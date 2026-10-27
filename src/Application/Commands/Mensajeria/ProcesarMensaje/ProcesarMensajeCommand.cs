using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;

/// <summary>Procesa un mensaje recibido de la cola de forma idempotente: reprocesar el mismo <see cref="MessageId"/> no repite efectos (§6.8).</summary>
public record ProcesarMensajeCommand(
    Guid MessageId,
    string AgregadoOrigen,
    Guid AgregadoId,
    string TipoEvento,
    string Payload) : IRequest<ResultadoProcesamientoMensaje>;

public enum EstadoProcesamientoMensaje
{
    Procesado,
    /// <summary>La clave de idempotencia ya existía: el mensaje se descarta sin repetir efectos.</summary>
    Duplicado,
    /// <summary>Evento de integración sin consumidor registrado: se confirma y no se reintenta.</summary>
    SinConsumidor
}

public record ResultadoProcesamientoMensaje(EstadoProcesamientoMensaje Estado, int NotificacionesEnviadas, int AvisosGenerados);
