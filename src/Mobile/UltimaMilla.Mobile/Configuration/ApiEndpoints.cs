namespace UltimaMilla.Mobile.Configuration;

public static class ApiEndpoints
{
    // Auto-detect base URL based on platform running the app
    public static string BaseUrl
    {
        get
        {
#if ANDROID
            // Android Emulator maps host's localhost to 10.0.2.2
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
}
