using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Views.Entrega;

namespace UltimaMilla.Mobile.ViewModels.Ruta;

public partial class ParadaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Orden { get; init; }
    public string Direccion { get; init; } = string.Empty;
    public string Ciudad { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public int CantidadBultos { get; init; }
    public string Estado { get; init; } = "Pendiente"; // "Pendiente", "Entregado", "Fallido"
    public string HoraEstimada { get; init; } = string.Empty;

    // Helpers visuales para el renderizado del badge en XAML
    public Color EstadoBgColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#DCFCE7"),
        "Fallido" => Color.FromArgb("#FEE2E2"),
        _ => Color.FromArgb("#FEF3C7")
    };

    public Color EstadoTextColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#15803D"),
        "Fallido" => Color.FromArgb("#B91C1C"),
        _ => Color.FromArgb("#B45309")
    };

    public Color EstadoBorderColor => Estado switch
    {
        "Entregado" => Color.FromArgb("#86EFAC"),
        "Fallido" => Color.FromArgb("#FCA5A5"),
        _ => Color.FromArgb("#FCD34D")
    };

    public string EstadoTextoBadge => Estado switch
    {
        "Entregado" => "✓ Entregado",
        "Fallido" => "✕ Fallido",
        _ => "⏳ Pendiente"
    };

    public bool PuedeRegistrar => Estado == "Pendiente";
}

public partial class RutaDiaViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fechaHojaRuta;

    [ObservableProperty]
    private bool _isOffline = true;

    [ObservableProperty]
    private string _resumenProgreso = "1 de 3 completadas (33%)";

    public ObservableCollection<ParadaItemViewModel> Paradas { get; } = new();

    public RutaDiaViewModel()
    {
        _fechaHojaRuta = DateTime.Now.ToString("dd/MM/yyyy");
        CargarDatosMock();
    }

    private void CargarDatosMock()
    {
        Paradas.Clear();

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 1,
            Direccion = "Av. 18 de Julio 1450, Apto 402",
            Ciudad = "Centro, Montevideo",
            Destinatario = "Martín Rodríguez",
            CantidadBultos = 2,
            Estado = "Entregado",
            HoraEstimada = "09:30"
        });

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 2,
            Direccion = "Bvar. España 2678",
            Ciudad = "Pocitos, Montevideo",
            Destinatario = "Lucía Fernández",
            CantidadBultos = 1,
            Estado = "Pendiente",
            HoraEstimada = "10:15"
        });

        Paradas.Add(new ParadaItemViewModel
        {
            Orden = 3,
            Direccion = "Rambla Gandhi 456, Piso 8",
            Ciudad = "Punta Carretas, Montevideo",
            Destinatario = "Esteban Morales",
            CantidadBultos = 3,
            Estado = "Pendiente",
            HoraEstimada = "11:00"
        });
    }

    [RelayCommand]
    private async Task IrAParadaAsync(ParadaItemViewModel parada)
    {
        if (parada is null)
            return;

        // Navegación hacia la pantalla de Confirmar Entrega
        await Shell.Current.GoToAsync(nameof(RegistroEntregaPage), new Dictionary<string, object>
        {
            { "Parada", parada }
        });
    }

    [RelayCommand]
    private void AlternarModoRed()
    {
        IsOffline = !IsOffline;
    }
}
