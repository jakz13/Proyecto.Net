using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Services.Tracking;
using UltimaMilla.Mobile.Views.Entrega;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

[QueryProperty(nameof(Parada), "Parada")]
public partial class DetalleParadaViewModel : ObservableObject
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
    [NotifyPropertyChangedFor(nameof(PuedeRegistrarRetiroDevolucion))]
    [NotifyPropertyChangedFor(nameof(EsRetiro))]
    [NotifyPropertyChangedFor(nameof(EsEntrega))]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarEntrega))]
    [NotifyPropertyChangedFor(nameof(MostrarAvisoBloqueo))]
    [NotifyPropertyChangedFor(nameof(MensajeBloqueoEntrega))]
    [NotifyPropertyChangedFor(nameof(PuedeRegistrarRetiroDevolucion))]
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

    public bool TieneNotasComercio => !string.IsNullOrWhiteSpace(Parada?.NotasComercio);

    public bool RutaActivaEnCalle => EstadoHojaDeRuta is "Despachada" or "EnTransito";

    public bool PuedeAccionar => Parada?.Estado == "Pendiente" && RutaActivaEnCalle && !IsBusy;

    public bool PuedeConfirmarEntrega =>
        Parada?.Estado == "Pendiente" &&
        RutaActivaEnCalle &&
        !IsBusy;

    public bool MostrarAvisoBloqueo =>
        Parada?.Estado == "Pendiente" && !RutaActivaEnCalle;

    public string MensajeBloqueoEntrega
    {
        get
        {
            if (!RutaActivaEnCalle)
                return "La hoja de ruta todavía no está en calle. Escanee y confirme la carga, y espere el despacho del depósito.";
            return string.Empty;
        }
    }

    public bool PuedeRegistrarRetiroDevolucion =>
        Parada is not null &&
        !Parada.EsRetiro &&
        Parada.Estado != "Entregado" &&
        Parada.Estado != "EnDevolucion" &&
        !IsBusy;

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

    [RelayCommand]
    private async Task IrAConfirmarEntregaAsync()
    {
        if (Parada is null)
            return;

        if (!PuedeConfirmarEntrega)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Entrega bloqueada",
                    MensajeBloqueoEntrega,
                    "Entendido");
            }
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
        if (Parada is null)
            return;

        if (!PuedeConfirmarEntrega)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Retiro bloqueado",
                    MensajeBloqueoEntrega,
                    "Entendido");
            }
            return;
        }

        await Shell.Current.GoToAsync(nameof(RegistroRetiroPage), new Dictionary<string, object>
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
    private async Task RegistrarRetiroDevolucionAsync()
    {
        if (IsBusy || Parada is null || Parada.Id == Guid.Empty)
            return;

        string observaciones = "Bulto retirado para devolución a depósito";

        if (Shell.Current is not null)
        {
            var input = await Shell.Current.DisplayPromptAsync(
                "Retiro de devolución",
                "Indique observaciones del retiro del bulto para su retorno al depósito:",
                "Confirmar retiro",
                "Cancelar",
                "Observaciones",
                initialValue: observaciones);

            if (input is null)
                return;

            if (!string.IsNullOrWhiteSpace(input))
                observaciones = input.Trim();
        }

        try
        {
            IsBusy = true;

            var parada = Parada;
            var ubicacion = await UbicacionDispositivo.ObtenerAsync();

            var payload = new RetiroDevolucionOfflinePayload
            {
                HojaDeRutaId = parada.HojaDeRutaId,
                ParadaId = parada.Id,
                EnvioId = parada.EnvioId,
                IdempotencyKey = $"RET-DEV-{parada.Id:N}",
                Latitud = ubicacion?.Latitude,
                Longitud = ubicacion?.Longitude,
                FechaHoraDispositivoUtc = DateTime.UtcNow,
                Observaciones = observaciones
            };

            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.RetiroDevolucion,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(parada.Id, "EnDevolucion");
            }

            parada.Estado = "EnDevolucion";
            OnPropertyChanged(nameof(PuedeAccionar));
            OnPropertyChanged(nameof(PuedeRegistrarRetiroDevolucion));

            if (_syncService?.IsOnline == true)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _syncService.SincronizarOperacionesPendientesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DetalleParadaViewModel] Sincronización en segundo plano: {ex.Message}");
                    }
                });
            }

            if (Shell.Current is not null)
            {
                string infoRed = (_syncService?.IsOnline == true)
                    ? "Se está enviando a la central."
                    : "Guardado en el dispositivo. Se enviará automáticamente al recuperar la conexión.";

                await Shell.Current.DisplayAlertAsync(
                    "Retiro de devolución registrado",
                    $"El retiro de bulto(s) del envío {parada.CodigoSeguimiento} para retorno a depósito fue registrado.\n\n{infoRed}",
                    "Aceptar");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VolverAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
