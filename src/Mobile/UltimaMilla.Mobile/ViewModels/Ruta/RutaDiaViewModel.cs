using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Views.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

public class BultoItemViewModel
{
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }
    public bool EsFragil { get; init; }
    public string Icono => EsFragil ? "FRÁGIL" : "ESTÁNDAR";
}

public partial class ParadaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid HojaDeRutaId { get; set; } = Guid.Empty;
    public Guid EnvioId { get; set; } = Guid.Empty;
    public int Orden { get; init; }
    public string Direccion { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public int CantidadBultos { get; init; }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoBgColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextColor))]
    [NotifyPropertyChangedFor(nameof(EstadoBorderColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTextoBadge))]
    [NotifyPropertyChangedFor(nameof(PuedeRegistrar))]
    private string _estado = "Pendiente"; // "Pendiente", "Entregado", "Fallido"

    public string HoraEstimada { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string NotasComercio { get; init; } = string.Empty;
    public double Latitud { get; init; }
    public double Longitud { get; init; }
    public List<BultoItemViewModel> Bultos { get; init; } = new();

    // Helpers visuales para el renderizado del badge en XAML
    public Color EstadoBgColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#DCFCE7"),
        "Fallido" => Color.FromArgb("#FEE2E2"),
        _ => Color.FromArgb("#FEF3C7")
    };

    public Color EstadoTextColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#15803D"),
        "Fallido" => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#B45309")
    };

    public Color EstadoBorderColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#86EFAC"),
        "Fallido" => Color.FromArgb("#FCA5A5"),
        _ => Color.FromArgb("#FCD34D")
    };

    public string EstadoTextoBadge => Estado switch
    {
        "Entregado" => "Entregado",
        "Fallido" => "Fallido",
        _ => "Pendiente"
    };

    public bool PuedeRegistrar => Estado == "Pendiente";
}

