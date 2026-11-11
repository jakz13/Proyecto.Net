using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("HojasDeRuta")]
public class HojaDeRutaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public DateTime Fecha { get; set; } = DateTime.Today;

    public string Estado { get; set; } = "EnTransito";

    public string NumeroRuta { get; set; } = string.Empty;

    public int TotalParadas { get; set; }

    public DateTime FechaDescargaUtc { get; set; } = DateTime.UtcNow;
}

public record HojaDeRutaSelectorItem(
    Guid Id,
    string NumeroRuta,
    string Estado,
    int TotalParadas,
    string TituloSelector
);
