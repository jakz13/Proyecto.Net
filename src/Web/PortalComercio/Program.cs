using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using UltimaMilla.PortalComercio.Components;

var builder = WebApplication.CreateBuilder(args);

// ADR 02: el Portal usa Blazor WebAssembly. Este proyecto solo hospeda y sirve el cliente
// (UltimaMilla.PortalComercio.Client); la lógica de la interfaz corre en el navegador
// y consume la API directamente.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Claves de antiforgery y cookies en un volumen: sin esto cada despliegue (contenedor nuevo) deja inválidos los formularios y las
// sesiones abiertas del navegador hasta que se limpian las cookies, y el portal "no anda" tras cada actualización.
var rutaClaves = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrWhiteSpace(rutaClaves))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("UltimaMilla.PortalComercio")
        .PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

// Caché del manifiesto de recursos de Blazor. resource-collection.<huella>.js sale con "immutable" por un año, pero su huella NO cambia
// cuando cambia su contenido (cada despliegue trae otros hashes de ensamblados bajo el mismo nombre). El navegador se quedaba con la copia
// vieja, su integridad ya no coincidía con el importmap y el portal quedaba en "Cargando Portal del Comercio…" (solo se arreglaba con
// una ventana de incógnito). Ahora se revalida siempre (ETag => 304, casi sin costo).
// Además, los navegadores que ya guardaron la copia vieja la purgan una única vez: Clear-Site-Data "cache" + cookie de marca.
app.Use(async (contexto, siguiente) =>
{
    var ruta = contexto.Request.Path.Value ?? string.Empty;
    var esManifiesto = ruta.StartsWith("/_framework/resource-collection", StringComparison.OrdinalIgnoreCase);
    var esDocumento = HttpMethods.IsGet(contexto.Request.Method)
        && !ruta.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase)
        && contexto.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
        && !contexto.Request.Cookies.ContainsKey(CookiePurgaCache);

    if (esManifiesto || esDocumento)
    {
        contexto.Response.OnStarting(() =>
        {
            if (esManifiesto)
                contexto.Response.Headers.CacheControl = "no-cache";
            if (esDocumento && contexto.Response.StatusCode < 400)
            {
                contexto.Response.Headers["Clear-Site-Data"] = "\"cache\"";
                contexto.Response.Cookies.Append(CookiePurgaCache, "1", new CookieOptions
                {
                    Secure = true,
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromDays(365)
                });
            }
            return Task.CompletedTask;
        });
    }

    await siguiente();
});

