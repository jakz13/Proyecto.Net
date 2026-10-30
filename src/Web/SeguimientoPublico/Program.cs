using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Infrastructure;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.SeguimientoPublico;
using UltimaMilla.SeguimientoPublico.Servicios;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lectura de configuraciones desde wwwroot/appsettings.json
var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", false);

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(urlBaseApi),
    Timeout = TimeSpan.FromSeconds(15)
});

if (usarMocks)
{
    // Registra las implementaciones simuladas para pruebas locales y demo oral
    builder.Services.AgregarMocksFrontend();
}
else
{
    // Registra los clientes HTTP reales para consultar directamente el backend (UC-33 e identidad visual)
    builder.Services.AgregarHttpClientsFrontend(urlBaseApi);
}

// Seguimiento en vivo: el destinatario ve el cambio de estado sin recargar (no requiere sesión; sin conexión la página sigue igual)
builder.Services.AddScoped(sp => new TiempoRealSeguimientoServicio(
    sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
    urlBaseApi));

await builder.Build().RunAsync();
