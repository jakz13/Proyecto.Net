using UltimaMilla.PortalComercio.Components;

var builder = WebApplication.CreateBuilder(args);

// ADR 02: el Portal usa Blazor WebAssembly. Este proyecto solo hospeda y sirve el cliente
// (UltimaMilla.PortalComercio.Client); la lógica de la interfaz corre en el navegador
// y consume la API directamente.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(UltimaMilla.PortalComercio.Client.Routes).Assembly);

app.Run();
