using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
public partial class RegistroEntregaViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;

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

    public RegistroEntregaViewModel() : this(null)
    {
    }

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService)
    {
        _localDatabaseService = localDatabaseService;
        // Valores mock por defecto si se accede directamente
        _nombreReceptor = "Lucía Fernández";
        _documentoReceptor = "4.567.890-1";
    }

    [RelayCommand]
    private async Task AbrirCamaraAsync()
    {
        if (IsBusy)
            return;

        // Mock: Simulación de captura con cámara
        TieneFoto = true;
        FotoStatusText = "📷 Foto de entrega adjunta (remito_mock_01.jpg)";

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
        FirmaStatusText = "✍️ Firma digital capturada (240 pts vectoriales)";
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

            // Simulación de guardado en base de datos cifrada SQLite local (RNF 6.11)
            var payload = JsonSerializer.Serialize(new
            {
                ParadaId = ParadaSeleccionada?.Id ?? Guid.NewGuid(),
                CodigoSeguimiento = ParadaSeleccionada?.CodigoSeguimiento ?? "ENV-MOCK",
                NombreReceptor = NombreReceptor.Trim(),
                DocumentoReceptor = DocumentoReceptor.Trim(),
                TieneFirma = TieneFirma,
                TieneFoto = TieneFoto,
                FechaUtc = DateTime.UtcNow
            });

            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync("REGISTRAR_ENTREGA", payload);
            }

            if (ParadaSeleccionada is not null)
            {
                ParadaSeleccionada.Estado = "Entregado";
            }

            await Task.Delay(400);

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "¡Entrega Exitosa!",
                    $"La entrega a {NombreReceptor.Trim()} ha sido guardada en la base de datos cifrada local.",
                    "Finalizar");

                // Retornar a la lista principal de ruta
                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
