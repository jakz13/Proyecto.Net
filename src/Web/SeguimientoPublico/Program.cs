using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.Frontend.Infrastructure;
using UltimaMilla.SeguimientoPublico;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lectura de configuraciones desde wwwroot/appsettings.json
var urlBaseApi = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
var usarMocks = builder.Configuration.GetValue<bool>("UsarMocks", true);

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
    // TODO: Registrar implementaciones HTTP reales cuando el backend exponga los endpoints públicos
}

await builder.Build().RunAsync();
