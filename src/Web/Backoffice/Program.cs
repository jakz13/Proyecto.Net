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
        opciones.ExpireTimeSpan = TimeSpan.FromHours(8);
        opciones.SlidingExpiration = true;
    });

// Políticas de autorización por perfil (Administrador, Despachador, Operario)
builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy("SoloAdministradores", politica => politica.RequireRole("Administrador"));
    opciones.AddPolicy("Operaciones", politica => politica.RequireRole("Administrador", "Despachador"));
    opciones.AddPolicy("Deposito", politica => politica.RequireRole("Administrador", "Despachador", "Operario"));
});

// Configuración de convenciones de Razor Pages
builder.Services.AddRazorPages(opciones =>
{
    opciones.Conventions.AuthorizeFolder("/Comercios", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Usuarios", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Zonas", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Tarifas", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/ReglasOperativas", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/MotivosNoEntrega", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/IdentidadVisual", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Flota", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Mensajeria", "SoloAdministradores");
    opciones.Conventions.AuthorizeFolder("/Deposito", "Deposito");
    opciones.Conventions.AuthorizeFolder("/Retiros", "Deposito");
    opciones.Conventions.AllowAnonymousToPage("/Login");
    opciones.Conventions.AllowAnonymousToPage("/AccesoDenegado");
});

// Fábrica de clientes HTTP (requerida por LoginModel y clientes de integración)
builder.Services.AddHttpClient();

var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", true);

// Detrás del proxy inverso (Caddy) el esquema y la IP reales llegan en cabeceras X-Forwarded-*.
// Solo Caddy alcanza este contenedor, por eso se aceptan sin lista de proxies conocidos.
builder.Services.Configure<ForwardedHeadersOptions>(opciones =>
{
    opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opciones.KnownNetworks.Clear();
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

// Con el modo demo apagado, las llamadas a la API llevan el JWT de la sesión del usuario
if (!LoginModel.ModoDemoActivo(builder.Configuration))
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddTransient<TokenApiHandler>();
    builder.Services.ConfigureHttpClientDefaults(cliente => cliente.AddHttpMessageHandler<TokenApiHandler>());
}

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
