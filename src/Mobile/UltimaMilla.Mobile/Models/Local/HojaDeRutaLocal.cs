using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("HojasDeRuta")]
public class HojaDeRutaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public DateTime Fecha { get; set; }

    public string Estado { get; set; } = string.Empty;
}
