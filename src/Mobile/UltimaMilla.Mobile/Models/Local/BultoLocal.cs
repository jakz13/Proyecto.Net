using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Bultos")]
public class BultoLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid EnvioId { get; set; }

    public string Identificacion { get; set; } = string.Empty;

    public decimal Peso { get; set; }
}
