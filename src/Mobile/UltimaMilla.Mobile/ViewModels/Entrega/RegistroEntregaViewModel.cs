using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
public partial class RegistroEntregaViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ParadaItemViewModel? _paradaSeleccionada;

    [ObservableProperty]
    private string _nombreReceptor = string.Empty;

    [ObservableProperty]
    private string _documentoReceptor = string.Empty;

    [ObservableProperty]
    private bool _tieneFirma;

    [ObservableProperty]
    private bool _tieneFoto;

    [ObservableProperty]
    private string _fotoStatusText = "Ninguna foto adjunta";

    [ObservableProperty]
    private string _firmaStatusText = "Toque o dibuje para firmar";

    [ObservableProperty]
    private bool _isBusy;

    public RegistroEntregaViewModel() : this(null, null)
    {
    }

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;

        _nombreReceptor = "Lucía Fernández";
        _documentoReceptor = "4.567.890-1";
    }

    [RelayCommand]
    private async Task AbrirCamaraAsync()
    {
        if (IsBusy)
            return;

        TieneFoto = true;
        FotoStatusText = "Foto de entrega adjunta (remito_evidencia_01.jpg)";

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Cámara de Remito",
                "Fotografía de comprobante capturada correctamente.",
                "Aceptar");
        }
    }

    [RelayCommand]
    private void SimularFirma()
    {
        TieneFirma = true;
        FirmaStatusText = "Firma digital capturada (240 pts vectoriales)";
    }

    [RelayCommand]
    private void LimpiarFirma()
    {
        TieneFirma = false;
        FirmaStatusText = "Toque o dibuje para firmar";
    }

    [RelayCommand]
    private async Task GuardarEntregaAsync()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(NombreReceptor) || string.IsNullOrWhiteSpace(DocumentoReceptor))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Campos requeridos",
                    "Por favor, complete el nombre y documento del receptor.",
                    "Entendido");
            }
            return;
        }

        try
        {
            IsBusy = true;

            var parada = ParadaSeleccionada;
            var paradaId = parada?.Id ?? Guid.NewGuid();
            var hojaDeRutaId = (parada?.HojaDeRutaId != null && parada.HojaDeRutaId != Guid.Empty)
                ? parada.HojaDeRutaId
                : DemoDataDefaults.DefaultHojaDeRutaId;

            var envioId = (parada?.EnvioId != null && parada.EnvioId != Guid.Empty)
                ? parada.EnvioId
                : Guid.NewGuid();

            var idempotencyKey = $"ENTREGA-{paradaId:N}-{DateTime.UtcNow.Ticks}";

            // Contrato idéntico a RegistrarEntregaExitosaCommand (UC-20)
            var payload = new EntregaExitosaOfflinePayload
            {
                HojaDeRutaId = hojaDeRutaId,
                ParadaId = paradaId,
                EnvioId = envioId,
                IdempotencyKey = idempotencyKey,
                NombreReceptor = NombreReceptor.Trim(),
                DocumentoReceptor = DocumentoReceptor.Trim(),
                FirmaDigitalBase64 = TieneFirma ? "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==" : null,
                FotoEvidenciaBase64 = TieneFoto ? "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABPxA=" : null,
                Latitud = parada?.Latitud != 0 ? parada?.Latitud : -34.9056,
                Longitud = parada?.Longitud != 0 ? parada?.Longitud : -56.1852,
                FechaHoraDispositivoUtc = DateTime.UtcNow
            };

            // 1. Guardar en cola offline cifrada y actualizar estado local SQLite
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.EntregaExitosa,
                    JsonSerializer.Serialize(payload));

                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(paradaId, "Entregado");
            }

            // 2. Actualizar estado en modelo en memoria
            if (parada is not null)
            {
                parada.Estado = "Entregado";
            }

            // 3. Si la app está Online, disparar sincronización en background
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
                    ? "Sincronizando entrega con la central en tiempo real."
                    : "Guardado en SQLite cifrado local. Se sincronizará automáticamente al restablecerse la red.";

                await Shell.Current.DisplayAlertAsync(
                    "Entrega Exitosa (UC-20)",
                    $"La entrega a {NombreReceptor.Trim()} fue registrada exitosamente.\n\n{infoRed}",
                    "Finalizar");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
