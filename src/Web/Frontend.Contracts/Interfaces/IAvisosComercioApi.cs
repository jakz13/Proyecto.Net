using UltimaMilla.Frontend.Contracts.Modelos.Avisos;

namespace UltimaMilla.Frontend.Contracts.Interfaces;

/// <summary>
/// Contrato de servicio para configurar, monitorear y reintentar avisos automáticos y webhooks del comercio.
/// </summary>
public interface IAvisosComercioApi
{
    /// <summary>
    /// Obtiene la configuración actual de webhooks, catálogo de eventos y el historial reciente de entregas.
    /// </summary>
    Task<ConfiguracionAvisosDto> ObtenerConfiguracionAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Guarda o actualiza la suscripción a avisos automáticos del comercio.
    /// </summary>
    Task<ConfiguracionAvisosDto> GuardarConfiguracionAsync(GuardarConfiguracionAvisosDto dto, CancellationToken cancelacion = default);

    /// <summary>
    /// Dispara un envío de prueba (ping/diagnóstico) con firma HMAC al webhook configurado.
    /// </summary>
    Task<ResultadoPruebaWebhookDto> EnviarPruebaWebhookAsync(string? urlOpcional = null, CancellationToken cancelacion = default);

    /// <summary>
    /// Consulta el historial de avisos enviados hacia los sistemas del comercio con filtros por estado o código.
    /// </summary>
    Task<IReadOnlyList<AvisoHistorialDetalleDto>> ConsultarHistorialAvisosAsync(
        string? estado = null,
        Guid? envioId = null,
        string? codigoSeguimiento = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Solicita el reenvío manual de un aviso que resultó fallido o agotado.
    /// </summary>
    Task<ResultadoReintentoAvisoDto> ReintentarAvisoAsync(Guid avisoId, CancellationToken cancelacion = default);
}
