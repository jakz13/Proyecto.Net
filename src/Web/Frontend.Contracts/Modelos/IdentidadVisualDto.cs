using UltimaMilla.Frontend.Contracts.Utilidades;

namespace UltimaMilla.Frontend.Contracts.Modelos;

/// <summary>
/// Representa la configuración de marca del operador logístico actual.
/// Se utiliza para la inyección dinámica de estilos en el portal del comercio y seguimiento público.
/// </summary>
public record IdentidadVisualDto
{
    public string OperadorId { get; init; } = string.Empty;
    public string NombreComercial { get; init; } = "Operador Logístico";
    public string ColorPrimario { get; init; } = "#1D4ED8";
    public string ColorSecundario { get; init; } = "#F97316";
    public string? LogoUrl { get; init; }
    public string? EmailContacto { get; init; }
    public string? TelefonoContacto { get; init; }

    /// <summary>
    /// Retorna el color de texto con contraste óptimo para el color primario (#FFFFFF o #0F172A).
    /// </summary>
    public string ColorTextoSobrePrimario => ColorUtil.ObtenerColorTextoParaFondo(ColorPrimario);

    /// <summary>
    /// Retorna el color de texto con contraste óptimo para el color secundario (#FFFFFF o #0F172A).
    /// </summary>
    public string ColorTextoSobreSecundario => ColorUtil.ObtenerColorTextoParaFondo(ColorSecundario);
}
