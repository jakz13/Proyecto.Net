using UltimaMilla.Mobile.Configuration;

namespace UltimaMilla.Mobile.Services.Theme;

public class OperadorIdentidadVisualModel
{
    public Guid? OperadorId { get; set; }
    public string NombreComercial { get; set; } = "Última Milla";
    public string ColorPrimarioHex { get; set; } = "#1D4ED8";
    public string ColorSecundarioHex { get; set; } = "#F97316";
    public string? LogoUrl { get; set; }
    public string? TelefonoContacto { get; set; }
    public string? EmailContacto { get; set; }

    public ImageSource? LogoSource => CrearImageSource(LogoUrl);

    public bool TieneLogo => LogoSource is not null;

    public bool MostrarIniciales => LogoSource is null;

    public string Iniciales
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NombreComercial))
                return "UM";

            var palabras = NombreComercial.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (palabras.Length == 1)
                return palabras[0].Length <= 2 ? palabras[0].ToUpperInvariant() : palabras[0][..2].ToUpperInvariant();

            return $"{char.ToUpperInvariant(palabras[0][0])}{char.ToUpperInvariant(palabras[1][0])}";
        }
    }

    public static ImageSource? CrearImageSource(string? rawLogoUrl)
    {
        if (string.IsNullOrWhiteSpace(rawLogoUrl))
            return null;

        var limpio = rawLogoUrl.Trim();

        // 1. Soporte para Data URI en Base64 (ej: data:image/png;base64,iVBORw0KGgo...)
        if (limpio.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            // Evitar SVG en data URI si no es rasterizado nativamente
            if (limpio.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase))
                return null;

            var comaIndex = limpio.IndexOf(',');
            if (comaIndex > 0 && comaIndex < limpio.Length - 1)
            {
                var base64Data = limpio[(comaIndex + 1)..].Trim();
                try
                {
                    var bytes = Convert.FromBase64String(base64Data);
                    if (bytes.Length > 0)
                    {
                        return ImageSource.FromStream(() => new MemoryStream(bytes));
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        // 2. Evitar URLs .svg directas que ImageView nativo no rasteriza sin librería externa, usando iniciales como fallback limpio
        if (limpio.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return null;

        // 3. Rutas relativas al servidor (ej: /uploads/logos/... o /api/v1/identidad-visual/logos/...)
        if (limpio.StartsWith('/'))
        {
            var baseUrl = ApiEndpoints.BaseUrl.TrimEnd('/');
            limpio = $"{baseUrl}{limpio}";
        }

        if (Uri.TryCreate(limpio, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ImageSource.FromUri(uri);
        }

        return null;
    }
}

public interface IOperadorThemeService
{
    OperadorIdentidadVisualModel IdentidadActual { get; }
    event EventHandler<OperadorIdentidadVisualModel>? IdentidadVisualCambiada;

    void CargarDesdePreferencias();
    void AplicarIdentidad(OperadorIdentidadVisualModel identidad);
    Task<OperadorIdentidadVisualModel?> DescargarYActualizarIdentidadAsync(CancellationToken ct = default);
    Task<OperadorIdentidadVisualModel?> SincronizarIdentidadVisualAsync(CancellationToken ct = default);
}