public partial class RutaDiaViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private string _fechaHojaRuta;

    [ObservableProperty]
    private bool _isOffline = true;

    [ObservableProperty]
    private bool _isOfflineSimulado = false;

    [ObservableProperty]
    private double _progresoRuta = 0.0;

    [ObservableProperty]
    private string _resumenProgresoTexto = "0 de 0 paradas completadas";

    [ObservableProperty]
    private string _progresoPorcentajeTexto = "0%";

    [ObservableProperty]
    private int _colaOfflineCount = 0;

    [ObservableProperty]
    private string _textoBadgeConectividad = "Modo Offline";

    [ObservableProperty]
    private Color _badgeConectividadBgColor = Color.FromArgb("#FEF2F2");

    [ObservableProperty]
    private Color _badgeConectividadBorderColor = Color.FromArgb("#FCA5A5");

    [ObservableProperty]
    private Color _badgeConectividadTextColor = Color.FromArgb("#991B1B");

    [ObservableProperty]
    private Color _indicadorConexionDotColor = Color.FromArgb("#DC2626");

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel() : this(null, null)
    {
    }

    public RutaDiaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _fechaHojaRuta = DateTime.Now.ToString("dd/MM/yyyy");

        if (_syncService != null)
        {
            _syncService.OnSyncStateChanged += OnSyncStateChangedHandler;
        }

        ActualizarEstadoVisualConectividad(_syncService?.IsOnline ?? false, 0);
    }

    private void OnSyncStateChangedHandler(object? sender, SyncStateChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ActualizarEstadoVisualConectividad(e.IsOnline, e.OperacionesPendientes);
            await RefrescarParadasDesdeSQLiteAsync();
        });
    }

    public async Task InicializarAsync()
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;

            if (_syncService is not null)
            {
                var paradas = await _syncService.DescargarRutaDelDiaAsync();
                Paradas.Clear();
                foreach (var p in paradas)
                {
                    Paradas.Add(p);
                }

                int pendientes = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(_syncService.IsOnline, pendientes);
            }
            else if (_localDatabaseService is not null)
            {
                var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                Paradas.Clear();
                if (local != null)
                {
                    foreach (var p in local.Paradas)
                    {
                        Paradas.Add(p);
                    }
                }
                int pendientes = await _localDatabaseService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(false, pendientes);
            }

            RecalcularProgreso();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefrescarParadasDesdeSQLiteAsync()
    {
        if (_localDatabaseService is null)
            return;

        try
        {
            var local = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            if (local != null && local.Paradas.Count > 0)
            {
                Paradas.Clear();
                foreach (var p in local.Paradas)
                {
                    Paradas.Add(p);
                }
            }
            RecalcularProgreso();
        }
        catch
        {
            // Fallback
        }
    }

    public void ActualizarEstadoVisualConectividad(bool isOnline, int pendientes)
    {
        IsOffline = !isOnline;
        IsOfflineSimulado = _syncService?.IsOfflineSimulado ?? false;
        ColaOfflineCount = pendientes;

        if (isOnline)
        {
            TextoBadgeConectividad = pendientes > 0 ? $"En Línea ({pendientes} pend.)" : "En Línea";
            BadgeConectividadBgColor = Color.FromArgb("#F0FDF4"); // Verde suave
            BadgeConectividadBorderColor = Color.FromArgb("#86EFAC");
            BadgeConectividadTextColor = Color.FromArgb("#166534");
            IndicadorConexionDotColor = Color.FromArgb("#16A34A");
        }
        else
        {
            string motivo = IsOfflineSimulado ? "Offline (Demo)" : "Modo Offline";
            TextoBadgeConectividad = pendientes > 0 ? $"{motivo} ({pendientes} pend.)" : motivo;
            BadgeConectividadBgColor = Color.FromArgb("#FEF2F2"); // Rojo suave
            BadgeConectividadBorderColor = Color.FromArgb("#FCA5A5");
            BadgeConectividadTextColor = Color.FromArgb("#991B1B");
            IndicadorConexionDotColor = Color.FromArgb("#DC2626");
        }
    }

    public void RecalcularProgreso()
    {
        if (Paradas.Count == 0)
        {
            ProgresoRuta = 0;
            ResumenProgresoTexto = "0 de 0 paradas completadas";
            ProgresoPorcentajeTexto = "0%";
            return;
        }

        int completadas = Paradas.Count(p => p.Estado == "Entregado" || p.Estado == "Fallido");
        int total = Paradas.Count;
        ProgresoRuta = (double)completadas / total;
        ResumenProgresoTexto = $"{completadas} de {total} paradas completadas";
        ProgresoPorcentajeTexto = $"{Math.Round(ProgresoRuta * 100)}%";
    }

    [RelayCommand]
    private async Task MostrarColaOfflineAsync()
    {
        if (Shell.Current is null)
            return;

        int pendientes = _syncService != null
            ? await _syncService.ObtenerCantidadPendientesAsync()
            : (_localDatabaseService != null ? await _localDatabaseService.ObtenerCantidadPendientesAsync() : 0);

        ColaOfflineCount = pendientes;

        string opcionToggle = (_syncService?.IsOfflineSimulado ?? false)
            ? "Desactivar Modo Offline (Pasar a Online)"
            : "Activar Modo Offline Simulado (Demo)";

        const string opcionSync = "Sincronizar Ahora con Backend";
        const string opcionReset = "Reiniciar Datos de Demo (Local + Backend)";

        var seleccion = await Shell.Current.DisplayActionSheetAsync(
            $"Conectividad y Cola Offline ({pendientes} pendientes)",
            "Cerrar",
            null,
            opcionToggle,
            opcionSync,
            opcionReset);

        if (seleccion == opcionToggle && _syncService is not null)
        {
            await _syncService.AlternarModoOfflineSimuladoAsync();

            if (_syncService.IsOnline)
            {
                var syncRes = await _syncService.SincronizarOperacionesPendientesAsync();
                int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(true, pendientesRestantes);
                await RefrescarParadasDesdeSQLiteAsync();

                string mensaje = syncRes.ErroresNegocio > 0
                    ? $"Modo Online activado.\n{syncRes.Mensaje}"
                    : $"Modo Online activado.\n{syncRes.Mensaje}";

                await Shell.Current.DisplayAlertAsync("Sincronización al Volver a Línea", mensaje, "Aceptar");
            }
            else
            {
                int p = await _syncService.ObtenerCantidadPendientesAsync();
                ActualizarEstadoVisualConectividad(false, p);
                await Shell.Current.DisplayAlertAsync(
                    "Modo de Conexión",
                    "Modo Offline Simulado activado para demostración. Las operaciones se registrarán de forma 100% local en SQLite cifrado.",
                    "Aceptar");
            }

            await RefrescarParadasDesdeSQLiteAsync();
        }
        else if (seleccion == opcionSync && _syncService is not null)
        {
            if (!_syncService.IsOnline)
            {
                await Shell.Current.DisplayAlertAsync(
                    "No es posible sincronizar",
                    "El dispositivo se encuentra en Modo Offline. Desactive el modo offline simulado para transferir la cola al backend.",
                    "Entendido");
                return;
            }

            var resultado = await _syncService.SincronizarOperacionesPendientesAsync();
            int pendientesRestantes = await _syncService.ObtenerCantidadPendientesAsync();
            ActualizarEstadoVisualConectividad(true, pendientesRestantes);
            await Shell.Current.DisplayAlertAsync("Sincronización de Cola", resultado.Mensaje, "Aceptar");
            await RefrescarParadasDesdeSQLiteAsync();
        }
        else if (seleccion == opcionReset && _syncService is not null)
        {
            bool confirmar = await Shell.Current.DisplayAlertAsync(
                "Reiniciar Datos de Demo",
                "¿Desea restablecer los envíos ENV-2026-00108, ENV-2026-00109 y ENV-2026-00110 a estado 'EnTransito' (4) en PostgreSQL y limpiar SQLite local (cola en 0)?",
                "Sí, reiniciar demo",
                "Cancelar");

            if (confirmar)
            {
                IsBusy = true;
                try
                {
                    var res = await _syncService.ReiniciarDemoCompletaAsync();
                    ActualizarEstadoVisualConectividad(_syncService.IsOnline, 0);
                    await RefrescarParadasDesdeSQLiteAsync();
                    await Shell.Current.DisplayAlertAsync("Reinicio de Demo", res, "Aceptar");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        await Shell.Current.GoToAsync("DetalleParadaPage", new Dictionary<string, object>
        {
            { "Parada", parada }
        });
    }

    [RelayCommand]
    private async Task LlamarClienteAsync(ParadaItemViewModel? parada)
    {
        if (parada is null || string.IsNullOrWhiteSpace(parada.Telefono))
            return;

        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(parada.Telefono);
            }
            else
            {
                await Launcher.Default.OpenAsync($"tel:{parada.Telefono}");
            }
        }
        catch
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Llamar al Cliente",
                    $"Marcando a {parada.Destinatario} ({parada.Telefono})...\nSimulación en entorno local.",
                    "Cerrar");
            }
        }
    }

    [RelayCommand]
    private async Task NavegarAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        try
        {
            var destination = (parada.Latitud != 0 && parada.Longitud != 0)
                ? $"{parada.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture)},{parada.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : Uri.EscapeDataString($"{parada.Direccion}, {parada.Ciudad}");

            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                var mapsUrl = $"https://www.google.com/maps/dir/?api=1&destination={destination}";
                await Launcher.Default.OpenAsync(mapsUrl);
            }
            else
            {
                var location = new Location(parada.Latitud, parada.Longitud);
                var options = new MapLaunchOptions
                {
                    Name = parada.Direccion,
                    NavigationMode = NavigationMode.Driving
                };

                await Map.Default.OpenAsync(location, options);
            }
        }
        catch
        {
            var fallbackDest = Uri.EscapeDataString($"{parada.Direccion}, {parada.Ciudad}");
            await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={fallbackDest}");
        }
    }
}
