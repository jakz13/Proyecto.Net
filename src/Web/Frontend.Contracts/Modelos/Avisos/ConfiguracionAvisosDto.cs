namespace UltimaMilla.Frontend.Contracts.Modelos.Avisos;

/// <summary>
/// Configuración integral de avisos automáticos y webhooks del comercio (UC-29).
/// </summary>
public record ConfiguracionAvisosDto
{
    public Guid ComercioOperadorId { get; init; }
    public string UrlWebhook { get; init; } = string.Empty;
    public IReadOnlyList<string> EventosSuscritos { get; init; } = Array.Empty<string>();
    public string ClaveFirma { get; init; } = string.Empty;
    public bool Activa { get; init; } = true;
    public bool EstaConfigurado { get; init; }
    public IReadOnlyList<EventoDisponibleDto> CatalogoEventos { get; init; } = Array.Empty<EventoDisponibleDto>();
    public IReadOnlyList<AvisoHistorialDto> HistorialReciente { get; init; } = Array.Empty<AvisoHistorialDto>();
}

public record GuardarConfiguracionAvisosDto
{
    public string UrlWebhook { get; init; } = string.Empty;
    public IReadOnlyList<string> Eventos { get; init; } = Array.Empty<string>();
    public string? ClaveFirma { get; init; }
    public bool Activa { get; init; } = true;
}

public record EventoDisponibleDto(
    string Codigo,
    string Titulo,
    string Descripcion,
    string Categoria);

public record ResultadoPruebaWebhookDto(
    bool Exitoso,
    int CodigoHttp,
    string Mensaje,
    string PayloadEnviado,
    string FirmaGenerada,
    DateTime Timestamp);

public record AvisoHistorialDto(
    Guid Id,
    string TipoEvento,
    string CodigoSeguimiento,
    string Estado,
    int CodigoRespuestaHttp,
    DateTime Timestamp,
    int Intentos);

/// <summary>
/// Detalle completo de un aviso enviado hacia el sistema del comercio con payload y respuesta HTTP (UC-30).
/// </summary>
public record AvisoHistorialDetalleDto(
    Guid Id,
    Guid SuscripcionAvisoId,
    Guid EnvioId,
    string CodigoSeguimiento,
    string TipoEvento,
    string UrlWebhook,
    string Payload,
    string FirmaEnviada,
    string Estado,
    int Intentos,
    string? UltimaRespuestaHttp,
    int CodigoRespuestaHttp,
    DateTime Timestamp,
    DateTime? ProximoReintento);

/// <summary>
/// Resultado tras solicitar el reenvío manual de un aviso fallido o agotado (UC-30).
/// </summary>
public record ResultadoReintentoAvisoDto(
    Guid AvisoId,
    bool Exitoso,
    string Estado,
    int Intentos,
    string Mensaje,
    int CodigoHttp,
    DateTime Timestamp);
