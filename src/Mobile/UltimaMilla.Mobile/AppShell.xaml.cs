using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Views.Entrega;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthService? _authService;
    private bool _sesionInicialVerificada;

    public AppShell() : this(null)
    {
    }

    public AppShell(IAuthService? authService)
    {
        InitializeComponent();
        _authService = authService;

        // Registro de rutas secundarias para navegación jerárquica (GoToAsync)
        Routing.RegisterRoute(nameof(DetalleParadaPage), typeof(DetalleParadaPage));
        Routing.RegisterRoute(nameof(RegistroEntregaPage), typeof(RegistroEntregaPage));
        Routing.RegisterRoute(nameof(RegistroRetiroPage), typeof(RegistroRetiroPage));
        Routing.RegisterRoute(nameof(IntentoFallidoPage), typeof(IntentoFallidoPage));
    }

    public async Task VerificarSesionInicialAsync()
    {
        if (_sesionInicialVerificada || _authService is null)
            return;

        _sesionInicialVerificada = true;

        try
        {
            var autenticado = await _authService.EstaAutenticadoAsync();
            if (autenticado)
            {
                await GoToAsync("//RutaDiaPage");
            }
            else if (CurrentState?.Location?.ToString() != "//LoginPage")
            {
                await GoToAsync("//LoginPage");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Error al verificar sesión inicial: {ex.Message}");
            try
            {
                if (CurrentState?.Location?.ToString() != "//LoginPage")
                {
                    await GoToAsync("//LoginPage");
                }
            }
            catch
            {
                // Fallback silencioso si ya se encuentra en LoginPage por defecto
            }
        }
    }
}
