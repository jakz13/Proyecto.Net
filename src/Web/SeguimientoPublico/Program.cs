using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Infrastructure;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.SeguimientoPublico;

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
    // Registra la implementación HTTP real para consultar directamente el backend (UC-33)
    builder.Services.AddScoped<ISeguimientoApi, HttpSeguimientoApi>();
}

await builder.Build().RunAsync();
