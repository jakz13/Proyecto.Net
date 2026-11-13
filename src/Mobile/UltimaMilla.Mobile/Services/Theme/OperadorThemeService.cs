using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Configuration;

namespace UltimaMilla.Mobile.Services.Theme;

public class OperadorThemeService : IOperadorThemeService
{
    private const string PrefKeyNombreComercial = "theme_nombre_comercial";
    private const string PrefKeyColorPrimario = "theme_color_primario";
    private const string PrefKeyColorSecundario = "theme_color_secundario";
    private const string PrefKeyLogoUrl = "theme_logo_url";
    private const string PrefKeyTelefono = "theme_telefono";
    private const string PrefKeyEmail = "theme_email";
    private const string PrefKeyOperadorId = "theme_operador_id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OperadorThemeService> _logger;

    public OperadorIdentidadVisualModel IdentidadActual { get; private set; } = new();

    public event EventHandler<OperadorIdentidadVisualModel>? IdentidadVisualCambiada;

    public OperadorThemeService(IHttpClientFactory httpClientFactory, ILogger<OperadorThemeService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public void CargarDesdePreferencias()
    {
        try
        {
            var nombre = Preferences.Default.Get(PrefKeyNombreComercial, "Última Milla");
            var colorPrimario = Preferences.Default.Get(PrefKeyColorPrimario, "#1D4ED8");
            var colorSecundario = Preferences.Default.Get(PrefKeyColorSecundario, "#F97316");
            var logoUrl = Preferences.Default.Get(PrefKeyLogoUrl, string.Empty);
            var telefono = Preferences.Default.Get(PrefKeyTelefono, string.Empty);
            var email = Preferences.Default.Get(PrefKeyEmail, string.Empty);
            var operadorIdStr = Preferences.Default.Get(PrefKeyOperadorId, string.Empty);

            Guid? opId = Guid.TryParse(operadorIdStr, out var id) ? id : null;

            var modelo = new OperadorIdentidadVisualModel
            {
                OperadorId = opId,
                NombreComercial = string.IsNullOrWhiteSpace(nombre) ? "Última Milla" : nombre,
                ColorPrimarioHex = NormalizarHex(colorPrimario, "#1D4ED8"),
                ColorSecundarioHex = NormalizarHex(colorSecundario, "#F97316"),
                LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl,
                TelefonoContacto = string.IsNullOrWhiteSpace(telefono) ? null : telefono,
                EmailContacto = string.IsNullOrWhiteSpace(email) ? null : email
            };

            AplicarIdentidadInterno(modelo, persistir: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar identidad visual desde preferencias.");
        }
    }

    public void AplicarIdentidad(OperadorIdentidadVisualModel identidad)
    {
        ArgumentNullException.ThrowIfNull(identidad);
        AplicarIdentidadInterno(identidad, persistir: true);
    }

    private void AplicarIdentidadInterno(OperadorIdentidadVisualModel identidad, bool persistir)
    {
        identidad.ColorPrimarioHex = NormalizarHex(identidad.ColorPrimarioHex, "#1D4ED8");
        identidad.ColorSecundarioHex = NormalizarHex(identidad.ColorSecundarioHex, "#F97316");

        var primario = ParsearColor(identidad.ColorPrimarioHex, Color.FromArgb("#1D4ED8"));
        var secundarioRaw = ParsearColor(identidad.ColorSecundarioHex, Color.FromArgb("#F97316"));
        var secundario = GarantizarContrasteSecundario(primario, secundarioRaw);

        identidad.ColorSecundarioHex = $"#{Math.Clamp((int)(secundario.Red * 255), 0, 255):X2}{Math.Clamp((int)(secundario.Green * 255), 0, 255):X2}{Math.Clamp((int)(secundario.Blue * 255), 0, 255):X2}";

        IdentidadActual = identidad;

        if (persistir)
        {
            try
            {
                Preferences.Default.Set(PrefKeyNombreComercial, identidad.NombreComercial);
                Preferences.Default.Set(PrefKeyColorPrimario, identidad.ColorPrimarioHex);
                Preferences.Default.Set(PrefKeyColorSecundario, identidad.ColorSecundarioHex);
                Preferences.Default.Set(PrefKeyLogoUrl, identidad.LogoUrl ?? string.Empty);
                Preferences.Default.Set(PrefKeyTelefono, identidad.TelefonoContacto ?? string.Empty);
                Preferences.Default.Set(PrefKeyEmail, identidad.EmailContacto ?? string.Empty);
                if (identidad.OperadorId.HasValue)
                {
                    Preferences.Default.Set(PrefKeyOperadorId, identidad.OperadorId.Value.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudieron persistir las preferencias de identidad visual.");
            }
        }

        // Actualizar recursos de Application en el hilo de UI (síncrono si ya estamos en MainThread)
        Action aplicarRecursos = () =>
        {
            try
            {
                var primarioOscuro = AjustarBrillo(primario, -0.15f);
                var primarioSuave = MezclarConBlanco(primario, 0.14f);
                var secundarioOscuro = AjustarBrillo(secundario, -0.15f);
                var secundarioSuave = MezclarConBlanco(secundario, 0.15f);

                var headerBg = Color.FromArgb("#0F172A");
                var headerText = Color.FromArgb("#FFFFFF");
                var cardBorder = Color.FromArgb("#E2E8F0");
                var logoSource = identidad.LogoSource;

                var resources = Application.Current?.Resources;
                if (resources != null)
                {
                    resources["Primary"] = primario;
                    resources["PrimaryColor"] = primario;
                    resources["CorporatePrimaryColor"] = primario;
                    resources["PrimaryDark"] = primarioOscuro;
                    resources["PrimaryLight"] = primarioSuave;
                    resources["PrimaryLightColor"] = primarioSuave;
                    resources["PrimarySoftBackground"] = primarioSuave;
                    resources["PrimaryBrush"] = new SolidColorBrush(primario);
                    resources["CorporatePrimaryBrush"] = new SolidColorBrush(primario);

                    resources["Secondary"] = secundario;
                    resources["SecondaryColor"] = secundario;
                    resources["CorporateSecondaryColor"] = secundario;
                    resources["SecondaryDark"] = secundarioOscuro;
                    resources["SecondaryLight"] = secundarioSuave;
                    resources["SecondaryLightColor"] = secundarioSuave;
                    resources["SecondarySoftBackground"] = secundarioSuave;
                    resources["SecondaryBrush"] = new SolidColorBrush(secundario);
                    resources["CorporateSecondaryBrush"] = new SolidColorBrush(secundario);
                    resources["AccentColor"] = secundario;
                    resources["AccentBrush"] = new SolidColorBrush(secundario);

                    resources["HeaderBackgroundColor"] = headerBg;
                    resources["HeaderTextColor"] = headerText;
                    resources["CardBorderColor"] = cardBorder;
                    resources["CardBorder"] = cardBorder;
                    resources["CardBorderBrush"] = new SolidColorBrush(cardBorder);

                    resources["NombreOperadorActual"] = identidad.NombreComercial;
                    resources["InicialesOperadorActual"] = identidad.Iniciales;
                    resources["LogoOperadorUrl"] = identidad.LogoUrl ?? string.Empty;
                    if (logoSource is not null)
                    {
                        resources["LogoSource"] = logoSource;
                    }
                    else
                    {
                        resources.Remove("LogoSource");
                    }
                    resources["TieneLogoOperador"] = logoSource is not null;
                    resources["MostrarInicialesOperador"] = logoSource is null;
                }

                if (Shell.Current != null)
                {
                    Shell.SetBackgroundColor(Shell.Current, headerBg);
                    Shell.SetForegroundColor(Shell.Current, headerText);
                    Shell.SetTitleColor(Shell.Current, headerText);
                    Shell.SetTabBarBackgroundColor(Shell.Current, Color.FromArgb("#0F172A"));
                    Shell.SetTabBarTitleColor(Shell.Current, secundario);
                    Shell.SetTabBarForegroundColor(Shell.Current, secundario);
                    Shell.SetTabBarUnselectedColor(Shell.Current, Color.FromArgb("#94A3B8"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inyectar recursos dinámicos de identidad visual en Application.Current.Resources.");
            }

            IdentidadVisualCambiada?.Invoke(this, identidad);
        };

        if (MainThread.IsMainThread)
        {
            aplicarRecursos();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(aplicarRecursos);
        }
    }

    public Task<OperadorIdentidadVisualModel?> SincronizarIdentidadVisualAsync(CancellationToken ct = default)
        => DescargarYActualizarIdentidadAsync(ct);

    public async Task<OperadorIdentidadVisualModel?> DescargarYActualizarIdentidadAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

            var response = await client.GetAsync("/api/mobile/v1/operador/identidad-visual", ct);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback secundario si se consulta endpoint general del Backoffice
                response = await client.GetAsync("/api/v1/identidad-visual", ct);
            }

            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<IdentidadVisualMobileResponseDto>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken: ct);

                if (dto != null)
                {
                    var nombre = !string.IsNullOrWhiteSpace(dto.NombreComercial) ? dto.NombreComercial : "Última Milla";
                    var colorPri = !string.IsNullOrWhiteSpace(dto.ColorPrimarioHex)
                        ? dto.ColorPrimarioHex
                        : (!string.IsNullOrWhiteSpace(dto.ColorPrimario) ? dto.ColorPrimario : "#1D4ED8");
                    var colorSec = !string.IsNullOrWhiteSpace(dto.ColorSecundarioHex)
                        ? dto.ColorSecundarioHex
                        : (!string.IsNullOrWhiteSpace(dto.ColorSecundario) ? dto.ColorSecundario : "#F97316");

                    Guid? opId = null;
                    if (!string.IsNullOrWhiteSpace(dto.OperadorId) && Guid.TryParse(dto.OperadorId, out var parsedGuid))
                    {
                        opId = parsedGuid;
                    }

                    var modelo = new OperadorIdentidadVisualModel
                    {
                        OperadorId = opId,
                        NombreComercial = nombre.Trim(),
                        ColorPrimarioHex = NormalizarHex(colorPri, "#1D4ED8"),
                        ColorSecundarioHex = NormalizarHex(colorSec, "#F97316"),
                        LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim(),
                        TelefonoContacto = string.IsNullOrWhiteSpace(dto.TelefonoContacto) ? null : dto.TelefonoContacto.Trim(),
                        EmailContacto = string.IsNullOrWhiteSpace(dto.EmailContacto) ? null : dto.EmailContacto.Trim()
                    };

                    AplicarIdentidad(modelo);
                    if (!string.IsNullOrWhiteSpace(dto.TipoPruebaEntregaExigida))
                    {
                        Preferences.Default.Set("ReglaOperativa_TipoPruebaEntrega", dto.TipoPruebaEntregaExigida);
                    }
                    if (dto.MaxIntentosEntrega.HasValue)
                    {
                        Preferences.Default.Set("ReglaOperativa_MaxIntentos", dto.MaxIntentosEntrega.Value);
                    }
                    _logger.LogInformation("Identidad visual del operador actualizada: {Nombre} ({Primario}, {Secundario}).", modelo.NombreComercial, modelo.ColorPrimarioHex, modelo.ColorSecundarioHex);
                    return modelo;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar la identidad visual del operador desde el servidor.");
        }

        return null;
    }

    private static string NormalizarHex(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;

        var limpio = hex.Trim();
        if (!limpio.StartsWith('#'))
            limpio = "#" + limpio;

        if (limpio.Length != 7 && limpio.Length != 9)
            return fallback;

        return limpio.ToUpperInvariant();
    }

    private static Color ParsearColor(string hex, Color fallback)
    {
        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static Color AjustarBrillo(Color c, float factor)
    {
        float r = Math.Clamp(c.Red * (1f + factor), 0f, 1f);
        float g = Math.Clamp(c.Green * (1f + factor), 0f, 1f);
        float b = Math.Clamp(c.Blue * (1f + factor), 0f, 1f);
        return new Color(r, g, b, c.Alpha);
    }

    private static Color MezclarConBlanco(Color c, float opacidadColor)
    {
        float t = Math.Clamp(opacidadColor, 0f, 1f);
        float r = 1f - (1f - c.Red) * t;
        float g = 1f - (1f - c.Green) * t;
        float b = 1f - (1f - c.Blue) * t;
        return new Color(r, g, b, 1f);
    }

    private static Color GarantizarContrasteSecundario(Color primario, Color secundario)
    {
        double dr = primario.Red - secundario.Red;
        double dg = primario.Green - secundario.Green;
        double db = primario.Blue - secundario.Blue;
        double distancia = Math.Sqrt(dr * dr + dg * dg + db * db);

        if (distancia < 0.28)
        {
            RgbToHsl(primario.Red, primario.Green, primario.Blue, out double h, out double s, out double l);

            double nuevoHue = (h + 150.0) % 360.0;
            double nuevaSaturacion = Math.Clamp(s > 0.4 ? s : 0.75, 0.45, 0.90);
            double nuevaLuminosidad = l > 0.5 ? 0.38 : 0.62;

            HslToRgb(nuevoHue, nuevaSaturacion, nuevaLuminosidad, out double r, out double g, out double b);
            return new Color((float)r, (float)g, (float)b, 1.0f);
        }

        return secundario;
    }

    private static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
        {
            h = 0;
            s = 0;
        }
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (Math.Abs(max - r) < 0.0001)
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (Math.Abs(max - g) < 0.0001)
                h = (b - r) / d + 2.0;
            else
                h = (r - g) / d + 4.0;

            h *= 60.0;
        }
    }

    private static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
    {
        if (Math.Abs(s) < 0.0001)
        {
            r = g = b = l;
            return;
        }

        double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
        double p = 2.0 * l - q;
        double hk = h / 360.0;

        r = HueToRgb(p, q, hk + 1.0 / 3.0);
        g = HueToRgb(p, q, hk);
        b = HueToRgb(p, q, hk - 1.0 / 3.0);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0.0) t += 1.0;
        if (t > 1.0) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    private class IdentidadVisualMobileResponseDto
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(GuidOrStringConverter))]
        public string? OperadorId { get; set; }
        public string? NombreComercial { get; set; }
        public string? ColorPrimarioHex { get; set; }
        public string? ColorSecundarioHex { get; set; }
        public string? ColorPrimario { get; set; }
        public string? ColorSecundario { get; set; }
        public string? LogoUrl { get; set; }
        public string? TelefonoContacto { get; set; }
        public string? EmailContacto { get; set; }
        public string? TipoPruebaEntregaExigida { get; set; }
        public int? MaxIntentosEntrega { get; set; }
    }

    private sealed class GuidOrStringConverter : System.Text.Json.Serialization.JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString();
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.ToString();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
