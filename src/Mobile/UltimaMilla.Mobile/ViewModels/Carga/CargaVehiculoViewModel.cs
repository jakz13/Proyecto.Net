using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaMilla.Mobile.ViewModels.Carga;

public partial class CargaVehiculoViewModel : ObservableObject
{
    [ObservableProperty]
    private string _titulo = "Control de Carga de Vehículo";

    [ObservableProperty]
    private string _patente = "SBA-4321";

    [ObservableProperty]
    private int _bultosCargados = 6;

    [ObservableProperty]
    private int _bultosTotales = 6;
}
