using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UltimaMilla.PortalComercio.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// El cliente corre en el navegador: la URL de la API debe ser alcanzable desde el navegador
// del usuario (no desde la red interna de Docker). Se configura en wwwroot/appsettings.json.
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";

builder.Services.AddHttpClient<EnvioApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

await builder.Build().RunAsync();
