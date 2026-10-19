using SQLite;

namespace UltimaMilla.Mobile.Models.Local;

[Table("MotivosNoEntrega")]
public class MotivoNoEntregaLocal
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
