using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Components;
using UltimaMilla.Mobile.Views.Entrega;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

[QueryProperty(nameof(Parada), "Parada")]
public partial class DetalleParadaViewModel : ObservableObject, ICorporateAvisoModalHost
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneNotasComercio))]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarEntrega))]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoBloqueo))]
    [NotifyPropertyChangedFor(nameof(MensajeBloqueoEntrega))]
    [NotifyPropertyChangedFor(nameof(EsRetiro))]
    [NotifyPropertyChangedFor(nameof(EsEntrega))]
    [NotifyPropertyChangedFor(nameof(CodigoEtiquetaTitulo))]
    [NotifyPropertyChangedFor(nameof(ContactoSeccionTitulo))]
    [NotifyPropertyChangedFor(nameof(BultosSeccionTitulo))]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarEntrega))]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoBloqueo))]
    [NotifyPropertyChangedFor(nameof(MensajeBloqueoEntrega))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarEntrega))]
    [NotifyPropertyChangedFor(nameof(MensajeBloqueoEntrega))]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoBloqueo))]
    [NotifyPropertyChangedFor(nameof(RutaActivaEnCalle))]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    private string _estadoHojaDeRuta = "EnTransito";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    private ObservableCollection<BultoItemViewModel> _bultos = new();

    public bool EsRetiro => Parada?.EsRetiro == true;
    public bool EsEntrega => !EsRetiro;

    public string CodigoEtiquetaTitulo => EsRetiro ? "Código de Retiro" : "Código de Envío";
    public string ContactoSeccionTitulo => EsRetiro ? "Datos del Solicitante / Origen" : "Datos del Destinatario";
    public string BultosSeccionTitulo => EsRetiro ? "Bultos a Retirar" : "Bultos del Envío";

    public bool TieneNotasComercio => !string.IsNullOrWhiteSpace(Parada?.NotasComercio);

    public bool RutaActivaEnCalle => EstadoHojaDeRuta is "Despachada" or "EnTransito";

    public bool PuedeAccionar => Parada?.Estado == "Pendiente" && (EsRetiro || RutaActivaEnCalle) && !IsBusy;

    public bool PuedeConfirmarEntrega =>
        Parada?.Estado == "Pendiente" &&
        (EsRetiro || RutaActivaEnCalle) &&
        !IsBusy;

    public bool MostrarAvisoBloqueo =>
        Parada?.Estado == "Pendiente" && !EsRetiro && !RutaActivaEnCalle;

    public string MensajeBloqueoEntrega
    {
        get
        {
            if (!EsRetiro && !RutaActivaEnCalle)
                return "La hoja de ruta todavía no está en calle. Escanee y confirme la carga, y espere el despacho del depósito.";
            return string.Empty;
        }
    }



    public string ResumenBultosTexto
    {
        get
        {
            if (Bultos.Count == 0 && (Parada is null || Parada.Bultos.Count == 0))
                return $"{Parada?.CantidadBultos ?? 0} bulto(s) asociados";

            IEnumerable<BultoItemViewModel> lista = Bultos.Count > 0
                ? Bultos
                : (Parada?.Bultos ?? (IEnumerable<BultoItemViewModel>)Array.Empty<BultoItemViewModel>());

            var totalPeso = lista.Sum(b => b.PesoKg);
            int total = lista.Count();
            return totalPeso > 0 ? $"{total} bulto(s) • {totalPeso:F1} kg" : $"{total} bulto(s)";
        }
    }

    public DetalleParadaViewModel() : this(null, null)
    {
    }

    public DetalleParadaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
    }

    partial void OnParadaChanged(ParadaItemViewModel? value)
    {
        _ = CargarDatosParadaAsync(value);
    }

    public async Task CargarDatosParadaAsync(ParadaItemViewModel? p)
    {
        if (p == null)
            return;

        Bultos.Clear();

        if (_localDatabaseService != null)
        {
            var rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
            if (rutaLocal?.Ruta != null)
            {
                EstadoHojaDeRuta = rutaLocal.Ruta.Estado;
            }

            var paradaDb = rutaLocal?.Paradas.FirstOrDefault(x => x.Id == p.Id);
            foreach (var b in (paradaDb?.Bultos ?? p.Bultos))
            {
                Bultos.Add(b);
            }
        }
        else
        {
            foreach (var b in p.Bultos)
            {
                Bultos.Add(b);
            }
        }

        OnPropertyChanged(nameof(PuedeConfirmarEntrega));
        OnPropertyChanged(nameof(MostrarAvisoBloqueo));
        OnPropertyChanged(nameof(MensajeBloqueoEntrega));
        OnPropertyChanged(nameof(ResumenBultosTexto));
    }

    [RelayCommand]
    private async Task NavegarAsync()
    {
        if (Parada is null)
            return;

        try
        {
            var tieneCoordenadas = Parada.Latitud != 0 && Parada.Longitud != 0;
            var destination = tieneCoordenadas
                ? $"{Parada.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture)},{Parada.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : Uri.EscapeDataString($"{Parada.Direccion}, {Parada.Ciudad}");

            // En Windows, la app UWP 'Mapas' fue discontinuada por Microsoft.
            // Abrimos directamente la ruta en Google Maps en el navegador predeterminado para navegación inmediata.
            if (DeviceInfo.Platform == DevicePlatform.WinUI || !tieneCoordenadas)
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DetalleParadaViewModel] No se pudo abrir el mapa nativo: {ex.Message}");
            var fallbackDest = Uri.EscapeDataString($"{Parada.Direccion}, {Parada.Ciudad}");
            await Launcher.Default.OpenAsync($"https://www.google.com/maps/dir/?api=1&destination={fallbackDest}");
        }
    }

    [ObservableProperty]
    private bool _mostrarModalAviso;

    [ObservableProperty]
    private string _tituloModalAviso = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneSubtituloModalAviso))]
    private string _subtituloModalAviso = string.Empty;

    [ObservableProperty]
    private string _mensajeModalAviso = string.Empty;

    [ObservableProperty]
    private string _textoBotonModalAviso = "Entendido";

    public bool TieneSubtituloModalAviso => !string.IsNullOrWhiteSpace(SubtituloModalAviso);

    ICommand ICorporateAvisoModalHost.CerrarModalAvisoCommand => CerrarModalAvisoCommand;

    [RelayCommand]
    private void CerrarModalAviso()
    {
        MostrarModalAviso = false;
    }

    private void MostrarAvisoModal(string titulo, string mensaje, string subtitulo = "Validación de ruta")
    {
        TituloModalAviso = titulo;
        SubtituloModalAviso = subtitulo;
        MensajeModalAviso = mensaje;
        TextoBotonModalAviso = "Entendido";
        MostrarModalAviso = true;
    }

    [RelayCommand]
    private async Task IrAConfirmarEntregaAsync()
    {
        if (Parada is null)
            return;

        if (!PuedeConfirmarEntrega)
        {
            MostrarAvisoModal("Entrega bloqueada", MensajeBloqueoEntrega);
            return;
        }

        await Shell.Current.GoToAsync(nameof(RegistroEntregaPage), new Dictionary<string, object>
        {
            { "Parada", Parada }
        });
    }

    [RelayCommand]
    private async Task IrARegistrarRetiroAsync()
    {
        await AbrirRegistroRetiroAsync(modoFallo: false);
    }

    [RelayCommand]
    private async Task IrARegistrarNoRetiradoAsync()
    {
        await AbrirRegistroRetiroAsync(modoFallo: true);
    }

    private async Task AbrirRegistroRetiroAsync(bool modoFallo)
    {
        if (Parada is null)
            return;

        if (!PuedeConfirmarEntrega)
        {
            MostrarAvisoModal("Retiro bloqueado", MensajeBloqueoEntrega);
            return;
        }

        if (_localDatabaseService is not null && Parada.HojaDeRutaId != Guid.Empty && EstadoHojaDeRuta is "Liberada" or "EnCarga" or "Despachada")
        {
            EstadoHojaDeRuta = "EnTransito";
            await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(Parada.HojaDeRutaId, "EnTransito");
        }

        await Shell.Current.GoToAsync(nameof(RegistroRetiroPage), new Dictionary<string, object>
        {
            { "Parada", Parada },
            { "ModoFallo", modoFallo }
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
