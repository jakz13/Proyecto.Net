using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UltimaMilla.Mobile.Services.Updates;

public record VersionManifiestoDto(
    int VersionCode,
    string VersionName,
    string ApkUrl,
    string? Sha256 = null,
    long? Size = null,
    string? Notas = null
);

public record ResultadoVerificacionActualizacion(
    bool HayActualizacionDisponible,
    int VersionCodeInstalada,
    string VersionInstaladaTexto,
    int VersionCodeRemota,
    string VersionRemotaTexto,
    string ApkUrl,
    string? Sha256Esperado,
    long? TamanoBytes,
    string Notas,
    string? MensajeEstado = null
);

public interface IAppUpdateService
{
    bool HayActualizacionDisponible { get; }
    bool ActualizacionPospuestaEnSesion { get; set; }
    string? UltimoErrorDescarga { get; }
    ResultadoVerificacionActualizacion? UltimaVerificacion { get; }

    Task<ResultadoVerificacionActualizacion> VerificarActualizacionAsync(CancellationToken cancellationToken = default);
    Task<bool> DescargarEInstalarAsync(IProgress<double> progreso, CancellationToken cancellationToken = default);
}

public class AppUpdateService : IAppUpdateService
{
    public const string ManifiestoRemotoUrl = "https://shippingbox.duckdns.org/descargas/version.json";
    public const string ApkRemotoPorDefectoUrl = "https://shippingbox.duckdns.org/descargas/UltimaMilla-Release-Signed.apk";
    private const string NombreArchivoApkTemporal = "UltimaMilla-Release-Signed.apk";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<AppUpdateService> _logger;
    private string? _apkUrlPendiente;
    private string? _sha256Pendiente;
    private long? _tamanoBytesPendiente;

    public bool HayActualizacionDisponible { get; private set; }
    public bool ActualizacionPospuestaEnSesion { get; set; }
    public string? UltimoErrorDescarga { get; private set; }
    public ResultadoVerificacionActualizacion? UltimaVerificacion { get; private set; }

    public AppUpdateService(ILogger<AppUpdateService> logger)
    {
        _logger = logger;
    }

