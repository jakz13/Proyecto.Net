using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

/// <summary>
/// Posición del vehículo informada por el dispositivo del repartidor durante la jornada (RF-23).
/// Es la fuente del mapa del tablero en vivo (RF-29).
/// </summary>
public class PosicionVehiculo : Entity
{
    public Guid OperadorId { get; private set; }
    public Guid VehiculoId { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }
    public double? VelocidadKmh { get; private set; }
    public int? BateriaPct { get; private set; }
    /// <summary>Instante en que el dispositivo tomó la posición (no el de recepción), para ordenar correctamente lotes diferidos.</summary>
    public DateTime Timestamp { get; private set; }
    public string Origen { get; private set; } = string.Empty;

    protected PosicionVehiculo() { }

    public PosicionVehiculo(
        Guid id,
        Guid operadorId,
        Guid vehiculoId,
        double latitud,
        double longitud,
        string origen,
        Guid? hojaDeRutaId = null,
        Guid? repartidorId = null,
        double? velocidadKmh = null,
        int? bateriaPct = null,
        DateTime? tomadaEn = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la posición no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El operador de la posición no puede ser vacío.");

        if (vehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(origen))
            throw new DomainValidationException("El origen del dato de posición no puede ser vacío.");

        if (double.IsNaN(latitud) || latitud is < -90 or > 90)
            throw new DomainValidationException("La latitud debe estar entre -90 y 90 grados.");

        if (double.IsNaN(longitud) || longitud is < -180 or > 180)
            throw new DomainValidationException("La longitud debe estar entre -180 y 180 grados.");

        if (velocidadKmh is < 0)
            throw new DomainValidationException("La velocidad no puede ser negativa.");

        if (bateriaPct is < 0 or > 100)
            throw new DomainValidationException("El nivel de batería debe estar entre 0 y 100.");

        OperadorId = operadorId;
        VehiculoId = vehiculoId;
        RepartidorId = repartidorId;
        HojaDeRutaId = hojaDeRutaId;
        Latitud = latitud;
        Longitud = longitud;
        VelocidadKmh = velocidadKmh;
        BateriaPct = bateriaPct;
        Origen = origen.Trim();
        Timestamp = tomadaEn ?? DateTime.UtcNow;
    }
}
