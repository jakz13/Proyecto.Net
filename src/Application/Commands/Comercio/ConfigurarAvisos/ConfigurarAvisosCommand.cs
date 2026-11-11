using MediatR;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;

/// <summary>
/// Comando para configurar la URL, la versión de contrato y los eventos ante los cuales el comercio recibirá avisos automáticos (UC-29, RF-36).
/// </summary>
public record ConfigurarAvisosCommand(
    string UrlWebhook,
    IReadOnlyList<string> Eventos,
    string? ClaveFirma = null,
    bool Activa = true,
    string VersionContrato = SuscripcionAviso.ContratoV1) : IRequest<ConfiguracionAvisosResultDto>;

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
    string Mensaje,
    string VersionContrato = SuscripcionAviso.ContratoV1);

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
