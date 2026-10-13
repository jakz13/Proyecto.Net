using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Envios")]
public class EnvioLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid ParadaId { get; set; }

    public string Destinatario { get; set; } = string.Empty;

    public string Modalidad { get; set; } = string.Empty;
}
