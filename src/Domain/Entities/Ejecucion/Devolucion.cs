using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class Devolucion : Entity
{
    public Guid EnvioId { get; private set; }
    public Guid? MotivoDevolucionId { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public DateTime FechaInicio { get; private set; }
    public DateTime? FechaRendicion { get; private set; }

    protected Devolucion() { }

    public Devolucion(
        Guid id,
        Guid envioId,
        Guid? motivoDevolucionId = null,
        string estado = "EnDevolucion") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la devolución no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        EnvioId = envioId;
        MotivoDevolucionId = motivoDevolucionId;
        Estado = string.IsNullOrWhiteSpace(estado) ? "EnDevolucion" : estado.Trim();
        FechaInicio = DateTime.UtcNow;
    }

    public void Rendir()
    {
        Estado = "Devuelto";
        FechaRendicion = DateTime.UtcNow;
    }
}
