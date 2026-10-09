using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Planificacion;

public class HojaDeRuta : AggregateRoot, IMustHaveTenant, IHasConcurrencyToken
{
    public Guid OperadorId { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? VehiculoId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public DateTime? FechaDespacho { get; private set; }
    public Guid SelloModificacion { get; private set; }

    private readonly List<Parada> _paradas = new();
    public IReadOnlyCollection<Parada> Paradas => _paradas.AsReadOnly();

    private readonly List<AsignacionEnvioRuta> _asignaciones = new();
    public IReadOnlyCollection<AsignacionEnvioRuta> Asignaciones => _asignaciones.AsReadOnly();

    protected HojaDeRuta() { }

    public HojaDeRuta(
        Guid id,
        Guid operadorId,
        DateOnly fecha,
        string estado = "Borrador") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        OperadorId = operadorId;
        Fecha = fecha;
        Estado = string.IsNullOrWhiteSpace(estado) ? "Borrador" : estado.Trim();
        SelloModificacion = Guid.NewGuid();
    }

    public void AsignarRecursos(Guid repartidorId, Guid vehiculoId)
    {
        if (repartidorId == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor no puede ser vacío.");

        if (vehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo no puede ser vacío.");

        RepartidorId = repartidorId;
        VehiculoId = vehiculoId;
        SelloModificacion = Guid.NewGuid();
    }

    public void AgregarParada(Guid envioId, int orden, Guid direccionId, Guid? franjaHorariaId = null)
    {
        var parada = new Parada(Guid.NewGuid(), Id, envioId, orden, direccionId, franjaHorariaId);
        _paradas.Add(parada);
        SelloModificacion = Guid.NewGuid();
    }

    public void RegistrarAsignacion(Guid envioId, Guid despachadorUsuarioId)
    {
        var asignacion = new AsignacionEnvioRuta(Guid.NewGuid(), envioId, Id, despachadorUsuarioId);
        _asignaciones.Add(asignacion);
        SelloModificacion = Guid.NewGuid();
    }

    public void Despachar()
    {
        if (!RepartidorId.HasValue || !VehiculoId.HasValue)
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin repartidor y vehículo asignados.");

        if (!_paradas.Any())
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin paradas.");

        Estado = "Despachada";
        FechaDespacho = DateTime.UtcNow;
        SelloModificacion = Guid.NewGuid();
    }

    public void Finalizar()
    {
        Estado = "Finalizada";
        SelloModificacion = Guid.NewGuid();
    }
}
