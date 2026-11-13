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
/// Intento de entrega fallido: motivo del catálogo del operador, observaciones, fotografía real de la fachada y posición real.
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

    [ObservableProperty]
    private string _textoEtiquetaFoto = "Evidencia Fotográfica";

    [ObservableProperty]
    private bool _requiereFotoOperador = false;

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
        try
        {
            _motivosCatalogo.Clear();
            MotivosNoEntrega.Clear();

            IReadOnlyList<MotivoNoEntregaLocal>? motivos = null;
            if (_localDatabaseService is not null)
            {
                motivos = await _localDatabaseService.ObtenerMotivosNoEntregaAsync();
            }

            if (motivos is not null && motivos.Count > 0)
            {
                foreach (var m in motivos.Where(x => x.Activo))
                {
                    _motivosCatalogo.Add(m);
                    MotivosNoEntrega.Add(m.Descripcion);
                }
            }

            // Catálogo estándar predeterminado con tildes y español corporativo si no hay datos locales
            if (_motivosCatalogo.Count == 0)
            {
                var motivosDefault = new (Guid Id, string Codigo, string Descripcion)[]
                {
                    (Guid.Parse("dddddddd-0001-0000-0000-000000000001"), "CLI_AUSENTE", "Cliente ausente / No atiende llamadas"),
                    (Guid.Parse("dddddddd-0002-0000-0000-000000000002"), "DIR_INCORRECTA", "Dirección incorrecta o inexistente"),
                    (Guid.Parse("dddddddd-0003-0000-0000-000000000003"), "COMERCIO_CERRADO", "Negocio o comercio cerrado"),
                    (Guid.Parse("dddddddd-0004-0000-0000-000000000004"), "RECHAZADO_CLIENTE", "Rechazado expresamente por el cliente"),
                    (Guid.Parse("dddddddd-0005-0000-0000-000000000005"), "ZONA_INACCESIBLE", "Zona inaccesible o fuerza mayor"),
                    (Guid.Parse("dddddddd-0006-0000-0000-000000000006"), "BULTO_DANADO", "Bulto dañado / Avería en tránsito"),
                    (Guid.Parse("dddddddd-0007-0000-0000-000000000007"), "ERROR_PAGO", "Problemas con medio de pago / Cobro")
                };

                foreach (var item in motivosDefault)
                {
                    var mLocal = new MotivoNoEntregaLocal
                    {
                        Id = item.Id,
                        Codigo = item.Codigo,
                        Descripcion = item.Descripcion,
                        Activo = true
                    };
                    _motivosCatalogo.Add(mLocal);
                    MotivosNoEntrega.Add(item.Descripcion);
                }
            }

            if (string.IsNullOrEmpty(MotivoSeleccionado) && MotivosNoEntrega.Count > 0)
            {
                MotivoSeleccionado = MotivosNoEntrega[0];
            }

            var tipoPrueba = Preferences.Default.Get("ReglaOperativa_TipoPruebaEntrega", "").ToUpperInvariant();
            RequiereFotoOperador = tipoPrueba.Contains("FOTO");
            TextoEtiquetaFoto = RequiereFotoOperador
                ? "Evidencia Fotográfica (Obligatoria según regla del operador)"
                : "Evidencia Fotográfica (Recomendada)";
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

        if (RequiereFotoOperador && !TieneFoto)
        {
            await AvisarAsync(
                "Fotografía obligatoria",
                "La regla operativa del operador exige capturar una fotografía como evidencia del intento fallido.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Observaciones))
        {
            await AvisarAsync("Observaciones requeridas", "Detalle obligatoriamente lo sucedido en el domicilio antes de registrar la incidencia.");
            return;
        }

        var motivoCodigoUpper = motivo.Codigo?.ToUpperInvariant() ?? string.Empty;
        var motivoDescUpper = motivo.Descripcion?.ToUpperInvariant() ?? string.Empty;
        bool exigeDetalleExhaustivo = motivoCodigoUpper.Contains("DIR_INCORRECTA")
            || motivoCodigoUpper.Contains("BULTO_DANADO")
            || motivoDescUpper.Contains("DIRECCIÓN")
            || motivoDescUpper.Contains("OTRO");

        if (exigeDetalleExhaustivo && Observaciones.Trim().Length < 5)
        {
            await AvisarAsync(
                "Observaciones obligatorias",
                "Debe completar obligatoriamente las observaciones detallando la causa exacta de la incidencia.");
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
