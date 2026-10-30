using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("EventosGeocerca")]
public class EventoGeocercaLocal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public Guid HojaDeRutaId { get; set; }

    [Indexed]
    public Guid ParadaId { get; set; }

    [Indexed]
    public Guid EnvioId { get; set; }

    public string CodigoSeguimiento { get; set; } = string.Empty;

    /// <summary>
    /// "LLEGADA_GEOCERCA" | "SALIDA_GEOCERCA"
    /// </summary>
    public string TipoEvento { get; set; } = string.Empty;

    public double Latitud { get; set; }

    public double Longitud { get; set; }

    public double DistanciaMetros { get; set; }

    public DateTime TimestampUtc { get; set; }

    public int? TiempoPermanenciaSegundos { get; set; }

    public bool Sincronizado { get; set; }
}

