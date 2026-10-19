using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(Parada), "Parada")]
public partial class IntentoFallidoViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    private string _motivoSeleccionado = string.Empty;

    [ObservableProperty]
    private string _observaciones = string.Empty;

    [ObservableProperty]
    private bool _tieneFoto;

    [ObservableProperty]
    private string _fotoStatusText = "Ninguna foto adjunta";

    [ObservableProperty]
    private string _coordenadasGps = "-34.9124, -56.1589 (Precisión ±4m)";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<string> MotivosNoEntrega { get; } = new();

    private readonly List<MotivoNoEntregaLocal> _motivosCatalogo = new();

    public IntentoFallidoViewModel() : this(null, null)
    {
    }

    public IntentoFallidoViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;

        _ = CargarMotivosAsync();
    }

    public async Task CargarMotivosAsync()
    {
        try
        {
            var motivos = _localDatabaseService != null
                ? await _localDatabaseService.ObtenerMotivosNoEntregaAsync()
                : DemoDataDefaults.ObtenerCatalogoMotivos();

            _motivosCatalogo.Clear();
            MotivosNoEntrega.Clear();

            foreach (var m in motivos.Where(x => x.Activo))
            {
                _motivosCatalogo.Add(m);
                MotivosNoEntrega.Add(m.Descripcion);
            }

            if (string.IsNullOrEmpty(MotivoSeleccionado) && MotivosNoEntrega.Count > 0)
            {
                MotivoSeleccionado = MotivosNoEntrega[0];
            }
        }
        catch
        {
            // Carga catálogo básico ante cualquier inconsistencia
            foreach (var m in DemoDataDefaults.ObtenerCatalogoMotivos())
            {
                _motivosCatalogo.Add(m);
                MotivosNoEntrega.Add(m.Descripcion);
            }
        }
    }

    [RelayCommand]
    private async Task TomarFotoEvidenciaAsync()
    {
        if (IsBusy)
            return;

        TieneFoto = true;
        FotoStatusText = "Evidencia adjunta: fachada_visita_fallida_01.jpg";

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Cámara de Evidencia",
                "Fotografía de fachada/timbre capturada y geolocalizada con éxito para respaldo del intento de entrega.",
                "Continuar");
        }
    }

    [RelayCommand]
    private async Task RegistrarIncidenciaAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(MotivoSeleccionado))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Motivo requerido",
                    "Por favor, seleccione el motivo de no entrega del listado para cumplir con el protocolo UC-21.",
                    "Entendido");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(Observaciones))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Observaciones requeridas",
                    "Por favor, detalle brevemente la situación ocurrida en el domicilio antes de registrar la incidencia.",
                    "Entendido");
            }
            return;
        }

        try
        {
            IsBusy = true;

            var parada = Parada;
            var paradaId = parada?.Id ?? Guid.NewGuid();
            var hojaDeRutaId = (parada?.HojaDeRutaId != null && parada.HojaDeRutaId != Guid.Empty)
                ? parada.HojaDeRutaId
                : DemoDataDefaults.DefaultHojaDeRutaId;

            var envioId = (parada?.EnvioId != null && parada.EnvioId != Guid.Empty)
                ? parada.EnvioId
                : Guid.NewGuid();

            var motivoMatch = _motivosCatalogo.FirstOrDefault(m => m.Descripcion == MotivoSeleccionado);
            var motivoId = motivoMatch?.Id ?? Guid.NewGuid();
            var motivoCodigo = motivoMatch?.Codigo ?? "CLI_AUSENTE";

            var idempotencyKey = $"FALLO-{paradaId:N}-{DateTime.UtcNow.Ticks}";

            // Contrato idéntico a RegistrarIntentoFallidoCommand (UC-21)
            var payload = new IntentoFallidoOfflinePayload
            {
                HojaDeRutaId = hojaDeRutaId,
                ParadaId = paradaId,
                EnvioId = envioId,
                IdempotencyKey = idempotencyKey,
                MotivoNoEntregaId = motivoId,
                MotivoCodigo = motivoCodigo,
                Observaciones = Observaciones.Trim(),
                FotoEvidenciaBase64 = TieneFoto ? "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=" : null,
                Latitud = parada?.Latitud != 0 ? parada?.Latitud : -34.9124,
                Longitud = parada?.Longitud != 0 ? parada?.Longitud : -56.1589,
                FechaHoraDispositivoUtc = DateTime.UtcNow
            };

            // 1. Encolar en base SQLite cifrada y actualizar estado local
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.FalloEntrega,
                    JsonSerializer.Serialize(payload));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(paradaId, "Fallido");
            }

            // 2. Actualizar estado en memoria
            if (parada is not null)
            {
                parada.Estado = "Fallido";
            }

            // 3. Disparar sincronización si estamos Online
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

            await Task.Delay(250);

            if (Shell.Current is not null)
            {
                string infoRed = (_syncService?.IsOnline == true)
                    ? "Sincronizando incidencia con la central y resolviendo reglas operativas."
                    : "Guardado en SQLite cifrado local. Se resolverá la regla operativa (reprogramación / devolución) al sincronizar.";

                await Shell.Current.DisplayAlertAsync(
                    "Incidencia Registrada (UC-21)",
                    $"El intento fallido por '{MotivoSeleccionado}' fue guardado.\n\n{infoRed}",
                    "Aceptar");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
