using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("OperacionesOffline")]
public class OperacionOfflineLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public Guid OperacionId { get; set; } = Guid.NewGuid();

    public string TipoOperacion { get; set; } = string.Empty; // e.g. "REGISTRAR_ENTREGA", "FALLO_ENTREGA", "TELEMETRIA_GPS"

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime FechaCreacionUtc { get; set; } = DateTime.UtcNow;

    public int Reintentos { get; set; } = 0;

    public bool Sincronizado { get; set; } = false;

    public string? UltimoError { get; set; }
}
