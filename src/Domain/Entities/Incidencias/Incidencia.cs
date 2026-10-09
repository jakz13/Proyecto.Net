using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Incidencias;

public class Incidencia : AggregateRoot, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public Guid EnvioId { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;
    public Guid? ResponsableUsuarioId { get; private set; }
    public DateTime FechaApertura { get; private set; }
    public DateTime? FechaCierre { get; private set; }

    protected Incidencia() { }

    public Incidencia(
        Guid id,
        Guid operadorId,
        Guid envioId,
        string tipo,
        string descripcion,
        string estado = "Abierta") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la incidencia no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipo))
            throw new DomainValidationException("El tipo de incidencia no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainValidationException("La descripción de la incidencia no puede ser vacía.");

        OperadorId = operadorId;
        EnvioId = envioId;
        Tipo = tipo.Trim();
        Descripcion = descripcion.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? "Abierta" : estado.Trim();
        FechaApertura = DateTime.UtcNow;
    }

    public void AsignarResponsable(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario responsable no puede ser vacío.");

        ResponsableUsuarioId = usuarioId;
    }

    public void Cerrar()
    {
        Estado = "Cerrada";
        FechaCierre = DateTime.UtcNow;
    }

    public void Reabrir()
    {
        Estado = "Reabierta";
        FechaCierre = null;
    }
}
