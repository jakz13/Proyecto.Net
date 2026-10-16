using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
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

    [ObservableProperty]
    private string _fechaHojaRuta;

    [ObservableProperty]
    private bool _isOffline = true;

    [ObservableProperty]
    private double _progresoRuta = 0.33;

    [ObservableProperty]
    private string _resumenProgresoTexto = "1 de 3 paradas completadas";

    [ObservableProperty]
    private string _progresoPorcentajeTexto = "33%";

    [ObservableProperty]
    private int _colaOfflineCount = 2;

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel() : this(null)
    {
    }

    public RutaDiaViewModel(ILocalDatabaseService? localDatabaseService)
    {
        _localDatabaseService = localDatabaseService;
        _fechaHojaRuta = DateTime.Now.ToString("dd/MM/yyyy");
        CargarDatosMock();
        RecalcularProgreso();
    }

    public async Task InicializarAsync()
    {
        if (_localDatabaseService is not null)
        {
            try
            {
                var ops = await _localDatabaseService.ObtenerOperacionesPendientesAsync();
                ColaOfflineCount = ops.Count;
            }
            catch
            {
                // Mantiene el valor mock
            }
        }
        RecalcularProgreso();
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

    private void CargarDatosMock()
    {
        Paradas.Clear();

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 1,
            Direccion = "Av. 18 de Julio 1450, Apto 402",
            Ciudad = "Centro, Montevideo",
            Destinatario = "Martín Rodríguez",
            Telefono = "+598 99 123 456",
            CantidadBultos = 2,
            Estado = "Entregado",
            HoraEstimada = "09:30",
            CodigoSeguimiento = "ENV-2026-00108",
            NotasComercio = "Tocar timbre 402. Si no atiende, dejar en portería con guardia de turno.",
            Latitud = -34.9056,
            Longitud = -56.1852,
            Bultos = new List<BultoItemViewModel>
            {
                new() { Codigo = "BLT-001-A", Descripcion = "Caja mediana - Repuestos", PesoKg = 2.4m, EsFragil = false },
                new() { Codigo = "BLT-001-B", Descripcion = "Sobre acolchado - Facturas", PesoKg = 0.3m, EsFragil = false }
            }
        });

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 2,
            Direccion = "Bvar. España 2678, Apto 501",
            Ciudad = "Pocitos, Montevideo",
            Destinatario = "Lucía Fernández",
            Telefono = "+598 99 765 432",
            CantidadBultos = 1,
            Estado = "Pendiente",
            HoraEstimada = "10:15",
            CodigoSeguimiento = "ENV-2026-00109",
            NotasComercio = "Llamar al móvil 5 minutos antes de llegar. Producto frágil, mantener posición vertical.",
            Latitud = -34.9124,
            Longitud = -56.1589,
            Bultos = new List<BultoItemViewModel>
            {
                new() { Codigo = "BLT-002-A", Descripcion = "Caja cristalería - Delicado", PesoKg = 1.8m, EsFragil = true }
            }
        });

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 3,
            Direccion = "Rambla Gandhi 456, Piso 8",
            Ciudad = "Punta Carretas, Montevideo",
            Destinatario = "Esteban Morales",
            Telefono = "+598 98 999 111",
            CantidadBultos = 3,
            Estado = "Pendiente",
            HoraEstimada = "11:00",
            CodigoSeguimiento = "ENV-2026-00110",
            NotasComercio = "Horario estricto de entrega entre 10:45 y 11:30. Solicitar C.I. obligatoria.",
            Latitud = -34.9271,
            Longitud = -56.1534,
            Bultos = new List<BultoItemViewModel>
            {
                new() { Codigo = "BLT-003-A", Descripcion = "Bulto pesas / Deporte", PesoKg = 5.0m, EsFragil = false },
                new() { Codigo = "BLT-003-B", Descripcion = "Indumentaria deportiva", PesoKg = 1.2m, EsFragil = false },
                new() { Codigo = "BLT-003-C", Descripcion = "Calzado técnico", PesoKg = 1.1m, EsFragil = false }
            }
        });
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel? parada)
    {
        if (parada is null)
            return;

        // Navegación hacia la nueva pantalla de Detalle de Parada (vista previa previa a entrega/fallo)
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

            // En Windows, la app UWP 'Mapas' (Bing Maps) fue discontinuada por Microsoft.
            // Abrimos directamente la ruta en Google Maps en el navegador predeterminado para navegación inmediata.
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

    [RelayCommand]
    private async Task MostrarColaOfflineAsync()
    {
        int count = ColaOfflineCount;
        if (_localDatabaseService is not null)
        {
            try
            {
                var pendientes = await _localDatabaseService.ObtenerOperacionesPendientesAsync();
                count = pendientes.Count;
                ColaOfflineCount = count;
            }
            catch
            {
                // Fallback a valor interno
            }
        }

        if (Shell.Current is not null)
        {
            string mensaje = count == 0
                ? "No hay operaciones pendientes de sincronización. Todos los remitos y estados están al día con la central."
                : $"Hay {count} operación(es) registradas localmente en la base cifrada SQLite (RNF 6.11).\n\nSe sincronizarán automáticamente en segundo plano cuando la conectividad 4G/WiFi se restablezca.";

            await Shell.Current.DisplayAlertAsync(
                "Estado de Conectividad Offline",
                mensaje,
                "Entendido");
        }
    }

    [RelayCommand]
    private void AlternarModoRed()
    {
        IsOffline = !IsOffline;
    }
}
