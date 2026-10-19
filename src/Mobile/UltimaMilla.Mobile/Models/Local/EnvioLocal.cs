using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("Envios")]
public class EnvioLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid ParadaId { get; set; }

    public string CodigoSeguimiento { get; set; } = string.Empty;

    public string Destinatario { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Modalidad { get; set; } = string.Empty;

    public string Estado { get; set; } = string.Empty;
}
