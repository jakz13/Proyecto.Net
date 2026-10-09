using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class DiscrepanciaRecepcion : Entity
{
    public Guid EnvioId { get; private set; }
    public Guid? BultoId { get; private set; }
    public string TipoDiscrepancia { get; private set; } = string.Empty;
    public string Detalle { get; private set; } = string.Empty;
    public DateTime FechaDeteccion { get; private set; }
    public Guid UsuarioDeposito { get; private set; }

    protected DiscrepanciaRecepcion() { }

    public DiscrepanciaRecepcion(
        Guid id,
        Guid envioId,
        string tipoDiscrepancia,
        string detalle,
        Guid usuarioDeposito,
        Guid? bultoId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la discrepancia no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (usuarioDeposito == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario de depósito no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoDiscrepancia))
            throw new DomainValidationException("El tipo de discrepancia no puede ser vacío.");

        EnvioId = envioId;
        BultoId = bultoId;
        TipoDiscrepancia = tipoDiscrepancia.Trim();
        Detalle = detalle?.Trim() ?? string.Empty;
        UsuarioDeposito = usuarioDeposito;
        FechaDeteccion = DateTime.UtcNow;
    }
}
