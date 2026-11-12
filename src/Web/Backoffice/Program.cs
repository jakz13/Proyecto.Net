using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using UltimaMilla.Backoffice.Pages;
using UltimaMilla.Backoffice.Seguridad;
using UltimaMilla.Frontend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configuración de autenticación mediante Cookies compartibles (Stateless respecto a memoria de proceso)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.LoginPath = "/Login";
        opciones.AccessDeniedPath = "/AccesoDenegado";
        opciones.Cookie.Name = "UltimaMilla.Backoffice.Auth";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        // En el servidor la cookie solo viaja por HTTPS; en localhost (HTTP) se mantiene la política del pedido
        opciones.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
    });

// Políticas de autorización por perfil. Reflejan la letra (sección 3): el administrador configura la operación; el despachador planifica
// y monitorea; el operario trabaja en el depósito; despacho es lo que pasa por las manos del despachador y del operario.
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy("SoloAdministradores", politica => politica.RequireRole("Administrador"));
    opciones.AddPolicy("Operaciones", politica => politica.RequireRole("Despachador"));
    opciones.AddPolicy("Deposito", politica => politica.RequireRole("Operario"));
    opciones.AddPolicy("Despacho", politica => politica.RequireRole("Despachador", "Operario"));
    opciones.AddPolicy("Supervision", politica => politica.RequireRole("Administrador", "Despachador"));
});

// Configuración de convenciones de Razor Pages: cada carpeta declara quién puede abrirla (las páginas repiten el perfil con [Authorize])
builder.Services.AddRazorPages(opciones =>
{
    foreach (var carpeta in new[] { "Comercios", "Usuarios", "Zonas", "Tarifas", "ReglasOperativas", "MotivosNoEntrega", "IdentidadVisual", "Flota", "Mensajeria", "Observabilidad" })
        opciones.Conventions.AuthorizeFolder("/" + carpeta, "SoloAdministradores");
    foreach (var carpeta in new[] { "Tablero", "Rutas", "Envios", "Incidencias" })
        opciones.Conventions.AuthorizeFolder("/" + carpeta, "Operaciones");
    opciones.Conventions.AuthorizeFolder("/Reportes", "Supervision");
    opciones.Conventions.AuthorizeFolder("/Deposito", "Deposito");
    foreach (var carpeta in new[] { "Despacho", "Retiros" })
        opciones.Conventions.AuthorizeFolder("/" + carpeta, "Despacho");
    opciones.Conventions.AllowAnonymousToPage("/Login");
    opciones.Conventions.AllowAnonymousToPage("/AccesoDenegado");
    opciones.Conventions.AllowAnonymousToFolder("/Seguimiento");
});

// Fábrica de clientes HTTP (requerida por LoginModel y clientes de integración)
builder.Services.AddHttpClient();

// Marca del operador de la sesión (nombre, logo y colores), pedida a la API y guardada brevemente
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UltimaMilla.Backoffice.Seguridad.IdentidadDelOperadorEnSesion>();

var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", true);

// Detrás del proxy inverso (Caddy) el esquema y la IP reales llegan en cabeceras X-Forwarded-*.
// Solo Caddy alcanza este contenedor, por eso se aceptan sin lista de proxies conocidos.
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownIPNetworks.Clear();
    opciones.KnownProxies.Clear();
});

// Claves de las cookies de sesión en un volumen: así no se invalidan las sesiones al reiniciar el contenedor
var rutaClaves = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrWhiteSpace(rutaClaves))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("UltimaMilla.Backoffice")
        .PersistKeysToFileSystem(new DirectoryInfo(rutaClaves));
}

// Registro del manejador de tokens para llamadas salientes a la API
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TokenApiHandler>();
builder.Services.ConfigureHttpClientDefaults(cliente => cliente.AddHttpMessageHandler<TokenApiHandler>());

if (usarMocks)
{
    // Registra implementaciones simuladas en memoria para demo y desarrollo local
    builder.Services.AgregarMocksFrontend();
}
else
{
    // Registra clientes HTTP para conexión directa con los endpoints de la API backend
    builder.Services.AgregarHttpClientsFrontend(urlBaseApi);
}

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
