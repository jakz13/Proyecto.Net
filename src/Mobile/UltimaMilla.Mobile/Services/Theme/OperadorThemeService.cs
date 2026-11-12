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
            var colorPrimario = Preferences.Default.Get(PrefKeyColorPrimario, "#2563EB");
            var colorSecundario = Preferences.Default.Get(PrefKeyColorSecundario, "#64748B");
            var logoUrl = Preferences.Default.Get(PrefKeyLogoUrl, string.Empty);
            var telefono = Preferences.Default.Get(PrefKeyTelefono, string.Empty);
            var email = Preferences.Default.Get(PrefKeyEmail, string.Empty);
            var operadorIdStr = Preferences.Default.Get(PrefKeyOperadorId, string.Empty);

            Guid? opId = Guid.TryParse(operadorIdStr, out var id) ? id : null;

            var modelo = new OperadorIdentidadVisualModel
            {
                OperadorId = opId,
                NombreComercial = string.IsNullOrWhiteSpace(nombre) ? "Última Milla" : nombre,
                ColorPrimarioHex = NormalizarHex(colorPrimario, "#2563EB"),
                ColorSecundarioHex = NormalizarHex(colorSecundario, "#64748B"),
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
        identidad.ColorPrimarioHex = NormalizarHex(identidad.ColorPrimarioHex, "#2563EB");
        identidad.ColorSecundarioHex = NormalizarHex(identidad.ColorSecundarioHex, "#64748B");

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

        // Actualizar recursos de Application en el hilo de UI
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var primario = ParsearColor(identidad.ColorPrimarioHex, Color.FromArgb("#2563EB"));
                var secundario = ParsearColor(identidad.ColorSecundarioHex, Color.FromArgb("#64748B"));

                var primarioOscuro = AjustarBrillo(primario, -0.15f);
                var primarioClaro = primario.WithAlpha(0.12f);
                var secundarioOscuro = AjustarBrillo(secundario, -0.15f);
                var secundarioClaro = secundario.WithAlpha(0.12f);

                var resources = Application.Current?.Resources;
                if (resources != null)
                {
                    resources["Primary"] = primario;
                    resources["PrimaryColor"] = primario;
                    resources["PrimaryDark"] = primarioOscuro;
                    resources["PrimaryLight"] = primarioClaro;
                    resources["PrimaryBrush"] = new SolidColorBrush(primario);
                    resources["CorporatePrimaryBrush"] = new SolidColorBrush(primario);

                    resources["Secondary"] = secundario;
                    resources["SecondaryColor"] = secundario;
                    resources["SecondaryDark"] = secundarioOscuro;
                    resources["SecondaryLight"] = secundarioClaro;
                    resources["SecondaryBrush"] = new SolidColorBrush(secundario);

                    resources["NombreOperadorActual"] = identidad.NombreComercial;
                    resources["LogoOperadorUrl"] = identidad.LogoUrl ?? string.Empty;
                }

                if (Shell.Current != null)
                {
                    Shell.SetTabBarBackgroundColor(Shell.Current, Color.FromArgb("#0F172A"));
                    Shell.SetTabBarTitleColor(Shell.Current, primario);
                    Shell.SetTabBarForegroundColor(Shell.Current, primario);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inyectar recursos dinámicos de identidad visual en Application.Current.Resources.");
            }

            IdentidadVisualCambiada?.Invoke(this, identidad);
        });
    }

    public async Task<OperadorIdentidadVisualModel?> DescargarYActualizarIdentidadAsync(CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("BackendApi");
            client.BaseAddress = new Uri(ApiEndpoints.BaseUrl);

            var response = await client.GetAsync("/api/mobile/v1/operador/identidad-visual", ct);
            if (!response.IsSuccessStatusCode)
            {
                // Fallback secundario si se consulta endpoint general
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
                    var colorPri = !string.IsNullOrWhiteSpace(dto.ColorPrimarioHex) ? dto.ColorPrimarioHex : (!string.IsNullOrWhiteSpace(dto.ColorPrimario) ? dto.ColorPrimario : "#2563EB");
                    var colorSec = !string.IsNullOrWhiteSpace(dto.ColorSecundarioHex) ? dto.ColorSecundarioHex : (!string.IsNullOrWhiteSpace(dto.ColorSecundario) ? dto.ColorSecundario : "#64748B");

                    var modelo = new OperadorIdentidadVisualModel
                    {
                        OperadorId = dto.OperadorId,
                        NombreComercial = nombre.Trim(),
                        ColorPrimarioHex = NormalizarHex(colorPri, "#2563EB"),
                        ColorSecundarioHex = NormalizarHex(colorSec, "#64748B"),
                        LogoUrl = string.IsNullOrWhiteSpace(dto.LogoUrl) ? null : dto.LogoUrl.Trim(),
                        TelefonoContacto = string.IsNullOrWhiteSpace(dto.TelefonoContacto) ? null : dto.TelefonoContacto.Trim(),
                        EmailContacto = string.IsNullOrWhiteSpace(dto.EmailContacto) ? null : dto.EmailContacto.Trim()
                    };

                    AplicarIdentidad(modelo);
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

    private class IdentidadVisualMobileResponseDto
    {
        public Guid? OperadorId { get; set; }
        public string? NombreComercial { get; set; }
        public string? ColorPrimarioHex { get; set; }
        public string? ColorSecundarioHex { get; set; }
        public string? ColorPrimario { get; set; }
        public string? ColorSecundario { get; set; }
        public string? LogoUrl { get; set; }
        public string? TelefonoContacto { get; set; }
        public string? EmailContacto { get; set; }
    }
}

