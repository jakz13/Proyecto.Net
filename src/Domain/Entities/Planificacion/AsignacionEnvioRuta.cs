using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Planificacion;

public class AsignacionEnvioRuta : Entity, IHasConcurrencyToken
{
    public Guid EnvioId { get; private set; }
    public Guid HojaDeRutaId { get; private set; }
    public DateTime FechaAsignacion { get; private set; }
    public Guid DespachadorUsuarioId { get; private set; }
    public bool Vigente { get; private set; }
    public Guid SelloModificacion { get; private set; }

    protected AsignacionEnvioRuta() { }

    public AsignacionEnvioRuta(
        Guid id,
        Guid envioId,
        Guid hojaDeRutaId,
        Guid despachadorUsuarioId) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la asignación no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (despachadorUsuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del despachador no puede ser vacío.");

        EnvioId = envioId;
        HojaDeRutaId = hojaDeRutaId;
        DespachadorUsuarioId = despachadorUsuarioId;
        FechaAsignacion = DateTime.UtcNow;
        Vigente = true;
        SelloModificacion = Guid.NewGuid();
    }

    public void Desactivar()
    {
        Vigente = false;
        SelloModificacion = Guid.NewGuid();
    }
}
