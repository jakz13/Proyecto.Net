using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
public partial class RegistroEntregaViewModel : ObservableObject
{
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

    public RegistroEntregaViewModel()
    {
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
                "Cámara Mock",
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
            await Task.Delay(1000);

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "¡Entrega Registrada!",
                    $"La entrega a {NombreReceptor.Trim()} ha sido guardada en la base de datos local cifrada.",
                    "Finalizar");

                // Retornar a la lista de entregas
                await Shell.Current.GoToAsync("..");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
