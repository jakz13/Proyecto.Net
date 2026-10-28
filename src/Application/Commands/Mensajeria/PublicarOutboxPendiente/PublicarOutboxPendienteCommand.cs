using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.PublicarOutboxPendiente;

/// <summary>Publica en el broker los mensajes del outbox que aún no salieron (patrón outbox, §6.8).</summary>
public record PublicarOutboxPendienteCommand(int TamanoLote = 50) : IRequest<ResultadoPublicacionOutbox>;

/// <param name="Publicados">Mensajes publicados y marcados como procesados.</param>
/// <param name="Pendientes">Mensajes que quedaron sin publicar por una falla del broker (se reintentan en la próxima vuelta).</param>
/// <param name="Error">Descripción de la falla del broker, si la hubo.</param>
public record ResultadoPublicacionOutbox(int Publicados, int Pendientes, string? Error);
