using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class ConflictoSincronizacion : Entity
{
    public string EntidadAfectada { get; private set; } = string.Empty;
    public Guid EntidadId { get; private set; }
    public string EstadoServidor { get; private set; } = string.Empty;
    public string EstadoDispositivo { get; private set; } = string.Empty;
    public string PoliticaAplicada { get; private set; } = string.Empty;
    public string Resolucion { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    protected ConflictoSincronizacion() { }

    public ConflictoSincronizacion(
        Guid id,
        string entidadAfectada,
        Guid entidadId,
        string estadoServidor,
        string estadoDispositivo,
        string politicaAplicada,
        string resolucion) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del conflicto no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(entidadAfectada))
            throw new DomainValidationException("La entidad afectada no puede ser vacía.");

        if (entidadId == Guid.Empty)
            throw new DomainValidationException("El identificador de la entidad afectada no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(resolucion))
            throw new DomainValidationException("La resolución del conflicto no puede ser vacía.");

        EntidadAfectada = entidadAfectada.Trim();
        EntidadId = entidadId;
        EstadoServidor = estadoServidor?.Trim() ?? string.Empty;
        EstadoDispositivo = estadoDispositivo?.Trim() ?? string.Empty;
        PoliticaAplicada = politicaAplicada?.Trim() ?? string.Empty;
        Resolucion = resolucion.Trim();
        Timestamp = DateTime.UtcNow;
    }
}
