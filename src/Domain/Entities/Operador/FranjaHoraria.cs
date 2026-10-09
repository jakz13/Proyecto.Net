using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class FranjaHoraria : Entity
{
    public Guid ZonaCoberturaId { get; private set; }
    public DayOfWeek DiaSemana { get; private set; }
    public TimeOnly HoraInicio { get; private set; }
    public TimeOnly HoraFin { get; private set; }
    public bool Activa { get; private set; }

    protected FranjaHoraria() { }

    public FranjaHoraria(
        Guid id,
        Guid zonaCoberturaId,
        DayOfWeek diaSemana,
        TimeOnly horaInicio,
        TimeOnly horaFin) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la franja horaria no puede ser vacío.");

        if (zonaCoberturaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        if (horaInicio >= horaFin)
            throw new DomainValidationException("La hora de inicio debe ser anterior a la hora de fin.");

        ZonaCoberturaId = zonaCoberturaId;
        DiaSemana = diaSemana;
        HoraInicio = horaInicio;
        HoraFin = horaFin;
        Activa = true;
    }

    public void Activar()
    {
        Activa = true;
    }

    public void Desactivar()
    {
        Activa = false;
    }

    public void ModificarHorario(TimeOnly horaInicio, TimeOnly horaFin)
    {
        if (horaInicio >= horaFin)
            throw new DomainValidationException("La hora de inicio debe ser anterior a la hora de fin.");

        HoraInicio = horaInicio;
        HoraFin = horaFin;
    }
}
