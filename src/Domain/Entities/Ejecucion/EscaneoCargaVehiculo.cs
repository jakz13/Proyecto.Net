using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class EscaneoCargaVehiculo : Entity
{
    public Guid HojaDeRutaId { get; private set; }
    public Guid BultoId { get; private set; }
    public string Resultado { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    protected EscaneoCargaVehiculo() { }

    public EscaneoCargaVehiculo(
        Guid id,
        Guid hojaDeRutaId,
        Guid bultoId,
        string resultado) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del escaneo no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (bultoId == Guid.Empty)
            throw new DomainValidationException("El identificador del bulto no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(resultado))
            throw new DomainValidationException("El resultado del escaneo no puede ser vacío.");

        HojaDeRutaId = hojaDeRutaId;
        BultoId = bultoId;
        Resultado = resultado.Trim();
        Timestamp = DateTime.UtcNow;
    }
}
