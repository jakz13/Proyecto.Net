using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Views.Entrega;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

[QueryProperty(nameof(Parada), "Parada")]
public partial class DetalleParadaViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneNotasComercio))]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    private ParadaItemViewModel? _parada;

    public bool TieneNotasComercio => !string.IsNullOrWhiteSpace(Parada?.NotasComercio);

    public bool PuedeAccionar => Parada?.Estado == "Pendiente";

    public string ResumenBultosTexto
    {
        get
        {
            if (Parada is null || Parada.Bultos.Count == 0)
                return $"{Parada?.CantidadBultos ?? 0} bultos asociados";

            var totalPeso = Parada.Bultos.Sum(b => b.PesoKg);
            return $"{Parada.Bultos.Count} bultos declarados • {totalPeso:F1} kg total";
        }
    }

    public DetalleParadaViewModel()
    {
        // Mock por defecto para previsualización directa
        _parada = new ParadaItemViewModel
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
            NotasComercio = "Llamar al móvil 5 minutos antes de llegar. Producto frágil, mantener posición vertical y no dejar en el piso exterior.",
            Latitud = -34.9124,
            Longitud = -56.1589,
            Bultos = new List<BultoItemViewModel>
            {
                new() { Codigo = "BLT-002-A", Descripcion = "Caja cristalería y vajilla fina", PesoKg = 1.8m, EsFragil = true }
            }
        };
    }

    [RelayCommand]
    private async Task LlamarClienteAsync()
    {
        if (Parada is null || string.IsNullOrWhiteSpace(Parada.Telefono))
            return;

        try
        {
            if (PhoneDialer.Default.IsSupported)
            {
                PhoneDialer.Default.Open(Parada.Telefono);
            }
            else
            {
                await Launcher.Default.OpenAsync($"tel:{Parada.Telefono}");
            }
        }
        catch
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Llamar al Cliente",
                    $"Marcando a {Parada.Destinatario} ({Parada.Telefono})...\nSimulación en entorno de prueba.",
                    "Cerrar");
            }
        }
    }

    [RelayCommand]
    private async Task NavegarAsync()
    {
        if (Parada is null)
            return;

        try
        {
            var destination = (Parada.Latitud != 0 && Parada.Longitud != 0)
                ? $"{Parada.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Parada.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : Uri.EscapeDataString($"{Parada.Direccion}, {Parada.Ciudad}");

            // En Windows, la app UWP 'Mapas' fue discontinuada por Microsoft.
            // Abrimos directamente la ruta en Google Maps en el navegador predeterminado para navegación inmediata.
            if (DeviceInfo.Platform == DevicePlatform.WinUI)
            {
                var mapsUrl = $"https://www.google.com/maps/dir/?api=1&destination={destination}";
                await Launcher.Default.OpenAsync(mapsUrl);
            }
            else
            {
                var location = new Location(Parada.Latitud, Parada.Longitud);
                var options = new MapLaunchOptions
                {
                    Name = Parada.Direccion,
                    NavigationMode = NavigationMode.Driving
                };

                await Map.Default.OpenAsync(location, options);
            }
        }
        catch
        {
            var fallbackDest = Uri.EscapeDataString($"{Parada.Direccion}, {Parada.Ciudad}");
            await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={fallbackDest}");
        }
    }

    [RelayCommand]
    private async Task IrAConfirmarEntregaAsync()
    {
        if (Parada is null)
            return;

        await Shell.Current.GoToAsync(nameof(RegistroEntregaPage), new Dictionary<string, object>
        {
            { "Parada", Parada }
        });
    }

    [RelayCommand]
    private async Task IrARegistrarFalloAsync()
    {
        if (Parada is null)
            return;

        await Shell.Current.GoToAsync(nameof(IntentoFallidoPage), new Dictionary<string, object>
        {
            { "Parada", Parada }
        });
    }

    [RelayCommand]
    private async Task VolverAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
