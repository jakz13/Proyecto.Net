using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class Vehiculo : Entity, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public string Patente { get; private set; } = string.Empty;
    public string Tipo { get; private set; } = string.Empty;
    public decimal CapacidadPeso { get; private set; }
    public decimal CapacidadVolumen { get; private set; }
    public bool Activo { get; private set; }

    protected Vehiculo() { }

    public Vehiculo(
        Guid id,
        Guid operadorId,
        string patente,
        string tipo,
        decimal capacidadPeso,
        decimal capacidadVolumen) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(patente))
            throw new DomainValidationException("La patente del vehículo no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(tipo))
            throw new DomainValidationException("El tipo de vehículo no puede ser vacío.");

        if (capacidadPeso <= 0)
            throw new DomainValidationException("La capacidad en peso debe ser mayor a cero.");

        if (capacidadVolumen <= 0)
            throw new DomainValidationException("La capacidad en volumen debe ser mayor a cero.");

        OperadorId = operadorId;
        Patente = patente.Trim().ToUpperInvariant();
        Tipo = tipo.Trim();
        CapacidadPeso = capacidadPeso;
        CapacidadVolumen = capacidadVolumen;
        Activo = true;
    }

    public void Activar()
    {
        Activo = true;
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void ActualizarCapacidades(decimal capacidadPeso, decimal capacidadVolumen)
    {
        if (capacidadPeso <= 0)
            throw new DomainValidationException("La capacidad en peso debe ser mayor a cero.");

        if (capacidadVolumen <= 0)
            throw new DomainValidationException("La capacidad en volumen debe ser mayor a cero.");

        CapacidadPeso = capacidadPeso;
        CapacidadVolumen = capacidadVolumen;
    }
}
