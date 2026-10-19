using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Paradas")]
public class ParadaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid HojaDeRutaId { get; set; }

    [Indexed]
    public Guid EnvioId { get; set; }

    public int Orden { get; set; }

    public string Estado { get; set; } = "Pendiente"; // "Pendiente", "EnCamino", "EnSitio", "Entregado", "Fallido", "Reprogramado", "EnDevolucion"

    public string CodigoSeguimiento { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Ciudad { get; set; } = string.Empty;

    public string DestinatarioNombre { get; set; } = string.Empty;

    public string? DestinatarioTelefono { get; set; }

    public string? DestinatarioEmail { get; set; }

    public string? NotasComercio { get; set; }

    public double Latitud { get; set; }

    public double Longitud { get; set; }

    public string HoraEstimada { get; set; } = string.Empty;

    public string? VentanaHoraria { get; set; }

    public int CantidadBultos { get; set; }

    public bool EsParadaActual { get; set; }
}
