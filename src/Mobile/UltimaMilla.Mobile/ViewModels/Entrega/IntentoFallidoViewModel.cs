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

/// <summary>
/// Intento de entrega fallido (UC-21): motivo del catálogo del operador, observaciones, fotografía real de la fachada y posición real.
/// </summary>
[QueryProperty(nameof(Parada), "Parada")]
public partial class IntentoFallidoViewModel : EvidenciaViewModelBase
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
    private string _coordenadasGps = "Consultando ubicación GPS...";

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

    partial void OnParadaChanged(ParadaItemViewModel? value)
    {
        _ = CargarMotivosAsync();
        _ = ActualizarPosicionAsync();
    }

    public async Task ActualizarPosicionAsync()
    {
        await ActualizarUbicacionYFechaAsync();
        CoordenadasGps = PosicionGpsTexto;
    }

    /// <summary>El catálogo es el del operador, descargado con la hoja de ruta. Sin catálogo no se puede registrar un fallo.</summary>
    public async Task CargarMotivosAsync()
    {
        if (_localDatabaseService is null)
            return;

        try
        {
            var motivos = await _localDatabaseService.ObtenerMotivosNoEntregaAsync();

            _motivosCatalogo.Clear();
            MotivosNoEntrega.Clear();

            foreach (var m in motivos)
            {
                _motivosCatalogo.Add(m);
                MotivosNoEntrega.Add(m.Descripcion);
            }

            if (string.IsNullOrEmpty(MotivoSeleccionado) && MotivosNoEntrega.Count > 0)
            {
                MotivoSeleccionado = MotivosNoEntrega[0];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IntentoFallidoViewModel] No se pudo leer el catálogo de motivos: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TomarFotoEvidenciaAsync()
    {
        if (IsBusy)
            return;

        await CapturarFotoAsync();
    }

    [RelayCommand]
    private async Task RegistrarIncidenciaAsync()
    {
        if (IsBusy)
            return;

        var parada = Parada;
        if (parada is null || parada.Id == Guid.Empty)
        {
            await AvisarAsync("Parada no disponible", "No se pudo identificar la parada. Vuelva a la hoja de ruta e intente nuevamente.");
            return;
        }

        var motivo = _motivosCatalogo.FirstOrDefault(m =>
            string.Equals(m.Descripcion, MotivoSeleccionado, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Codigo, MotivoSeleccionado, StringComparison.OrdinalIgnoreCase));

        if (motivo is null)
        {
            await AvisarAsync(
                "Motivo requerido",
                "Seleccione el motivo de no entrega. Si la lista está vacía, descargue la hoja de ruta con conexión para obtener el catálogo del operador.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Observaciones))
        {
            await AvisarAsync("Observaciones requeridas", "Detalle brevemente la situación ocurrida en el domicilio antes de registrar la incidencia.");
            return;
        }

        try
        {
            IsBusy = true;

            await ActualizarPosicionAsync();

            var payload = new IntentoFallidoOfflinePayload
            {
                HojaDeRutaId = parada.HojaDeRutaId,
                ParadaId = parada.Id,
                EnvioId = parada.EnvioId,
                IdempotencyKey = $"FALLO-{parada.Id:N}-{DateTime.UtcNow.Ticks}",
                MotivoNoEntregaId = motivo.Id,
                MotivoCodigo = motivo.Codigo,
                Observaciones = Observaciones.Trim(),
                FotoEvidenciaBase64 = TieneFoto ? FotoEvidenciaBase64 : null,
                Latitud = LatitudActual,
                Longitud = LongitudActual,
                FechaHoraDispositivoUtc = DateTime.UtcNow
            };

            // 1. Encolar en la base cifrada y actualizar el estado local
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.FalloEntrega,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(parada.Id, "Fallido");
            }

            // 2. Estado en memoria
            parada.Estado = "Fallido";

            // 3. Sincronizar si hay conexión
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
                        System.Diagnostics.Debug.WriteLine($"[IntentoFallidoViewModel] Sincronización en segundo plano: {ex.Message}");
                    }
                });
            }

            if (Shell.Current is not null)
            {
                var infoRed = _syncService?.IsOnline == true
                    ? "Se está enviando a la central, que aplicará la regla operativa (reprogramación o devolución)."
                    : "Guardado en el dispositivo. Se resolverá la regla operativa al recuperar la conexión.";

                await Shell.Current.DisplayAlertAsync(
                    "Intento fallido registrado",
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
