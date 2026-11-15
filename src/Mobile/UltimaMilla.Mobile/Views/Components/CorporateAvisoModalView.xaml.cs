using System.Windows.Input;

namespace UltimaMilla.Mobile.Views.Components;

/// <summary>Contrato para ViewModels que muestran alertas e informativos en modal corporativo.</summary>
public interface ICorporateAvisoModalHost
{
    bool MostrarModalAviso { get; }
    string TituloModalAviso { get; }
    string SubtituloModalAviso { get; }
    bool TieneSubtituloModalAviso { get; }
    string MensajeModalAviso { get; }
    string TextoBotonModalAviso { get; }
    ICommand CerrarModalAvisoCommand { get; }
}

public partial class CorporateAvisoModalView : ContentView
{
    public CorporateAvisoModalView()
    {
        InitializeComponent();
    }
}