    public async Task<ResultadoVerificacionActualizacion> VerificarActualizacionAsync(CancellationToken cancellationToken = default)
    {
        int versionCodeInstalada = ObtenerVersionCodeInstalada();
        string versionInstaladaTexto = ObtenerVersionNameInstalada();

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return new ResultadoVerificacionActualizacion(
                HayActualizacionDisponible: false,
                VersionCodeInstalada: versionCodeInstalada,
                VersionInstaladaTexto: versionInstaladaTexto,
                VersionCodeRemota: versionCodeInstalada,
                VersionRemotaTexto: versionInstaladaTexto,
                ApkUrl: ApkRemotoPorDefectoUrl,
                Sha256Esperado: null,
                TamanoBytes: null,
                Notas: string.Empty,
                MensajeEstado: "Sin conexión a internet para consultar actualizaciones.");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var separador = ManifiestoRemotoUrl.Contains('?') ? "&" : "?";
            var urlConBust = $"{ManifiestoRemotoUrl}{separador}t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            using var response = await httpClient.GetAsync(urlConBust, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return new ResultadoVerificacionActualizacion(
                    HayActualizacionDisponible: false,
                    VersionCodeInstalada: versionCodeInstalada,
                    VersionInstaladaTexto: versionInstaladaTexto,
                    VersionCodeRemota: versionCodeInstalada,
                    VersionRemotaTexto: versionInstaladaTexto,
                    ApkUrl: ApkRemotoPorDefectoUrl,
                    Sha256Esperado: null,
                    TamanoBytes: null,
                    Notas: string.Empty,
                    MensajeEstado: "La aplicación se encuentra actualizada en su última versión disponible.");
            }

            var manifiesto = await response.Content.ReadFromJsonAsync<VersionManifiestoDto>(JsonOptions, cts.Token);
            if (manifiesto is null || manifiesto.VersionCode <= 0)
            {
                return new ResultadoVerificacionActualizacion(
                    HayActualizacionDisponible: false,
                    VersionCodeInstalada: versionCodeInstalada,
                    VersionInstaladaTexto: versionInstaladaTexto,
                    VersionCodeRemota: versionCodeInstalada,
                    VersionRemotaTexto: versionInstaladaTexto,
                    ApkUrl: ApkRemotoPorDefectoUrl,
                    Sha256Esperado: null,
                    TamanoBytes: null,
                    Notas: string.Empty,
                    MensajeEstado: "Manifiesto de versión no disponible.");
            }

            bool disponible = manifiesto.VersionCode > versionCodeInstalada;
            string apkUrl = !string.IsNullOrWhiteSpace(manifiesto.ApkUrl)
                ? manifiesto.ApkUrl.Trim()
                : ApkRemotoPorDefectoUrl;
            string versionRemotaTexto = !string.IsNullOrWhiteSpace(manifiesto.VersionName)
                ? manifiesto.VersionName.Trim()
                : $"1.0.{manifiesto.VersionCode}";
            string notas = !string.IsNullOrWhiteSpace(manifiesto.Notas)
                ? manifiesto.Notas.Trim()
                : "Mejoras operativas de estabilidad, sincronización en segundo plano y seguridad de sesión.";

            HayActualizacionDisponible = disponible;
            _apkUrlPendiente = apkUrl;
            _sha256Pendiente = !string.IsNullOrWhiteSpace(manifiesto.Sha256) ? manifiesto.Sha256.Trim() : null;
            _tamanoBytesPendiente = manifiesto.Size is > 0 ? manifiesto.Size : null;

            UltimaVerificacion = new ResultadoVerificacionActualizacion(
                HayActualizacionDisponible: disponible,
                VersionCodeInstalada: versionCodeInstalada,
                VersionInstaladaTexto: versionInstaladaTexto,
                VersionCodeRemota: manifiesto.VersionCode,
                VersionRemotaTexto: versionRemotaTexto,
                ApkUrl: apkUrl,
                Sha256Esperado: _sha256Pendiente,
                TamanoBytes: _tamanoBytesPendiente,
                Notas: notas,
                MensajeEstado: disponible
                    ? $"Nueva versión {versionRemotaTexto} disponible."
                    : $"Ya cuenta con la versión más reciente instalada ({versionInstaladaTexto}).");

            return UltimaVerificacion;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Consulta silenciosa de actualización OTA omitida por timeout o falta de red.");
            return new ResultadoVerificacionActualizacion(
                HayActualizacionDisponible: false,
                VersionCodeInstalada: versionCodeInstalada,
                VersionInstaladaTexto: versionInstaladaTexto,
                VersionCodeRemota: versionCodeInstalada,
                VersionRemotaTexto: versionInstaladaTexto,
                ApkUrl: ApkRemotoPorDefectoUrl,
                Sha256Esperado: null,
                TamanoBytes: null,
                Notas: string.Empty,
                MensajeEstado: "No fue posible conectar con el servidor de actualizaciones.");
        }
    }

    public async Task<bool> DescargarEInstalarAsync(IProgress<double> progreso, CancellationToken cancellationToken = default)
    {
        UltimoErrorDescarga = null;

        var urlDescarga = !string.IsNullOrWhiteSpace(_apkUrlPendiente)
            ? _apkUrlPendiente
            : (UltimaVerificacion?.ApkUrl ?? ApkRemotoPorDefectoUrl);

        var sha256Esperado = !string.IsNullOrWhiteSpace(_sha256Pendiente)
            ? _sha256Pendiente
            : UltimaVerificacion?.Sha256Esperado;

        var tamanoManifiesto = _tamanoBytesPendiente ?? UltimaVerificacion?.TamanoBytes;
        var rutaDestino = Path.Combine(FileSystem.CacheDirectory, NombreArchivoApkTemporal);

        try
        {
            progreso?.Report(0.0);
            EliminarArchivoTemporal(rutaDestino);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            using var response = await httpClient.GetAsync(
                urlDescarga,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? tamanoManifiesto ?? -1L;

            await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var fileStream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long totalLeido = 0;
                int bytesLeidos;

                while ((bytesLeidos = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesLeidos), cancellationToken);
                    totalLeido += bytesLeidos;

                    if (totalBytes > 0)
                    {
                        double avance = Math.Clamp((double)totalLeido / totalBytes, 0.0, 1.0);
                        progreso?.Report(avance);
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
            }

            // Verificación criptográfica de integridad SHA-256 antes de invocar al instalador del sistema operativo
            using (var sha256 = SHA256.Create())
            await using (var verifyStream = new FileStream(rutaDestino, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                var hashBytes = await sha256.ComputeHashAsync(verifyStream, cancellationToken);
                var hashCalculado = Convert.ToHexString(hashBytes);

                if (!string.IsNullOrWhiteSpace(sha256Esperado) && !string.Equals(sha256Esperado.Trim(), "...", StringComparison.Ordinal))
                {
                    var hashNormalizado = sha256Esperado.Replace("-", string.Empty).Replace(" ", string.Empty).Trim();
                    if (!string.Equals(hashCalculado, hashNormalizado, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError(
                            "Verificación SHA-256 fallida para el APK descargado. Esperado: {Esperado}, Calculado: {Calculado}.",
                            hashNormalizado,
                            hashCalculado);

                        UltimoErrorDescarga = "La verificación de integridad SHA-256 del paquete APK falló. El archivo fue descartado por seguridad.";
                        EliminarArchivoTemporal(rutaDestino);
                        return false;
                    }
                }
            }

            progreso?.Report(1.0);

#if ANDROID
            var iniciado = await MainThread.InvokeOnMainThreadAsync(() => LanzarInstaladorNativoAndroid(rutaDestino));
            if (!iniciado)
            {
                UltimoErrorDescarga = "No fue posible abrir el instalador nativo de paquetes de Android.";
            }
            return iniciado;
#else
            await Task.CompletedTask;
            return true;
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al descargar o iniciar la instalación del paquete APK desde {Url}.", urlDescarga);
            UltimoErrorDescarga = "No fue posible descargar el paquete de actualización. Verifique su conexión e intente nuevamente.";
            EliminarArchivoTemporal(rutaDestino);
            return false;
        }
    }

    private static void EliminarArchivoTemporal(string rutaArchivo)
    {
        try
        {
            if (File.Exists(rutaArchivo))
            {
                File.Delete(rutaArchivo);
            }
        }
        catch
        {
            // Ignorar error de limpieza en caché temporal
        }
    }

#if ANDROID
    private bool LanzarInstaladorNativoAndroid(string rutaApkLocal)
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var archivoApk = new global::Java.IO.File(rutaApkLocal);
            if (!archivoApk.Exists())
            {
                return false;
            }

            var autoridad = $"{context.PackageName}.fileprovider";
            var apkUri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, autoridad, archivoApk);

            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission | global::Android.Content.ActivityFlags.NewTask);

            var activity = Platform.CurrentActivity;
            if (activity is not null)
            {
                activity.StartActivity(intent);
            }
            else
            {
                context.StartActivity(intent);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No fue posible invocar el instalador nativo de paquetes de Android.");
            return false;
        }
    }
#endif

    private static int ObtenerVersionCodeInstalada()
    {
        try
        {
            return int.Parse(AppInfo.Current.BuildString);
        }
        catch
        {
            return int.TryParse(AppInfo.Current.BuildString, out var parsed) && parsed > 0 ? parsed : 1;
        }
    }

    private static string ObtenerVersionNameInstalada()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(AppInfo.Current.VersionString)
                ? AppInfo.Current.VersionString
                : "1.0.2";
        }
        catch
        {
            return "1.0.2";
        }
    }
}
