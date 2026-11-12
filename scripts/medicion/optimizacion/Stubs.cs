namespace Microsoft.Maui.Devices.Sensors { public class Marcador { } }
namespace UltimaMilla.Mobile.ViewModels.Ruta
{
    // Solo los campos que usa el servicio de optimizacion
    public class ParadaItemViewModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string Estado { get; set; } = "Pendiente";
        public string CodigoSeguimiento { get; set; } = "";
        public string VentanaHoraria { get; set; } = "09:00 - 13:00";
        public double Latitud { get; set; }
        public double Longitud { get; set; }
    }
}
