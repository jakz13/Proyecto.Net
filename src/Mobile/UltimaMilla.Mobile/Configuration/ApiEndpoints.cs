namespace UltimaMilla.Mobile.Configuration;

public static class ApiEndpoints
{
    public const string CloudUrl = "https://shippingbox.duckdns.org";
    public const string LocalWindowsUrl = "http://localhost:5000";
    public const string LocalAndroidEmulatorUrl = "http://10.0.2.2:5000";
    public const string LocalLanUrl = "http://192.168.1.9:5000";

    private const string ApiBaseUrlPrefKey = "api_base_url";

    /// <summary>
    /// Permite sobreescribir manualmente la URL de la API si cambia la IP o red.
    /// </summary>
    public static string? CustomBaseUrl { get; set; }

    public static string DefaultLocalUrl
    {
        get
        {
#if ANDROID
            if (DeviceInfo.DeviceType == DeviceType.Physical)
            {
                return LocalLanUrl;
            }
            return LocalAndroidEmulatorUrl;
#elif WINDOWS || IOS || MACCATALYST
            return LocalWindowsUrl;
#else
            return LocalWindowsUrl;
#endif
        }
    }

    public static bool EsServidorNube => BaseUrl.StartsWith(CloudUrl, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// URL base activa. Por defecto apunta al servidor en la nube con HTTPS y balanceador.
    /// </summary>
    public static string BaseUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomBaseUrl))
            {
                return CustomBaseUrl.TrimEnd('/');
            }

            var saved = Preferences.Default.Get<string?>(ApiBaseUrlPrefKey, null);
            if (!string.IsNullOrWhiteSpace(saved))
            {
                return saved.TrimEnd('/');
            }

            // Por defecto en la app móvil: servidor en la nube con balanceador y HTTPS
            return CloudUrl;
        }
    }

    public static void SetBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Preferences.Default.Remove(ApiBaseUrlPrefKey);
            CustomBaseUrl = null;
        }
        else
        {
            var cleanUrl = url.Trim().TrimEnd('/');
            Preferences.Default.Set(ApiBaseUrlPrefKey, cleanUrl);
            CustomBaseUrl = cleanUrl;
        }
    }

    public static void UsarServidorNube() => SetBaseUrl(CloudUrl);

    public static void UsarServidorLocal() => SetBaseUrl(DefaultLocalUrl);

    public const string MobilePing = "/api/mobile/v1/ping";
    public const string EnvioList = "/api/envios";
    public const string OperacionHub = "/hubs/operacion";
}
