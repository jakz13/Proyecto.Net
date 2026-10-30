namespace UltimaMilla.Mobile.Configuration;

public static class ApiEndpoints
{
    /// <summary>
    /// Permite sobreescribir manualmente la URL de la API si cambia la IP o red.
    /// </summary>
    public static string? CustomBaseUrl { get; set; }

    // Auto-detect base URL based on platform running the app
    public static string BaseUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomBaseUrl))
            {
                return CustomBaseUrl;
            }

#if ANDROID
            // Si es un celular físico conectado a la misma red Wi-Fi que la PC:
            if (DeviceInfo.DeviceType == DeviceType.Physical)
            {
                return "http://192.168.1.9:5000";
            }

            // Android Emulator mapea el localhost de la PC a 10.0.2.2
            return "http://10.0.2.2:5000";
#elif IOS || MACCATALYST
            return "http://localhost:5000";
#elif WINDOWS
            return "http://localhost:5000";
#else
            return "http://localhost:5000";
#endif
        }
    }

    public const string MobilePing = "/api/mobile/v1/ping";
    public const string EnvioList = "/api/envios";
    public const string OperacionHub = "/hubs/operacion";
}
