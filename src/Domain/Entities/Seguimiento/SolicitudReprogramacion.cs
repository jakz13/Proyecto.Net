using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class SolicitudReprogramacion : Entity
{
    public Guid EnvioId { get; private set; }
    public string FranjaHorariaSolicitada { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;
    public DateTime FechaSolicitud { get; private set; }
    public string? OrigenIp { get; private set; }

    protected SolicitudReprogramacion() { }

    public SolicitudReprogramacion(
        Guid id,
        Guid envioId,
        string franjaHorariaSolicitada,
        string? origenIp = null,
        string estado = "Pendiente") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la solicitud no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(franjaHorariaSolicitada))
            throw new DomainValidationException("La franja horaria solicitada no puede ser vacía.");

        EnvioId = envioId;
        FranjaHorariaSolicitada = franjaHorariaSolicitada.Trim();
        OrigenIp = origenIp?.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Trim();
        FechaSolicitud = DateTime.UtcNow;
    }

    public void Aprobar()
    {
        Estado = "Aprobada";
    }

    public void Rechazar(string? motivo = null)
    {
        Estado = "Rechazada";
    }
}
