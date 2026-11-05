using System.Diagnostics;

namespace UltimaMilla.Application.Messaging;

/// <summary>
/// Contexto asíncrono del identificador de correlación (<c>X-Correlation-Id</c>) para seguir una operación
/// de extremo a extremo desde la app móvil o el portal, pasando por la API y el Outbox, hasta el Worker (UC-44, §6.12).
/// </summary>
public static class ContextoCorrelacion
{
    public const string NombreCabecera = "X-Correlation-Id";
    public const string EtiquetaActividad = "correlation_id";
    public const int LargoMaximo = 64;

    /// <summary>Fuente de actividades de la aplicación para trazas distribuidas (OpenTelemetry).</summary>
    public static readonly ActivitySource FuenteActividades = new("UltimaMilla");

    private static readonly AsyncLocal<string?> _correlationIdActual = new();
    private static readonly AsyncLocal<string?> _tenantIdActual = new();

    public static string? CorrelationIdActual
    {
        get => _correlationIdActual.Value;
        set => _correlationIdActual.Value = valorNormalizadoONull(value);
    }

    public static string? TenantIdActual
    {
        get => _tenantIdActual.Value;
        set => _tenantIdActual.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Valida el encabezado <c>X-Correlation-Id</c> recibido. Si falta, supera los 64 caracteres o contiene
    /// caracteres no imprimibles (prevención de inyección en logs), genera un identificador nuevo.
    /// </summary>
    public static string NormalizarOGenerar(string? candidato)
    {
        if (EsValido(candidato))
            return candidato!;

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Comprueba que el identificador no sea vacío, tenga a lo sumo 64 caracteres y solo contenga caracteres ASCII imprimibles sin espacios de control.
    /// </summary>
    public static bool EsValido(string? candidato)
    {
        if (string.IsNullOrWhiteSpace(candidato) || candidato.Length > LargoMaximo)
            return false;

        foreach (var caracter in candidato)
        {
            if (caracter < 0x21 || caracter > 0x7E)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Obtiene el identificador de correlación vigente en el flujo actual (middleware HTTP, consumidor del worker o Activity W3C).
    /// </summary>
    public static string ObtenerCorrelationIdActual()
    {
        if (EsValido(_correlationIdActual.Value))
            return _correlationIdActual.Value!;

        if (Activity.Current?.GetTagItem(EtiquetaActividad) is string etiqueta && EsValido(etiqueta))
            return etiqueta;

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId) && traceId != "00000000000000000000000000000000")
            return traceId;

        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Establece temporalmente el identificador de correlación y el inquilino en el flujo asíncrono actual.
    /// </summary>
    public static IDisposable IniciarAlcance(string correlationId, string? tenantId = null)
    {
        var normalizado = NormalizarOGenerar(correlationId);
        var anteriorCorrelation = _correlationIdActual.Value;
        var anteriorTenant = _tenantIdActual.Value;

        _correlationIdActual.Value = normalizado;
        if (!string.IsNullOrWhiteSpace(tenantId))
            _tenantIdActual.Value = tenantId.Trim();

        Activity.Current?.SetTag(EtiquetaActividad, normalizado);

        return new AlcanceRestauracion(anteriorCorrelation, anteriorTenant);
    }

    private static string? valorNormalizadoONull(string? valor) =>
        EsValido(valor) ? valor : null;

    private sealed class AlcanceRestauracion : IDisposable
    {
        private readonly string? _anteriorCorrelation;
        private readonly string? _anteriorTenant;

        public AlcanceRestauracion(string? anteriorCorrelation, string? anteriorTenant)
        {
            _anteriorCorrelation = anteriorCorrelation;
            _anteriorTenant = anteriorTenant;
        }

        public void Dispose()
        {
            _correlationIdActual.Value = _anteriorCorrelation;
            _tenantIdActual.Value = _anteriorTenant;
        }
    }
}

