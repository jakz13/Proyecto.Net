using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Bultos")]
public class BultoLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid EnvioId { get; set; }

    [Indexed]
    public Guid ParadaId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Identificacion { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public decimal Peso { get; set; }

    public bool EsFragil { get; set; }
}
