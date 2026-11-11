using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Frontend.Infrastructure;
using UltimaMilla.PortalComercio.Client.Auth;
using UltimaMilla.PortalComercio.Client.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configuración de URL y modo mock desde /appsettings.json (servido dinámicamente por el host)
var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", false);
// Modo demo (solo desarrollo local): token precargado, sin pedir usuario ni contraseña. El host manda este valor; si no llega definido,
// solo se permite en Development. En el servidor el acceso pasa siempre por el login real.
var modoDemo = builder.Configuration.GetValue<bool?>("Auth:PermitirModoDemo") ?? builder.HostEnvironment.IsDevelopment();

// Soporte de autorización en Blazor WebAssembly
builder.Services.AddAuthorizationCore();
if (modoDemo)
    builder.Services.AddSingleton<ITokenStore, TokenStoreEnMemoria>();
else
    builder.Services.AddSingleton<ITokenStore, TokenStoreSesion>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

// DelegatingHandler para JWT y CorrelationId
builder.Services.AddTransient<JwtAuthorizationHandler>();
// Con login real, todos los clientes HTTP del portal deben enviar el JWT de la sesión
if (!modoDemo)
    builder.Services.ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<JwtAuthorizationHandler>());

builder.Services.AddHttpClient("UltimaMillaApi", cliente =>
{
    cliente.BaseAddress = new Uri(urlBaseApi);
    cliente.Timeout = TimeSpan.FromSeconds(15);
}).AddHttpMessageHandler<JwtAuthorizationHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("UltimaMillaApi"));

if (usarMocks)
{
    // Registra implementaciones MOCK en memoria para demo y desarrollo local
    builder.Services.AgregarMocksFrontend();
}
else
{
    // Registra clientes HTTP reales conectados al backend
    builder.Services.AgregarHttpClientsFrontend(urlBaseApi);
}

// Estado de los envíos en vivo: una conexión por sesión, compartida por las páginas (en modo mock no hay hub)
builder.Services.AddScoped(sp => new TiempoRealEnviosServicio(
    sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
    sp.GetRequiredService<ITokenStore>(),
    urlBaseApi));

await builder.Build().RunAsync();