// Middleware para sincronizar configuración (Mocks vs API real) hacia el cliente WASM
// y evitar bloqueos de caché o desincronización de hashes SRI en desarrollo.
app.Use(async (contexto, siguiente) =>
{
    var ruta = contexto.Request.Path.Value ?? string.Empty;

    // 1. Sirve appsettings.json dinámicamente al cliente WASM reflejando la configuración actual del host
    // (permite usar --UsarMocks=true desde el script o UsarMocks=false por defecto para el equipo).
    if (ruta.Equals("/appsettings.json", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/appsettings.Development.json", StringComparison.OrdinalIgnoreCase) ||
        ruta.Equals("/appsettings.Production.json", StringComparison.OrdinalIgnoreCase) ||
        (ruta.StartsWith("/appsettings.", StringComparison.OrdinalIgnoreCase) && ruta.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
    {
        contexto.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        contexto.Response.Headers.Pragma = "no-cache";
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var usarMocks = app.Configuration.GetValue<bool>("UsarMocks", false);
        var urlBaseApi = app.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";

        var jsonConfiguracion = JsonSerializer.Serialize(new
        {
            ApiSettings = new { BaseUrl = urlBaseApi },
            Auth = new { PermitirModoDemo = app.Configuration.GetValue<bool?>("Auth:PermitirModoDemo") ?? app.Environment.IsDevelopment() },
            UsarMocks = usarMocks
        });

        await contexto.Response.WriteAsync(jsonConfiguracion);
        return;
    }

    // 2. En desarrollo, sirve los activos WASM de /_framework/ directamente desde la carpeta compilada del cliente
    // sin caché y sin chequeos SRI que fallen cuando se recompilan proyectos compartidos.
    if (app.Environment.IsDevelopment() && ruta.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase))
    {
        contexto.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        contexto.Response.Headers.Pragma = "no-cache";

        if (ruta.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
        {
            contexto.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        var nombreArchivo = Path.GetFileName(ruta);
        if (!string.IsNullOrEmpty(nombreArchivo) && !nombreArchivo.Contains("..", StringComparison.Ordinal))
        {
            var directorioFrameworkCliente = Path.GetFullPath(
                Path.Combine(app.Environment.ContentRootPath, "..", "PortalComercio.Client", "bin", "Debug", "net10.0", "wwwroot", "_framework"));

            if (Directory.Exists(directorioFrameworkCliente))
            {
                // Si se solicita dotnet.js, desactiva la validación SRI y el force-cache en el manifiesto embebido
                var esDotnetBootJs = nombreArchivo.Equals("dotnet.js", StringComparison.OrdinalIgnoreCase) ||
                    (nombreArchivo.StartsWith("dotnet.", StringComparison.OrdinalIgnoreCase) &&
                     nombreArchivo.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
                     !nombreArchivo.StartsWith("dotnet.native.", StringComparison.OrdinalIgnoreCase) &&
                     !nombreArchivo.StartsWith("dotnet.runtime.", StringComparison.OrdinalIgnoreCase));

                if (esDotnetBootJs)
                {
                    var rutaDotnetJs = Path.Combine(directorioFrameworkCliente, "dotnet.js");
                    if (File.Exists(rutaDotnetJs))
                    {
                        var contenidoJs = await File.ReadAllTextAsync(rutaDotnetJs);
                        contenidoJs = contenidoJs
                            .Replace("/*json-start*/{", "/*json-start*/{\"disableIntegrityCheck\":true,", StringComparison.Ordinal)
                            .Replace("\"cache\": \"force-cache\"", "\"cache\": \"no-cache\"", StringComparison.Ordinal);

                        contexto.Response.ContentType = "text/javascript; charset=utf-8";
                        await contexto.Response.WriteAsync(contenidoJs);
                        return;
                    }
                }

                // Busca el archivo exacto o resuelve por nombre base si el hash intermedio cambió tras recompilar
                var rutaArchivo = Path.Combine(directorioFrameworkCliente, nombreArchivo);
                if (!File.Exists(rutaArchivo))
                {
                    var partesNombre = nombreArchivo.Split('.');
                    if (partesNombre.Length >= 3)
                    {
                        var extension = partesNombre[^1];
                        var nombreSinHash = string.Join('.', partesNombre[..^2]);
                        var patronBusqueda = $"{nombreSinHash}.*.{extension}";
                        var coincidencias = Directory.GetFiles(directorioFrameworkCliente, patronBusqueda);

                        if (coincidencias.Length > 0)
                        {
                            rutaArchivo = coincidencias
                                .OrderByDescending(File.GetLastWriteTimeUtc)
                                .First();
                        }
                        else
                        {
                            var rutaSinFingerprint = Path.Combine(directorioFrameworkCliente, $"{nombreSinHash}.{extension}");
                            if (File.Exists(rutaSinFingerprint))
                            {
                                rutaArchivo = rutaSinFingerprint;
                            }
                        }
                    }
                }

                if (File.Exists(rutaArchivo))
                {
                    var extensionArchivo = Path.GetExtension(rutaArchivo).ToLowerInvariant();
                    contexto.Response.ContentType = extensionArchivo switch
                    {
                        ".wasm" => "application/wasm",
                        ".js" or ".mjs" => "text/javascript; charset=utf-8",
                        ".json" => "application/json; charset=utf-8",
                        _ => "application/octet-stream"
                    };

                    await contexto.Response.SendFileAsync(rutaArchivo);
                    return;
                }
            }
        }
    }

    await siguiente();
});

app.UseStaticFiles();

var rutaUploadsBackoffice = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "Backoffice", "wwwroot", "uploads"));
if (Directory.Exists(rutaUploadsBackoffice))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(rutaUploadsBackoffice),
        RequestPath = "/uploads"
    });
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(UltimaMilla.PortalComercio.Client.Routes).Assembly);

app.Run();

/// <summary>Cookie que marca que este navegador ya purgó la caché vieja del portal (ver el middleware de caché).</summary>
public partial class Program
{
    internal const string CookiePurgaCache = "um_purga_cache_v1";
}
