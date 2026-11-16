using System.Collections.ObjectModel;
using System.Windows.Input;
using UltimaMilla.Mobile.Models.Local;

namespace UltimaMilla.Mobile.Views.Components;

/// <summary>Contrato para ViewModels que exponen el modal corporativo selector de hoja de ruta.</summary>
public interface ISelectorRutaModalHost
{
    bool MostrarModalSelectorRuta { get; }
    ObservableCollection<HojaDeRutaSelectorItem> HojasDeRutaDisponibles { get; }
    HojaDeRutaSelectorItem? HojaDeRutaSeleccionada { get; }
    ICommand AbrirSelectorRutaCommand { get; }
    ICommand CerrarSelectorRutaCommand { get; }
    ICommand SeleccionarRutaDelModalCommand { get; }
}

public partial class SelectorRutaModalView : ContentView
{
    public SelectorRutaModalView()
    {
        InitializeComponent();
    }
}
