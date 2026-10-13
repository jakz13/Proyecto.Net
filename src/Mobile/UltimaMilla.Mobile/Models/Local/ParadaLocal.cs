using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Paradas")]
public class ParadaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid HojaDeRutaId { get; set; }

    public string Direccion { get; set; } = string.Empty;

    public int Orden { get; set; }

    public string Estado { get; set; } = string.Empty;
}
