using MediatR;

namespace UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

/// <summary>
/// Comando para configurar la URL y los eventos ante los cuales el comercio recibirá avisos automáticos (UC-29, RF-33, RF-28).
/// </summary>
public record ConfigurarAvisosCommand(
    string UrlWebhook,
    IReadOnlyList<string> Eventos,
    string? ClaveFirma = null,
    bool Activa = true) : IRequest<ConfiguracionAvisosResultDto>;

/// <summary>
/// Comando para realizar un envío de prueba (ping/diagnóstico) al webhook configurado del comercio.
/// </summary>
public record ProbarWebhookCommand(
    string? UrlWebhookOpcional = null) : IRequest<ProbarWebhookResultDto>;

public record ConfiguracionAvisosResultDto(
    Guid ComercioOperadorId,
    string UrlWebhook,
    IReadOnlyList<string> Eventos,
    string ClaveFirma,
    bool Activa,
    DateTime FechaActualizacion,
    string Mensaje);

public record ProbarWebhookResultDto(
    bool Exitoso,
    int CodigoHttp,
    string Mensaje,
    string PayloadEnviado,
    string FirmaGenerada,
    DateTime Timestamp);

public record AvisoHistorialItemDto(
    Guid Id,
    string TipoEvento,
    string CodigoSeguimiento,
    string Estado,
    int CodigoRespuestaHttp,
    DateTime Timestamp,
    int Intentos);

