using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Planificacion;

public class HojaDeRuta : AggregateRoot, IMustHaveTenant, IHasConcurrencyToken
{
    public Guid OperadorId { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? VehiculoId { get; private set; }
    public DateOnly Fecha { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public DateTime? FechaLiberacion { get; private set; }
    public DateTime? FechaDespacho { get; private set; }
    public DateTime? FechaRendicion { get; private set; }
    public Guid SelloModificacion { get; private set; }

    private readonly List<Parada> _paradas = new();
    public IReadOnlyCollection<Parada> Paradas => _paradas.AsReadOnly();

    private readonly List<AsignacionEnvioRuta> _asignaciones = new();
    public IReadOnlyCollection<AsignacionEnvioRuta> Asignaciones => _asignaciones.AsReadOnly();

    private readonly List<EscaneoCargaVehiculo> _escaneosCarga = new();
    public IReadOnlyCollection<EscaneoCargaVehiculo> EscaneosCarga => _escaneosCarga.AsReadOnly();

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

    public void AgregarParada(Guid paradaId, Guid envioId, int orden, Guid direccionId, Guid? franjaHorariaId = null, string estado = "Pendiente")
    {
        var parada = new Parada(paradaId, Id, envioId, orden, direccionId, franjaHorariaId, estado);
        _paradas.Add(parada);
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>Orden que le corresponde a una parada agregada al final de la ruta.</summary>
    public int SiguienteOrden => _paradas.Count == 0 ? 1 : _paradas.Max(p => p.Orden) + 1;

    /// <summary>
    /// Retira un envío de la ruta (UC-10): su parada se cancela y su asignación deja de estar vigente.
    /// Solo es posible mientras la parada no esté cerrada.
    /// </summary>
    public void RetirarEnvio(Guid envioId)
    {
        if (Estado is not ("Liberada" or "EnCarga" or "Despachada" or "EnTransito"))
            throw new DomainValidationException($"No se pueden retirar envíos de una hoja de ruta en estado '{Estado}'.");

        var parada = _paradas.FirstOrDefault(p => p.EnvioId == envioId && p.Estado != "Cancelada")
            ?? throw new DomainValidationException("El envío no tiene una parada vigente en esta hoja de ruta.");

        if (parada.Estado is "Entregada" or "Fallida")
            throw new DomainValidationException($"La parada del envío ya fue cerrada ({parada.Estado}) y no se puede retirar.");

        parada.CancelarPorFaltante("Envío retirado de la ruta por el despachador.");
        _asignaciones.FirstOrDefault(a => a.EnvioId == envioId && a.Vigente)?.Desactivar();
        SelloModificacion = Guid.NewGuid();
    }

    public void RegistrarAsignacion(Guid envioId, Guid despachadorUsuarioId)
    {
        var asignacion = new AsignacionEnvioRuta(Guid.NewGuid(), envioId, Id, despachadorUsuarioId);
        _asignaciones.Add(asignacion);
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>
    /// Libera la hoja de ruta una vez armada: queda con sus paradas ordenadas y disponible para su descarga en la app del repartidor.
    /// </summary>
    public void Liberar()
    {
        if (Estado != "Borrador")
            throw new DomainValidationException($"Solo se puede liberar una hoja de ruta en estado 'Borrador' (estado actual: '{Estado}').");

        if (!RepartidorId.HasValue || !VehiculoId.HasValue)
            throw new DomainValidationException("No se puede liberar una hoja de ruta sin repartidor y vehículo asignados.");

        if (!_paradas.Any())
            throw new DomainValidationException("No se puede liberar una hoja de ruta sin paradas.");

        Estado = "Liberada";
        FechaLiberacion = DateTime.UtcNow;
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>
    /// Registra qué bultos se cargaron efectivamente en el vehículo (UC-18). Cada registro reemplaza al anterior,
    /// por lo que reenviar la misma carga es idempotente. Esa información es la que usa el operario para despachar.
    /// </summary>
    public void RegistrarCargaEscaneada(IEnumerable<Guid> bultosCargadosIds)
    {
        if (Estado is not ("Liberada" or "EnCarga"))
            throw new DomainValidationException($"No se puede registrar la carga de una hoja de ruta en estado '{Estado}'.");

        var ids = (bultosCargadosIds ?? Enumerable.Empty<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainValidationException("Debe registrar al menos un bulto cargado.");

        if (ids.Contains(Guid.Empty))
            throw new DomainValidationException("La carga contiene un identificador de bulto inválido.");

        _escaneosCarga.Clear();
        foreach (var bultoId in ids)
            _escaneosCarga.Add(new EscaneoCargaVehiculo(Guid.NewGuid(), Id, bultoId, "Cargado"));

        Estado = "EnCarga";
        SelloModificacion = Guid.NewGuid();
    }

    public bool BultoFueCargado(Guid bultoId) => _escaneosCarga.Any(e => e.BultoId == bultoId);

    /// <summary>
    /// Despacha la hoja de ruta (UC-09). Requiere que la carga del vehículo ya haya sido escaneada y sincronizada.
    /// </summary>
    public void Despachar()
    {
        if (!RepartidorId.HasValue || !VehiculoId.HasValue)
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin repartidor y vehículo asignados.");

        if (!_paradas.Any())
            throw new DomainValidationException("No se puede despachar una hoja de ruta sin paradas.");

        if (Estado is "Despachada" or "EnTransito" or "Finalizada")
            throw new DomainValidationException($"La hoja de ruta ya se encuentra en estado '{Estado}'.");

        if (Estado != "EnCarga")
            throw new DomainValidationException($"No se puede despachar una hoja de ruta en estado '{Estado}': la carga del vehículo debe estar escaneada y sincronizada.");

        Estado = "Despachada";
        FechaDespacho = DateTime.UtcNow;
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>
    /// Cierra la hoja de ruta tras la rendición del repartidor en depósito (UC-16). Libera sus asignaciones
    /// para que los envíos que vuelven a depósito puedan incluirse en otra ruta.
    /// </summary>
    public void Rendir()
    {
        if (Estado == "Rendida")
            throw new DomainValidationException("La hoja de ruta ya fue rendida.");

        if (Estado is not ("Despachada" or "EnTransito" or "Finalizada"))
            throw new DomainValidationException($"Solo se puede rendir una hoja de ruta despachada (estado actual: '{Estado}').");

        foreach (var parada in _paradas)
            parada.MarcarNoVisitada();

        foreach (var asignacion in _asignaciones.Where(a => a.Vigente))
            asignacion.Desactivar();

        Estado = "Rendida";
        FechaRendicion = DateTime.UtcNow;
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

    public Parada RegistrarEntregaExitosaEnParada(Guid paradaId)
    {
        var parada = _paradas.FirstOrDefault(p => p.Id == paradaId)
            ?? throw new DomainValidationException($"La parada con id '{paradaId}' no pertenece a esta hoja de ruta.");

        parada.RegistrarEntregaExitosa();
        SelloModificacion = Guid.NewGuid();

        VerificarYActualizarFinDeRuta();
        return parada;
    }

    public Parada RegistrarIntentoFallidoEnParada(Guid paradaId)
    {
        var parada = _paradas.FirstOrDefault(p => p.Id == paradaId)
            ?? throw new DomainValidationException($"La parada con id '{paradaId}' no pertenece a esta hoja de ruta.");

        parada.RegistrarIntentoFallido();
        SelloModificacion = Guid.NewGuid();

        VerificarYActualizarFinDeRuta();
        return parada;
    }

    private void VerificarYActualizarFinDeRuta()
    {
        bool todasCompletadas = _paradas.Count > 0 && _paradas.All(p => p.Estado is "Entregada" or "Fallida" or "Cancelada");
        if (todasCompletadas)
        {
            Estado = "Finalizada";
        }
    }

    public void Finalizar()
    {
        Estado = "Finalizada";
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>
    /// Restablece la hoja de ruta a estado 'Despachada' y sus paradas a 'Pendiente' para reiniciar demostraciones (Demo Reset).
    /// </summary>
    public void ReiniciarParaDemo()
    {
        Estado = "Despachada";
        FechaDespacho = DateTime.UtcNow;
        FechaRendicion = null;
        SelloModificacion = Guid.NewGuid();
        foreach (var parada in _paradas)
        {
            parada.CambiarEstado("Pendiente");
        }
    }
}
