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

    public void IniciarCarga()
    {
        if (!RepartidorId.HasValue || !VehiculoId.HasValue)
            throw new DomainValidationException("No se puede iniciar la carga sin repartidor y vehículo asignados.");

        if (!_paradas.Any())
            throw new DomainValidationException("No se puede iniciar la carga de una hoja de ruta sin paradas planificadas.");

        if (Estado == "Despachada" || Estado == "EnTransito" || Estado == "Finalizada")
            throw new DomainValidationException($"No se puede iniciar la carga en una hoja de ruta en estado '{Estado}'.");

        Estado = "EnCarga";
        SelloModificacion = Guid.NewGuid();
    }

    public void Despachar()
    {
        DespacharConCarga(Enumerable.Empty<string>());
    }

    public void DespacharConCarga(IEnumerable<string> bultosCargadosEfectivos, string? observaciones = null)
    {
        if (!RepartidorId.HasValue || !VehiculoId.HasValue)
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin repartidor y vehículo asignados.");

        if (!_paradas.Any())
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin paradas.");

        if (Estado == "Despachada" || Estado == "EnTransito" || Estado == "Finalizada")
            throw new DomainValidationException($"La hoja de ruta ya se encuentra en estado '{Estado}'.");

        Estado = "Despachada";
        FechaDespacho = DateTime.UtcNow;
        SelloModificacion = Guid.NewGuid();
    }

    public Parada IniciarNavegacionHaciaParada(Guid paradaId)
    {
        if (Estado != "EnTransito" && Estado != "Despachada")
            throw new DomainValidationException($"Precondición no cumplida: El vehículo debe estar cargado y la ruta en tránsito (Estado actual: '{Estado}').");

        var parada = _paradas.FirstOrDefault(p => p.Id == paradaId);
        if (parada is null)
            throw new DomainValidationException($"La parada con id '{paradaId}' no pertenece a esta hoja de ruta.");

        // Si alguna otra parada estaba en camino, se regresa a pendiente (reordenamiento en calle)
        foreach (var p in _paradas.Where(p => p.Id != paradaId && p.Estado == "EnCamino"))
        {
            p.CambiarEstado("Pendiente");
        }

        parada.IniciarNavegacion();
        SelloModificacion = Guid.NewGuid();
        return parada;
    }

    public Parada RegistrarArriboAParada(Guid paradaId)
    {
        if (Estado != "EnTransito" && Estado != "Despachada")
            throw new DomainValidationException($"No se puede registrar arribo si la ruta no está en tránsito.");

        var parada = _paradas.FirstOrDefault(p => p.Id == paradaId);
        if (parada is null)
            throw new DomainValidationException($"La parada con id '{paradaId}' no pertenece a esta hoja de ruta.");

        parada.RegistrarArribo();
        SelloModificacion = Guid.NewGuid();
        return parada;
    }

    public void DesasignarEnvioFaltante(Guid envioId)
    {
        var asignacion = _asignaciones.FirstOrDefault(a => a.EnvioId == envioId && a.Vigente);
        asignacion?.Desactivar();

        var parada = _paradas.FirstOrDefault(p => p.EnvioId == envioId);
        if (parada is not null)
        {
            parada.CancelarPorFaltante("Bulto faltante durante escaneo de carga en depósito.");
        }
        SelloModificacion = Guid.NewGuid();
    }

    public void Finalizar()
    {
        Estado = "Finalizada";
        SelloModificacion = Guid.NewGuid();
    }
}
