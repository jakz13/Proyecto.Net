using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Frontend.Infrastructure;
using UltimaMilla.PortalComercio.Client.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Configuración de URL y modo mock desde /appsettings.json (servido dinámicamente por el host)
var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", false);

// Soporte de autorización en Blazor WebAssembly
builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<ITokenStore, TokenStoreEnMemoria>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());

// DelegatingHandler para JWT y CorrelationId
builder.Services.AddTransient<JwtAuthorizationHandler>();

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

await builder.Build().RunAsync();
