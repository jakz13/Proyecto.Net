using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("EnviosAdmitidosDeposito")]
public class EnvioAdmitidoDepositoLocal
{
    [PrimaryKey]
    public Guid EnvioId { get; set; }

    [Indexed]
    public string CodigoSeguimiento { get; set; } = string.Empty;

    public string NombreDestinatario { get; set; } = string.Empty;

    public string DireccionDestino { get; set; } = string.Empty;

    public string? TelefonoDestinatario { get; set; }

    public string ModalidadServicio { get; set; } = string.Empty;

    public decimal PesoTotal { get; set; }

    public int CantidadBultos { get; set; }

    public string BultosJson { get; set; } = "[]";

    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;

    public bool RecepcionadoLocal { get; set; } = false;
}

