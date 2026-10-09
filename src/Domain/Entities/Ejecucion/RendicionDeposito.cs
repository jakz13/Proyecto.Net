using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class RendicionDeposito : Entity
{
    public Guid HojaDeRutaId { get; private set; }
    public Guid RepartidorId { get; private set; }
    public DateTime FechaHora { get; private set; }
    public int BultosEntregados { get; private set; }
    public int BultosDevueltos { get; private set; }
    public int BultosPendientes { get; private set; }
    public string? Observaciones { get; private set; }

    protected RendicionDeposito() { }

    public RendicionDeposito(
        Guid id,
        Guid hojaDeRutaId,
        Guid repartidorId,
        int bultosEntregados,
        int bultosDevueltos,
        int bultosPendientes,
        string? observaciones = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la rendición no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (repartidorId == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor no puede ser vacío.");

        if (bultosEntregados < 0 || bultosDevueltos < 0 || bultosPendientes < 0)
            throw new DomainValidationException("Las cantidades de bultos no pueden ser negativas.");

        HojaDeRutaId = hojaDeRutaId;
        RepartidorId = repartidorId;
        BultosEntregados = bultosEntregados;
        BultosDevueltos = bultosDevueltos;
        BultosPendientes = bultosPendientes;
        Observaciones = observaciones?.Trim();
        FechaHora = DateTime.UtcNow;
    }
}
