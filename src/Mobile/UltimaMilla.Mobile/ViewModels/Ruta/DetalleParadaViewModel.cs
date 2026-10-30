using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
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
    [NotifyPropertyChangedFor(nameof(PuedeRegistrarRetiroDevolucion))]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAccionar))]
    [NotifyPropertyChangedFor(nameof(PuedeRegistrarRetiroDevolucion))]
    private bool _isBusy;

    public bool TieneNotasComercio => !string.IsNullOrWhiteSpace(Parada?.NotasComercio);

    public bool PuedeAccionar => Parada?.Estado == "Pendiente" && !IsBusy;

    public bool PuedeRegistrarRetiroDevolucion =>
        Parada is not null &&
        Parada.Estado != "Entregado" &&
        Parada.Estado != "EnDevolucion" &&
        !IsBusy;

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

    public DetalleParadaViewModel() : this(null, null)
    {
    }

    public DetalleParadaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;

        // Mock por defecto para previsualización directa
        _parada = new ParadaItemViewModel
        {
            Id = DemoDataDefaults.Parada2Id,
            HojaDeRutaId = DemoDataDefaults.DefaultHojaDeRutaId,
            EnvioId = DemoDataDefaults.Envio2Id,
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
    private async Task RegistrarRetiroDevolucionAsync()
    {
        if (IsBusy || Parada is null)
            return;

        string observaciones = "Retiro de bulto en calle para retorno a depósito";

        if (Shell.Current is not null)
        {
            var input = await Shell.Current.DisplayPromptAsync(
                "Retiro de Devolución",
                "Indique observaciones del retiro del bulto para su retorno al depósito:",
                "Confirmar Retiro",
                "Cancelar",
                "Ej: Bulto retirado en domicilio para devolución",
                initialValue: "Bulto retirado para devolución a depósito");

            if (input is null)
                return;

            if (!string.IsNullOrWhiteSpace(input))
                observaciones = input.Trim();
        }

        try
        {
            IsBusy = true;

            var parada = Parada;
            var hojaDeRutaId = (parada.HojaDeRutaId != Guid.Empty)
                ? parada.HojaDeRutaId
                : DemoDataDefaults.DefaultHojaDeRutaId;

            var paradaId = (parada.Id != Guid.Empty)
                ? parada.Id
                : (parada.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Parada1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Parada2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Parada3Id,
                    _ => DemoDataDefaults.Parada3Id
                });

            var envioId = (parada.EnvioId != Guid.Empty)
                ? parada.EnvioId
                : (parada.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Envio1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Envio2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Envio3Id,
                    _ => DemoDataDefaults.Envio3Id
                });

            var payload = new RetiroDevolucionOfflinePayload
            {
                HojaDeRutaId = hojaDeRutaId,
                ParadaId = paradaId,
                EnvioId = envioId,
                IdempotencyKey = $"RET-DEV-{paradaId:N}-{DateTime.UtcNow.Ticks}",
                Latitud = parada.Latitud != 0 ? parada.Latitud : -34.9271,
                Longitud = parada.Longitud != 0 ? parada.Longitud : -56.1534,
                FechaHoraDispositivoUtc = DateTime.UtcNow,
                Observaciones = observaciones
            };

            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.RetiroDevolucion,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(paradaId, "EnDevolucion");
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
                    catch
                    {
                        // Fallback silente en background
                    }
                });
            }

            await Task.Delay(200);

            if (Shell.Current is not null)
            {
                string infoRed = (_syncService?.IsOnline == true)
                    ? "Sincronizando retiro de devolución con la central en tiempo real."
                    : "Guardado en SQLite cifrado local. Se sincronizará automáticamente al restablecerse la red.";

                await Shell.Current.DisplayAlertAsync(
                    "Retiro de Devolución Registrado",
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
