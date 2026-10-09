using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class PosicionVehiculo : Entity
{
    public Guid VehiculoId { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Origen { get; private set; } = string.Empty;

    protected PosicionVehiculo() { }

    public PosicionVehiculo(
        Guid id,
        Guid vehiculoId,
        double latitud,
        double longitud,
        string origen,
        Guid? hojaDeRutaId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la posición no puede ser vacío.");

        if (vehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(origen))
            throw new DomainValidationException("El origen del dato de posición no puede ser vacío.");

        VehiculoId = vehiculoId;
        HojaDeRutaId = hojaDeRutaId;
        Latitud = latitud;
        Longitud = longitud;
        Origen = origen.Trim();
        Timestamp = DateTime.UtcNow;
    }
}
