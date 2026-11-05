using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class Envio : AggregateRoot, IMustHaveTenant, IMustHaveComercioTenant, IHasConcurrencyToken
{
    public Guid OperadorId { get; private set; }
    public Guid ComercioOperadorId { get; private set; }
    public Guid DestinatarioId { get; private set; }
    public Guid? DireccionEntregaId { get; private set; }
    public string ModalidadServicio { get; private set; } = string.Empty;
    public Guid? FranjaHorariaComprometidaId { get; private set; }
    public EstadoEnvio EstadoActual { get; private set; }
    public decimal TarifaCalculada { get; private set; }
    public Guid? ReglaTarifaAplicadaId { get; private set; }
    public string CodigoSeguimientoPublico { get; private set; } = string.Empty;
    public Guid? LoteImportacionId { get; private set; }
    /// <summary>Identificador del envío en el sistema del comercio; permite reintentar altas sin duplicar.</summary>
    public string? ReferenciaExterna { get; private set; }
    public DateTime FechaAlta { get; private set; }
    public Guid SelloModificacion { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }

    // Propiedades de compatibilidad calculadas
    public Guid ComercioId => ComercioOperadorId;
    public string DireccionDestino { get; private set; } = string.Empty;
    public string NombreDestinatario { get; private set; } = string.Empty;
    public DateTime FechaCreacion => FechaAlta;
    public EstadoEnvio Estado => EstadoActual;

    private readonly List<Bulto> _bultos = new();
    public IReadOnlyCollection<Bulto> Bultos => _bultos.AsReadOnly();

    private readonly List<EventoEnvio> _eventos = new();
    public IReadOnlyCollection<EventoEnvio> Eventos => _eventos.AsReadOnly();

    private readonly List<DiscrepanciaRecepcion> _discrepancias = new();
    public IReadOnlyCollection<DiscrepanciaRecepcion> Discrepancias => _discrepancias.AsReadOnly();

    private readonly List<IntentoEntrega> _intentosEntrega = new();
    public IReadOnlyCollection<IntentoEntrega> IntentosEntrega => _intentosEntrega.AsReadOnly();
    public int CantidadIntentos => _intentosEntrega.Count;

    protected Envio() { }

    public Envio(
        Guid id,
        Guid comercioOperadorId,
        Guid destinatarioId,
        string modalidadServicio,
        string codigoSeguimientoPublico,
        Guid? direccionEntregaId = null,
        Guid? franjaHorariaComprometidaId = null,
        Guid? loteImportacionId = null,
        string? direccionDestino = null,
        string? nombreDestinatario = null,
        Guid? operadorId = null,
        string? referenciaExterna = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (referenciaExterna is { Length: > 100 })
            throw new DomainValidationException("La referencia externa no puede superar los 100 caracteres.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (destinatarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del destinatario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(modalidadServicio))
            throw new DomainValidationException("La modalidad de servicio no puede ser vacía.");

        ValidacionTexto.LargoMaximo(modalidadServicio, 50, "La modalidad de servicio");
        ValidacionTexto.LargoMaximo(codigoSeguimientoPublico, 100, "El código de seguimiento");
        ValidacionTexto.LargoMaximo(direccionDestino, 300, "La dirección de destino");
        ValidacionTexto.LargoMaximo(nombreDestinatario, 200, "El nombre del destinatario");

        if (string.IsNullOrWhiteSpace(codigoSeguimientoPublico))
            throw new DomainValidationException("El código de seguimiento no puede ser vacío.");

        ComercioOperadorId = comercioOperadorId;
        DestinatarioId = destinatarioId;
        ModalidadServicio = modalidadServicio.Trim();
        CodigoSeguimientoPublico = codigoSeguimientoPublico.Trim().ToUpperInvariant();
        DireccionEntregaId = direccionEntregaId;
        FranjaHorariaComprometidaId = franjaHorariaComprometidaId;
        LoteImportacionId = loteImportacionId;
        ReferenciaExterna = string.IsNullOrWhiteSpace(referenciaExterna) ? null : referenciaExterna.Trim();
        DireccionDestino = direccionDestino?.Trim() ?? string.Empty;
        NombreDestinatario = nombreDestinatario?.Trim() ?? string.Empty;
        OperadorId = operadorId ?? Guid.Empty;
        FechaAlta = DateTime.UtcNow;
        EstadoActual = EstadoEnvio.Admitido;
        SelloModificacion = Guid.NewGuid();

        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(),
            Id,
            "AltaEnvio",
            null,
            EstadoEnvio.Admitido,
            "Sistema"));
    }

    public Envio(Guid id, Guid comercioId, string direccionDestino, string nombreDestinatario, string? codigoSeguimientoPublico = null, Guid? operadorId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (comercioId == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(direccionDestino))
            throw new DomainValidationException("La dirección de destino no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(nombreDestinatario))
            throw new DomainValidationException("El nombre del destinatario no puede ser vacío.");

        ComercioOperadorId = comercioId;
        DireccionDestino = direccionDestino.Trim();
        NombreDestinatario = nombreDestinatario.Trim();
        DestinatarioId = Guid.NewGuid();
        ModalidadServicio = "Estandar";
        OperadorId = operadorId ?? Guid.Empty;
        CodigoSeguimientoPublico = string.IsNullOrWhiteSpace(codigoSeguimientoPublico)
            ? $"TRK-{id.ToString()[..8].ToUpperInvariant()}"
            : codigoSeguimientoPublico.Trim().ToUpperInvariant();
        FechaAlta = DateTime.UtcNow;
        EstadoActual = EstadoEnvio.Admitido;
        SelloModificacion = Guid.NewGuid();

        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(),
            Id,
            "AltaEnvio",
            null,
            EstadoEnvio.Admitido,
            "Sistema"));
    }

    // Los bultos faltantes en recepción no viajan, y cuando el envío está entregado parcialmente solo cuentan los bultos pendientes
    public decimal PesoTotal =>
        EstadoActual == EstadoEnvio.EntregadoParcialmente
            ? _bultos.Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion && !b.EstaEntregado).Sum(b => b.Peso)
            : _bultos.Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion).Sum(b => b.Peso);

    public decimal VolumenTotal =>
        EstadoActual == EstadoEnvio.EntregadoParcialmente
            ? _bultos.Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion && !b.EstaEntregado).Sum(b => b.Volumen)
            : _bultos.Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion).Sum(b => b.Volumen);

    public void AgregarBulto(string identificacion, decimal peso, decimal alto = 20, decimal ancho = 20, decimal profundo = 20, Guid? bultoId = null)
    {
        if (EstadoActual != EstadoEnvio.Admitido)
            throw new DomainValidationException($"No se pueden agregar bultos a un envío en estado '{EstadoActual}'.");

        if (_bultos.Any(b => b.Identificacion.Equals(identificacion?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainValidationException($"El envío ya contiene un bulto con la identificación '{identificacion}'.");

        var bulto = new Bulto(bultoId ?? Guid.NewGuid(), Id, identificacion, peso, alto, ancho, profundo);
        _bultos.Add(bulto);
        SelloModificacion = Guid.NewGuid();
    }

    public void AsignarRuta(Guid hojaDeRutaId, Guid? responsableUsuarioId = null)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        TransicionarEstado(
            EstadoEnvio.AsignadoARuta,
            "AsignacionRuta",
            responsableUsuarioId,
            payload: $"Hoja de ruta {hojaDeRutaId}.");
        HojaDeRutaId = hojaDeRutaId;
    }

    /// <summary>Prioridad más alta entre los bultos clasificados del envío.</summary>
    public PrioridadBulto? PrioridadMaxima
    {
        get
        {
            var prioridades = _bultos.Where(b => b.Prioridad.HasValue).Select(b => b.Prioridad!.Value).ToList();
            return prioridades.Count == 0 ? null : prioridades.Max();
        }
    }

    /// <summary>Zona operativa con la que se clasificaron los bultos del envío (la menor, para que el orden sea determinístico).</summary>
    public Guid? ZonaClasificadaId => _bultos.Where(b => b.ZonaClasificadaId.HasValue).Select(b => b.ZonaClasificadaId!.Value).OrderBy(z => z).Cast<Guid?>().FirstOrDefault();

    /// <summary>Devuelve el motivo por el cual el envío no puede incluirse en una hoja de ruta, o null si es asignable (RF-34).</summary>
    public string? ObtenerMotivoNoAsignable()
    {
        if (EstadoActual == EstadoEnvio.AsignadoARuta)
            return "El envío ya fue asignado a otra hoja de ruta.";

        if (EstadoActual is not (EstadoEnvio.EnDeposito or EstadoEnvio.EntregadoParcialmente))
            return $"El envío está en estado '{EstadoActual}' y solo se pueden asignar envíos 'EnDeposito' o 'EntregadoParcialmente'.";

        if (EstadoActual == EstadoEnvio.EntregadoParcialmente && !_bultos.Any(b => b.EstaDisponibleParaRuta))
            return "El envío entregado parcialmente no tiene bultos pendientes disponibles para una nueva hoja de ruta.";

        if (!EstaClasificado)
            return "El envío tiene bultos sin clasificar en depósito.";

        if (!DireccionEntregaId.HasValue)
            return "El envío no tiene dirección de entrega.";

        return null;
    }

    public void DespacharEnRuta(Guid hojaDeRutaId, Guid? responsableUsuarioId = null, string? observaciones = null)
    {
        if (EstadoActual != EstadoEnvio.AsignadoARuta)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede despachar porque su estado es {EstadoActual} (se requiere estado AsignadoARuta).");

        HojaDeRutaId = hojaDeRutaId;
        foreach (var bulto in _bultos.Where(b => b.EstaDisponibleParaRuta))
        {
            bulto.MarcarCargadoEnVehiculo();
        }

        TransicionarEstado(
            EstadoEnvio.EnTransito,
            "DespachoRuta",
            responsableUsuarioId,
            "Backoffice",
            null,
            null,
            observaciones);
    }

    /// <summary>
    /// Retira el envío de la hoja de ruta cuando sus bultos no se cargaron (UC-09): vuelve a depósito, conserva su
    /// clasificación y queda disponible para otra hoja de ruta.
    /// </summary>
    public void DesasignarDeRutaPorFaltante(string motivo, Guid? responsableUsuarioId = null)
    {
        if (EstadoActual != EstadoEnvio.AsignadoARuta)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede desasignar porque su estado es {EstadoActual}.");

        HojaDeRutaId = null;

        TransicionarEstado(
            EstadoEnvio.EnDeposito,
            "BultoFaltanteEnCarga",
            responsableUsuarioId,
            "Backoffice",
            null,
            null,
            motivo);
    }

    /// <summary>
    /// Registra que el envío volvió en el vehículo sin que se intentara entregarlo (UC-16): queda "no entregado"
    /// con un motivo interno del catálogo y vuelve a depósito, conservando su clasificación.
    /// </summary>
    public void RegistrarRetornoSinIntento(MotivoNoEntrega motivoInterno, Guid responsableUsuarioId, string? observaciones = null)
    {
        ArgumentNullException.ThrowIfNull(motivoInterno);

        if (EstadoActual != EstadoEnvio.EnTransito)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede registrar como retornado sin intento porque su estado es '{EstadoActual}'.");

        if (!motivoInterno.Activo || !motivoInterno.EsInterno)
            throw new DomainValidationException($"El motivo '{motivoInterno.Codigo}' no es un motivo interno activo.");

        TransicionarEstado(
            EstadoEnvio.NoEntregado,
            "RetornoSinIntento",
            responsableUsuarioId,
            "Backoffice",
            payload: $"Motivo: {motivoInterno.Codigo} - {motivoInterno.Descripcion}. {observaciones?.Trim()}".Trim());

        VolverADeposito("RetornoADeposito", responsableUsuarioId, "Rendición del repartidor.");
    }

    /// <summary>Devuelve a depósito un envío reprogramado o entregado parcialmente cuando el repartidor rinde la ruta (UC-16, RF-34).</summary>
    public void ReintegrarADepositoPorRendicion(Guid responsableUsuarioId)
    {
        if (EstadoActual == EstadoEnvio.EntregadoParcialmente)
        {
            HojaDeRutaId = null;
            foreach (var bulto in _bultos.Where(b => b.EstadoBulto is MaquinaEstadosBulto.CargadoEnVehiculo or MaquinaEstadosBulto.NoEntregado or MaquinaEstadosBulto.PendienteReenvio))
                bulto.MarcarRecibido();

            RegistrarEventoSinCambioDeEstado(
                "ReintegroADeposito",
                responsableUsuarioId,
                "Backoffice",
                "Rendición del repartidor: bultos pendientes de entrega parcial reintegrados a depósito.");
            return;
        }

        if (EstadoActual is not (EstadoEnvio.Reprogramado or EstadoEnvio.NoEntregado))
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede reintegrar a depósito porque su estado es '{EstadoActual}'.");

        VolverADeposito("ReintegroADeposito", responsableUsuarioId, "Rendición del repartidor: envío reprogramado.");
    }

    /// <summary>
    /// El repartidor retira los bultos de un envío en devolución y los lleva de vuelta al depósito (UC-22).
    /// No cambia el estado del envío. Devuelve false cuando ya estaba registrado (operación idempotente).
    /// </summary>
    public bool RegistrarRetiroParaDevolucion(Guid? responsableUsuarioId, double? latitud, double? longitud, DateTime fechaHoraDispositivoUtc, string? observaciones = null)
    {
        if (EstadoActual != EstadoEnvio.EnDevolucion)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no está en devolución (estado '{EstadoActual}').");

        var porRetirar = _bultos.Where(b => b.EstadoBulto == MaquinaEstadosBulto.CargadoEnVehiculo).ToList();
        if (porRetirar.Count == 0)
            return false;

        foreach (var bulto in porRetirar)
            bulto.MarcarEnRetornoADeposito();

        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(), Id, "RetiroParaDevolucion", EstadoActual, EstadoActual, "MobileApp",
            responsableUsuarioId, latitud, longitud,
            $"Bultos retirados para devolver al depósito. DispositivoUtc: {fechaHoraDispositivoUtc:O}. {observaciones?.Trim()}".Trim()));
        SelloModificacion = Guid.NewGuid();
        return true;
    }

    /// <summary>El comercio confirma que recibió el bulto devuelto: la devolución queda cerrada (UC-32).</summary>
    public void ConfirmarDevolucionRecibida(Guid? responsableUsuarioId)
    {
        if (EstadoActual != EstadoEnvio.EnDevolucion)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no está en devolución (estado '{EstadoActual}').");

        foreach (var bulto in _bultos.Where(b => b.EstadoBulto is MaquinaEstadosBulto.EnRetornoADeposito or MaquinaEstadosBulto.Recibido or MaquinaEstadosBulto.Danado or MaquinaEstadosBulto.NoEntregado))
            bulto.MarcarDevuelto();

        TransicionarEstado(
            EstadoEnvio.Devuelto,
            "DevolucionConfirmadaPorComercio",
            responsableUsuarioId,
            "PortalComercio",
            payload: "El comercio confirmó la recepción del bulto devuelto.");
    }

    /// <summary>Retira de la ruta un envío que todavía no salió: vuelve directamente a depósito (UC-10).</summary>
    public void RetirarDeRuta(string motivo, Guid responsableUsuarioId)
    {
        if (EstadoActual != EstadoEnvio.AsignadoARuta)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede retirar de la ruta porque su estado es '{EstadoActual}'.");

        HojaDeRutaId = null;
        TransicionarEstado(EstadoEnvio.EnDeposito, "RetiroDeRuta", responsableUsuarioId, "Backoffice", payload: motivo);
    }

    /// <summary>
    /// Retira un envío que ya está en la calle (UC-10): queda "no entregado" con un motivo interno y vuelve a
    /// depósito cuando el repartidor rinda la ruta (UC-16).
    /// </summary>
    public void RegistrarRetiroEnCalle(MotivoNoEntrega motivoInterno, Guid responsableUsuarioId, string? observaciones = null)
    {
        ArgumentNullException.ThrowIfNull(motivoInterno);

        if (EstadoActual != EstadoEnvio.EnTransito)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no está en tránsito (estado '{EstadoActual}').");

        if (!motivoInterno.Activo || !motivoInterno.EsInterno)
            throw new DomainValidationException($"El motivo '{motivoInterno.Codigo}' no es un motivo interno activo.");

        TransicionarEstado(
            EstadoEnvio.NoEntregado,
            "RetiroEnRuta",
            responsableUsuarioId,
            "Backoffice",
            payload: $"Motivo: {motivoInterno.Codigo} - {motivoInterno.Descripcion}. {observaciones?.Trim()}".Trim());
    }

    /// <summary>Cambia la franja horaria comprometida de un envío que está en depósito (UC-10).</summary>
    public void ReprogramarFranja(Guid franjaHorariaId, Guid responsableUsuarioId)
    {
        if (franjaHorariaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la franja horaria no puede ser vacío.");

        if (EstadoActual is not (EstadoEnvio.EnDeposito or EstadoEnvio.EntregadoParcialmente))
            throw new DomainValidationException($"Solo se puede reprogramar la franja de un envío en depósito (estado '{EstadoActual}').");

        var anterior = FranjaHorariaComprometidaId;
        FranjaHorariaComprometidaId = franjaHorariaId;
        RegistrarEventoSinCambioDeEstado(
            "ReprogramacionFranja", responsableUsuarioId, "Backoffice", $"Franja anterior: {anterior?.ToString() ?? "ninguna"}. Franja nueva: {franjaHorariaId}.");
    }

    /// <summary>
    /// El destinatario eligió otra franja desde el seguimiento público. Solo es posible mientras el envío no esté
    /// asignado a una hoja de ruta ni en la calle; no altera el estado.
    /// </summary>
    public void ReprogramarFranjaPorDestinatario(Guid franjaHorariaId)
    {
        if (franjaHorariaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la franja horaria no puede ser vacío.");

        if (EstadoActual is not (EstadoEnvio.Admitido or EstadoEnvio.EnDeposito or EstadoEnvio.Reprogramado or EstadoEnvio.EntregadoParcialmente))
            throw new DomainValidationException($"No se puede reprogramar un envío en estado '{EstadoActual}'.");

        var anterior = FranjaHorariaComprometidaId;
        FranjaHorariaComprometidaId = franjaHorariaId;
        RegistrarEventoSinCambioDeEstado(
            "ReprogramacionSolicitadaPorDestinatario", null, "SeguimientoPublico", $"Franja anterior: {anterior?.ToString() ?? "ninguna"}. Franja nueva: {franjaHorariaId}.");
    }

    private void VolverADeposito(string tipoEvento, Guid responsableUsuarioId, string detalle)
    {
        HojaDeRutaId = null;
        foreach (var bulto in _bultos.Where(b => b.EstadoBulto is MaquinaEstadosBulto.CargadoEnVehiculo or MaquinaEstadosBulto.EnRetornoADeposito or MaquinaEstadosBulto.NoEntregado or MaquinaEstadosBulto.PendienteReenvio))
            bulto.MarcarRecibido();

        TransicionarEstado(EstadoEnvio.EnDeposito, tipoEvento, responsableUsuarioId, "Backoffice", payload: detalle);
    }

    /// <summary>
    /// Declara el envío como extraviado (RF-11): estado terminal que se admite desde cualquier estado no terminal.
    /// Si estaba asignado a una ruta se desvincula; la parada de la ruta la cancela quien invoca (el agregado de la ruta).
    /// </summary>
    public void MarcarExtraviado(string motivo, Guid responsableUsuarioId)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainValidationException("Indique el motivo del extravío.");

        if (motivo.Trim().Length > 500)
            throw new DomainValidationException("El motivo del extravío no puede superar los 500 caracteres.");

        if (MaquinaEstadosEnvio.EsEstadoTerminal(EstadoActual))
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede declarar extraviado porque su estado es '{EstadoActual}'.");

        TransicionarEstado(EstadoEnvio.Extraviado, "EnvioExtraviado", responsableUsuarioId, "Backoffice", payload: motivo.Trim());
        HojaDeRutaId = null;
    }

    public void TransicionarEstado(
        EstadoEnvio nuevoEstado,
        string tipoEvento,
        Guid? responsableUsuarioId = null,
        string? origen = null,
        double? latitud = null,
        double? longitud = null,
        string? payload = null)
    {
        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        // UC-39: toda transición se valida contra la máquina de estados declarativa
        if (!MaquinaEstadosEnvio.EsTransicionValida(EstadoActual, nuevoEstado))
            throw new DomainValidationException(
                $"Transición de estado inválida para el envío {CodigoSeguimientoPublico}: '{EstadoActual}' -> '{nuevoEstado}'.");

        var estadoAnterior = EstadoActual;
        EstadoActual = nuevoEstado;
        SelloModificacion = Guid.NewGuid();

        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(),
            Id,
            tipoEvento,
            estadoAnterior,
            nuevoEstado,
            origen ?? "Sistema",
            responsableUsuarioId,
            latitud,
            longitud,
            payload));
    }

    /// <summary>
    /// Deriva declarativamente el estado del envío a partir de los estados de sus bultos (RF-31).
    /// </summary>
    public EstadoEnvio DerivarEstadoDesdeBultos() =>
        MaquinaEstadosBulto.DerivarEstadoEnvio(_bultos, EstadoActual);

    /// <summary>Indica si todos los bultos disponibles para ruta del envío ya fueron clasificados.</summary>
    public bool EstaClasificado =>
        _bultos.Where(b => b.EstaDisponibleParaRuta).All(b => b.EstaClasificado)
        && _bultos.Any(b => b.EstaDisponibleParaRuta);

    /// <summary>
    /// Clasifica bultos del envío (zona, prioridad, tipo) y los deja disponibles para la hoja de ruta.
    /// La operación es atómica: si algún bulto no es válido no se clasifica ninguno.
    /// </summary>
    public void ClasificarBultos(IEnumerable<ClasificacionBulto>? clasificaciones, Guid usuarioDepositoId)
    {
        if (usuarioDepositoId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario de depósito no puede ser vacío.");

        if (EstadoActual is not (EstadoEnvio.EnDeposito or EstadoEnvio.EntregadoParcialmente))
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede clasificar porque su estado es '{EstadoActual}' (se requiere '{EstadoEnvio.EnDeposito}').");

        var lista = clasificaciones?.ToList() ?? new List<ClasificacionBulto>();
        if (lista.Count == 0)
            throw new DomainValidationException("Debe indicar al menos un bulto a clasificar.");

        if (lista.Any(c => c is null || string.IsNullOrWhiteSpace(c.CodigoBulto)))
            throw new DomainValidationException("Cada clasificación debe indicar el código del bulto.");

        var codigos = lista.Select(c => c.CodigoBulto.Trim().ToUpperInvariant()).ToList();
        var repetidos = codigos.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            throw new DomainValidationException($"Hay bultos repetidos en la clasificación: {string.Join(", ", repetidos)}.");

        var aplicar = new List<(Bulto Bulto, ClasificacionBulto Clasificacion)>();
        for (var i = 0; i < lista.Count; i++)
        {
            var bulto = _bultos.FirstOrDefault(b => b.Identificacion == codigos[i])
                ?? throw new DomainValidationException($"El bulto '{codigos[i]}' no pertenece al envío {CodigoSeguimientoPublico}.");

            bulto.ValidarClasificacion(lista[i].Prioridad, lista[i].ZonaCoberturaId, lista[i].Tipo);
            aplicar.Add((bulto, lista[i]));
        }

        foreach (var (bulto, clasificacion) in aplicar)
            bulto.Clasificar(clasificacion.Prioridad, clasificacion.ZonaCoberturaId, clasificacion.Tipo);

        RegistrarEventoSinCambioDeEstado(
            "ClasificacionBultos",
            usuarioDepositoId,
            "Backoffice",
            $"Bultos clasificados: {string.Join(", ", codigos)}. Clasificación completa del envío: {(EstaClasificado ? "sí" : "no")}.");
    }

    /// <summary>Registra un hecho relevante del envío en su historial sin alterar su estado.</summary>
    public void RegistrarEventoSinCambioDeEstado(
        string tipoEvento,
        Guid? responsableUsuarioId = null,
        string? origen = null,
        string? payload = null)
    {
        if (string.IsNullOrWhiteSpace(tipoEvento))
            throw new DomainValidationException("El tipo de evento no puede ser vacío.");

        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(),
            Id,
            tipoEvento,
            EstadoActual,
            EstadoActual,
            origen ?? "Sistema",
            responsableUsuarioId,
            payload: payload));
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>
    /// Recepciona el envío en depósito contrastando los bultos escaneados contra los declarados por el comercio.
    /// Sin discrepancias, el envío pasa a EnDeposito. Con discrepancias solo avanza si el operario las documenta
    /// en las observaciones; de lo contrario no se modifica nada y se informa el resultado.
    /// </summary>
    public ResultadoRecepcion RecepcionarEnDeposito(
        IEnumerable<string>? codigosEscaneados,
        IEnumerable<string>? codigosDanados,
        string? observaciones,
        Guid usuarioDepositoId)
    {
        if (usuarioDepositoId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario de depósito no puede ser vacío.");

        if (EstadoActual != EstadoEnvio.Admitido)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede recepcionar porque su estado es '{EstadoActual}' (se requiere '{EstadoEnvio.Admitido}').");

        if (_bultos.Count == 0)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no tiene bultos declarados para recepcionar.");

        static HashSet<string> Normalizar(IEnumerable<string>? codigos) =>
            (codigos ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .ToHashSet();

        var escaneados = Normalizar(codigosEscaneados);
        var danados = Normalizar(codigosDanados);
        var declarados = _bultos.Select(b => b.Identificacion).ToHashSet();

        var fueraDeEnvio = danados.Where(c => !declarados.Contains(c)).ToList();
        if (fueraDeEnvio.Count > 0)
            throw new DomainValidationException($"Los bultos marcados como dañados no pertenecen al envío: {string.Join(", ", fueraDeEnvio)}.");

        var danadosNoEscaneados = danados.Where(c => !escaneados.Contains(c)).ToList();
        if (danadosNoEscaneados.Count > 0)
            throw new DomainValidationException($"Los bultos marcados como dañados deben haber sido escaneados: {string.Join(", ", danadosNoEscaneados)}.");

        var correctos = _bultos.Where(b => escaneados.Contains(b.Identificacion) && !danados.Contains(b.Identificacion)).ToList();
        var faltantes = _bultos.Where(b => !escaneados.Contains(b.Identificacion)).ToList();
        var danadosBultos = _bultos.Where(b => danados.Contains(b.Identificacion)).ToList();
        var sobrantes = escaneados.Where(c => !declarados.Contains(c)).OrderBy(c => c).ToList();

        var hayDiscrepancias = faltantes.Count > 0 || sobrantes.Count > 0 || danadosBultos.Count > 0;
        var resultado = new ResultadoRecepcion(
            !hayDiscrepancias || !string.IsNullOrWhiteSpace(observaciones),
            correctos.Select(b => b.Identificacion).ToList(),
            faltantes.Select(b => b.Identificacion).ToList(),
            sobrantes,
            danadosBultos.Select(b => b.Identificacion).ToList());

        if (!resultado.Recepcionado)
            return resultado;

        foreach (var bulto in correctos)
            bulto.MarcarRecibido();

        foreach (var bulto in faltantes)
        {
            bulto.MarcarFaltanteEnRecepcion();
            RegistrarDiscrepancia(bulto.Id, "Faltante", $"Bulto {bulto.Identificacion} no escaneado en la recepción. {observaciones?.Trim()}", usuarioDepositoId);
        }

        foreach (var bulto in danadosBultos)
        {
            bulto.MarcarDanado();
            RegistrarDiscrepancia(bulto.Id, "Danado", $"Bulto {bulto.Identificacion} recibido con daños. {observaciones?.Trim()}", usuarioDepositoId);
        }

        foreach (var codigo in sobrantes)
            RegistrarDiscrepancia(null, "Sobrante", $"Código {codigo} escaneado y no declarado en el envío. {observaciones?.Trim()}", usuarioDepositoId);

        TransicionarEstado(
            EstadoEnvio.EnDeposito,
            "RecepcionEnDeposito",
            usuarioDepositoId,
            "Backoffice",
            payload: hayDiscrepancias
                ? $"Recepción con discrepancias documentadas: {observaciones!.Trim()}"
                : "Recepción sin discrepancias.");

        return resultado;
    }

    public void RegistrarDiscrepancia(
        Guid? bultoId,
        string tipoDiscrepancia,
        string detalle,
        Guid usuarioDeposito)
    {
        var discrepancia = new DiscrepanciaRecepcion(
            Guid.NewGuid(),
            Id,
            tipoDiscrepancia,
            detalle,
            usuarioDeposito,
            bultoId);

        _discrepancias.Add(discrepancia);
        SelloModificacion = Guid.NewGuid();
    }

    public void AplicarTarifa(decimal tarifa, Guid reglaTarifaId)
    {
        if (tarifa < 0)
            throw new DomainValidationException("La tarifa no puede ser negativa.");

        if (reglaTarifaId == Guid.Empty)
            throw new DomainValidationException("La regla de tarifa no puede ser vacía.");

        TarifaCalculada = tarifa;
        ReglaTarifaAplicadaId = reglaTarifaId;
        SelloModificacion = Guid.NewGuid();
    }

    public IntentoEntrega RegistrarEntregaExitosa(
        Guid paradaId,
        ReglaOperativa reglaOperativa,
        string nombreReceptor,
        string documentoReceptor,
        string? firmaDigitalBase64,
        string? fotoEvidenciaBase64,
        double? latitud,
        double? longitud,
        DateTime fechaHoraDispositivoUtc,
        Guid? responsableUsuarioId = null)
    {
        if (paradaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        ArgumentNullException.ThrowIfNull(reglaOperativa);

        if (EstadoActual != EstadoEnvio.EnTransito)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede entregar porque su estado actual es '{EstadoActual}' (se requiere '{EstadoEnvio.EnTransito}').");

        if (string.IsNullOrWhiteSpace(nombreReceptor))
            throw new DomainValidationException("El nombre del receptor es requerido para registrar la entrega exitosa.");

        reglaOperativa.ValidarPruebaEntrega(firmaDigitalBase64, fotoEvidenciaBase64, documentoReceptor);

        var bultosPorEntregar = _bultos
            .Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion && !b.EstaEntregado)
            .ToList();

        var intentoId = Guid.NewGuid();
        var prueba = new PruebaDeEntrega(
            intentoId,
            tipo: reglaOperativa.TipoPruebaEntregaExigida,
            firmaImagenUrl: firmaDigitalBase64,
            fotoUrl: fotoEvidenciaBase64,
            nombreReceptor: nombreReceptor.Trim(),
            documentoReceptor: documentoReceptor?.Trim(),
            latitud: latitud,
            longitud: longitud,
            timestampDispositivo: fechaHoraDispositivoUtc,
            codigosBultosEntregados: bultosPorEntregar.Select(b => b.Identificacion));

        foreach (var bulto in bultosPorEntregar)
        {
            if (MaquinaEstadosBulto.EsTransicionValida(bulto.EstadoBulto, MaquinaEstadosBulto.Entregado))
            {
                bulto.MarcarEntregado(prueba.Id);
            }
            else if (MaquinaEstadosBulto.EsTransicionValida(bulto.EstadoBulto, MaquinaEstadosBulto.CargadoEnVehiculo))
            {
                bulto.MarcarCargadoEnVehiculo();
                bulto.MarcarEntregado(prueba.Id);
            }
        }

        var intento = new IntentoEntrega(
            intentoId,
            paradaId,
            Id,
            resultado: "Exitoso",
            origenDatos: "MobileApp",
            motivoNoEntregaId: null,
            latitud: latitud,
            longitud: longitud);

        intento.AdjuntarPruebaEntrega(prueba);
        _intentosEntrega.Add(intento);

        TransicionarEstado(
            EstadoEnvio.Entregado,
            "EntregaExitosa",
            responsableUsuarioId,
            "MobileApp",
            latitud,
            longitud,
            payload: $"Receptor: {nombreReceptor.Trim()} (Doc: {documentoReceptor?.Trim()}). DispositivoUtc: {fechaHoraDispositivoUtc:O}");

        return intento;
    }

    /// <summary>
    /// Registra el resultado de entrega a nivel de cada bulto en la parada (RF-31, RF-32, RF-33, RF-34).
    /// Asocia la prueba de entrega a los bultos efectivamente entregados, registra el motivo del catálogo para los no entregados,
    /// resuelve conflictos offline a nivel de bulto y deriva declarativamente el estado del envío.
    /// </summary>
    public ResultadoEntregaParcialDominio RegistrarEntregaParcial(
        Guid paradaId,
        ReglaOperativa reglaOperativa,
        IEnumerable<string>? codigosBultosEntregados,
        IReadOnlyCollection<BultoNoEntregadoInput>? bultosNoEntregados,
        string nombreReceptor,
        string documentoReceptor,
        string? firmaDigitalBase64,
        string? fotoEvidenciaBase64,
        double? latitud,
        double? longitud,
        DateTime fechaHoraDispositivoUtc,
        Guid? responsableUsuarioId = null)
    {
        if (paradaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        ArgumentNullException.ThrowIfNull(reglaOperativa);

        if (EstadoActual is not (EstadoEnvio.EnTransito or EstadoEnvio.EntregadoParcialmente))
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no puede registrar entrega de bultos porque su estado actual es '{EstadoActual}' (se requiere '{EstadoEnvio.EnTransito}' o '{EstadoEnvio.EntregadoParcialmente}').");

        if (_bultos.Count == 0)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no tiene bultos declarados.");

        if (string.IsNullOrWhiteSpace(nombreReceptor))
            throw new DomainValidationException("El nombre del receptor es requerido para registrar la entrega de bultos.");

        var entregadosEscaneados = (codigosBultosEntregados ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (entregadosEscaneados.Count == 0)
            throw new DomainValidationException("Debe indicar mediante escaneo al menos un bulto entregado en la parada.");

        var bultosPorCodigo = _bultos.ToDictionary(b => b.Identificacion, StringComparer.OrdinalIgnoreCase);
        var ajenosEntregados = entregadosEscaneados.Where(c => !bultosPorCodigo.ContainsKey(c)).ToList();
        if (ajenosEntregados.Count > 0)
            throw new DomainValidationException($"Los siguientes bultos escaneados no pertenecen al envío {CodigoSeguimientoPublico}: {string.Join(", ", ajenosEntregados)}.");

        var noEntregadosLista = (bultosNoEntregados ?? Array.Empty<BultoNoEntregadoInput>()).ToList();
        var mapaNoEntregados = new Dictionary<string, BultoNoEntregadoInput>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in noEntregadosLista)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.CodigoBulto))
                throw new DomainValidationException("Cada bulto no entregado debe indicar su código de identificación.");

            var codigoNormalizado = item.CodigoBulto.Trim().ToUpperInvariant();
            if (!bultosPorCodigo.ContainsKey(codigoNormalizado))
                throw new DomainValidationException($"El bulto no entregado '{codigoNormalizado}' no pertenece al envío {CodigoSeguimientoPublico}.");

            if (entregadosEscaneados.Contains(codigoNormalizado, StringComparer.OrdinalIgnoreCase))
                throw new DomainValidationException($"El bulto '{codigoNormalizado}' no puede declararse simultáneamente como entregado y no entregado.");

            if (item.Motivo is null)
                throw new DomainValidationException($"Debe indicar un motivo del catálogo para el bulto no entregado '{codigoNormalizado}'.");

            if (!item.Motivo.Activo)
                throw new DomainValidationException($"El motivo de no entrega '{item.Motivo.Codigo}' para el bulto '{codigoNormalizado}' se encuentra inactivo.");

            mapaNoEntregados[codigoNormalizado] = item with { CodigoBulto = codigoNormalizado };
        }

        var operativos = _bultos
            .Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion)
            .ToList();

        var conflictosResueltos = new List<string>();
        var entregadosEnEstaVisita = new List<Bulto>();
        var noEntregadosEnEstaVisita = new List<(Bulto Bulto, BultoNoEntregadoInput Info)>();

        foreach (var bulto in operativos)
        {
            bool escaneadoComoEntregado = entregadosEscaneados.Contains(bulto.Identificacion, StringComparer.OrdinalIgnoreCase);
            bool reportadoNoEntregado = mapaNoEntregados.TryGetValue(bulto.Identificacion, out var infoNoEntregado);

            if (bulto.EstaEntregado)
            {
                if (reportadoNoEntregado)
                {
                    // Resolución de conflicto a nivel de bulto (RF-32): una entrega con prueba previa prevalece sobre un reporte offline tardío de no entrega
                    conflictosResueltos.Add(
                        $"Conflicto resuelto en bulto '{bulto.Identificacion}': ya constaba como Entregado con prueba de entrega; se preserva su entrega frente al reporte de no entrega ({infoNoEntregado!.Motivo.Codigo}).");
                }
                continue;
            }

            if (escaneadoComoEntregado)
            {
                entregadosEnEstaVisita.Add(bulto);
            }
            else if (reportadoNoEntregado)
            {
                noEntregadosEnEstaVisita.Add((bulto, infoNoEntregado!));
            }
            else
            {
                throw new DomainValidationException(
                    $"El bulto '{bulto.Identificacion}' no fue escaneado como entregado y no se indicó el motivo de no entrega correspondiente.");
            }
        }

        bool quedaranBultosSinEntregar = noEntregadosEnEstaVisita.Count > 0
            || operativos.Any(b => !b.EstaEntregado && !entregadosEnEstaVisita.Contains(b));

        if (quedaranBultosSinEntregar && !reglaOperativa.AdmiteEntregaParcial)
        {
            throw new DomainValidationException(
                "El operador no admite entregas parciales según su regla operativa vigente.");
        }

        reglaOperativa.ValidarPruebaEntrega(firmaDigitalBase64, fotoEvidenciaBase64, documentoReceptor);

        var codigosEntregadosEfectivos = entregadosEnEstaVisita
            .Select(b => b.Identificacion)
            .ToList();

        var intentoId = Guid.NewGuid();
        var prueba = new PruebaDeEntrega(
            intentoId,
            tipo: reglaOperativa.TipoPruebaEntregaExigida,
            firmaImagenUrl: firmaDigitalBase64,
            fotoUrl: fotoEvidenciaBase64,
            nombreReceptor: nombreReceptor.Trim(),
            documentoReceptor: documentoReceptor?.Trim(),
            latitud: latitud,
            longitud: longitud,
            timestampDispositivo: fechaHoraDispositivoUtc,
            codigosBultosEntregados: codigosEntregadosEfectivos);

        foreach (var bulto in entregadosEnEstaVisita)
        {
            if (bulto.EstadoBulto is MaquinaEstadosBulto.Ingresado or MaquinaEstadosBulto.Recibido or MaquinaEstadosBulto.Danado)
                bulto.MarcarCargadoEnVehiculo();

            if (bulto.EstadoBulto is MaquinaEstadosBulto.NoEntregado or MaquinaEstadosBulto.PendienteReenvio)
            {
                conflictosResueltos.Add(
                    $"Conflicto resuelto en bulto '{bulto.Identificacion}': actualización offline con prueba de entrega válida promueve el bulto de '{bulto.EstadoBulto}' a 'Entregado'.");
            }

            bulto.MarcarEntregado(prueba.Id);
        }

        string destinoBultoPendiente = string.Equals(
            reglaOperativa.PoliticaEntregaParcial,
            ReglaOperativa.PoliticaDevolucionAlComercio,
            StringComparison.OrdinalIgnoreCase)
            ? MaquinaEstadosBulto.EnRetornoADeposito
            : MaquinaEstadosBulto.PendienteReenvio;

        foreach (var (bulto, info) in noEntregadosEnEstaVisita)
        {
            if (bulto.EstadoBulto is MaquinaEstadosBulto.Ingresado or MaquinaEstadosBulto.Recibido or MaquinaEstadosBulto.Danado)
                bulto.MarcarCargadoEnVehiculo();

            bulto.MarcarNoEntregado(info.Motivo.Codigo, destinoBultoPendiente);
        }

        var estadoDerivado = DerivarEstadoDesdeBultos();
        string resultadoIntento = estadoDerivado == EstadoEnvio.Entregado ? "Exitoso" : "Parcial";
        Guid? motivoPrincipalId = noEntregadosEnEstaVisita.FirstOrDefault().Info?.Motivo.Id;

        var intento = new IntentoEntrega(
            intentoId,
            paradaId,
            Id,
            resultado: resultadoIntento,
            origenDatos: "MobileApp",
            motivoNoEntregaId: motivoPrincipalId,
            latitud: latitud,
            longitud: longitud);

        intento.AdjuntarPruebaEntrega(prueba);
        _intentosEntrega.Add(intento);

        bool aplicaRecargo = estadoDerivado == EstadoEnvio.EntregadoParcialmente
            && string.Equals(reglaOperativa.PoliticaEntregaParcial, ReglaOperativa.PoliticaNuevaVisitaConRecargo, StringComparison.OrdinalIgnoreCase);

        if (aplicaRecargo && TarifaCalculada > 0)
        {
            decimal recargoVisita = Math.Round(TarifaCalculada * 0.50m, 2);
            TarifaCalculada += recargoVisita;
        }

        int totalOperativos = operativos.Count;
        int totalEntregadosAcumulados = operativos.Count(b => b.EstaEntregado);
        int totalPendientes = totalOperativos - totalEntregadosAcumulados;

        string resumenNoEntregados = noEntregadosEnEstaVisita.Count == 0
            ? "Ninguno"
            : string.Join(", ", noEntregadosEnEstaVisita.Select(x => $"{x.Bulto.Identificacion}:{x.Info.Motivo.Codigo}"));

        string payloadEvento =
            $"Avance: {totalEntregadosAcumulados}/{totalOperativos} bultos entregados. " +
            $"Entregados en parada: [{string.Join(", ", codigosEntregadosEfectivos)}]. " +
            $"No entregados: [{resumenNoEntregados}]. " +
            $"Política aplicada: {reglaOperativa.PoliticaEntregaParcial}. " +
            $"Receptor: {nombreReceptor.Trim()} (Doc: {documentoReceptor?.Trim()}). DispositivoUtc: {fechaHoraDispositivoUtc:O}";

        string tipoEvento = estadoDerivado == EstadoEnvio.Entregado ? "EntregaExitosa" : "EntregaParcial";

        if (EstadoActual != estadoDerivado)
        {
            TransicionarEstado(
                estadoDerivado,
                tipoEvento,
                responsableUsuarioId,
                "MobileApp",
                latitud,
                longitud,
                payload: payloadEvento);
        }
        else
        {
            RegistrarEventoSinCambioDeEstado(
                tipoEvento,
                responsableUsuarioId,
                "MobileApp",
                payload: payloadEvento);
        }

        return new ResultadoEntregaParcialDominio(
            intento,
            prueba,
            estadoDerivado,
            totalOperativos,
            totalEntregadosAcumulados,
            totalPendientes,
            reglaOperativa.PoliticaEntregaParcial,
            aplicaRecargo,
            codigosEntregadosEfectivos,
            noEntregadosEnEstaVisita.Select(x => x.Bulto.Identificacion).ToList(),
            conflictosResueltos);
    }

    public (IntentoEntrega Intento, EstadoEnvio EstadoFinal, int IntentosRestantes) RegistrarIntentoFallido(
        Guid paradaId,
        MotivoNoEntrega motivo,
        ReglaOperativa reglaOperativa,
        string observaciones,
        string? fotoEvidenciaBase64,
        double? latitud,
        double? longitud,
        DateTime fechaHoraDispositivoUtc,
        Guid? responsableUsuarioId = null)
    {
        if (paradaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        ArgumentNullException.ThrowIfNull(motivo);
        ArgumentNullException.ThrowIfNull(reglaOperativa);

        if (EstadoActual != EstadoEnvio.EnTransito)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no puede registrar un intento fallido porque su estado actual es '{EstadoActual}' (se requiere '{EstadoEnvio.EnTransito}').");

        if (!motivo.Activo)
            throw new DomainValidationException($"El motivo de no entrega '{motivo.Codigo}' se encuentra inactivo y no puede ser utilizado.");

        if (string.IsNullOrWhiteSpace(observaciones))
            throw new DomainValidationException("Las observaciones de la incidencia son obligatorias para registrar un intento fallido.");

        var intentoId = Guid.NewGuid();
        var intento = new IntentoEntrega(
            intentoId,
            paradaId,
            Id,
            resultado: "Fallido",
            origenDatos: "MobileApp",
            motivoNoEntregaId: motivo.Id,
            latitud: latitud,
            longitud: longitud);

        if (!string.IsNullOrWhiteSpace(fotoEvidenciaBase64))
        {
            var prueba = new PruebaDeEntrega(
                intentoId,
                tipo: "EvidenciaFallo",
                firmaImagenUrl: null,
                fotoUrl: fotoEvidenciaBase64,
                nombreReceptor: null,
                documentoReceptor: null,
                latitud: latitud,
                longitud: longitud,
                timestampDispositivo: fechaHoraDispositivoUtc);

            intento.AdjuntarPruebaEntrega(prueba);
        }

        _intentosEntrega.Add(intento);

        TransicionarEstado(
            EstadoEnvio.NoEntregado,
            "IntentoEntregaFallido",
            responsableUsuarioId,
            "MobileApp",
            latitud,
            longitud,
            payload: $"Motivo: {motivo.Codigo} - {motivo.Descripcion}. Observaciones: {observaciones.Trim()}. DispositivoUtc: {fechaHoraDispositivoUtc:O}");

        int intentosAcumulados = _intentosEntrega.Count(i => i.Resultado == "Fallido");
        bool superaMaximo = reglaOperativa.SuperaMaximoIntentos(intentosAcumulados);
        int intentosRestantes = reglaOperativa.CalcularIntentosRestantes(intentosAcumulados);

        EstadoEnvio estadoFinal;
        string tipoEventoPaso2;
        string payloadPaso2;

        if (superaMaximo)
        {
            estadoFinal = EstadoEnvio.EnDevolucion;
            tipoEventoPaso2 = "ResolucionReglaOperativa_EnDevolucion";
            payloadPaso2 = $"Superado el límite de {reglaOperativa.MaxIntentosEntrega} intentos permitidos ({intentosAcumulados} registrados). Activando política de devolución: '{reglaOperativa.PoliticaDevolucion}'.";
        }
        else
        {
            estadoFinal = EstadoEnvio.Reprogramado;
            tipoEventoPaso2 = "ResolucionReglaOperativa_Reprogramado";
            payloadPaso2 = $"Intento fallido {intentosAcumulados} de {reglaOperativa.MaxIntentosEntrega}. Reprogramado automáticamente para nuevo intento (Quedan {intentosRestantes} intentos). Plazo reglamentario entre intentos: {reglaOperativa.PlazoEntreIntentos}.";
        }

        TransicionarEstado(
            estadoFinal,
            tipoEventoPaso2,
            responsableUsuarioId,
            "ReglasOperativasMotor",
            latitud,
            longitud,
            payload: payloadPaso2);

        return (intento, estadoFinal, intentosRestantes);
    }

    /// <summary>
    /// Registra un evento de entrada o salida de geocerca con histéresis e idempotencia.
    /// </summary>
    public (EventoEnvio Evento, bool Idempotente) RegistrarEventoGeocerca(
        string tipoEventoGeocerca,
        double latitud,
        double longitud,
        double distanciaMetros,
        int? tiempoPermanenciaSegundos = null,
        Guid? responsableUsuarioId = null)
    {
        if (string.IsNullOrWhiteSpace(tipoEventoGeocerca))
            throw new DomainValidationException("El tipo de evento de geocerca no puede ser vacío.");

        if (double.IsNaN(latitud) || latitud < -90 || latitud > 90)
            throw new DomainValidationException("La latitud de la geocerca debe estar entre -90 y 90 grados.");

        if (double.IsNaN(longitud) || longitud < -180 || longitud > 180)
            throw new DomainValidationException("La longitud de la geocerca debe estar entre -180 y 180 grados.");

        if (double.IsNaN(distanciaMetros) || distanciaMetros < 0)
            throw new DomainValidationException("La distancia en metros a la parada no puede ser negativa.");

        string tipoNormalizado = tipoEventoGeocerca.Trim().ToUpperInvariant() switch
        {
            "LLEGADA_GEOCERCA" or "LLEGADAGEOCERCA" => "LlegadaGeocerca",
            "SALIDA_GEOCERCA" or "SALIDAGEOCERCA" => "SalidaGeocerca",
            _ => throw new DomainValidationException($"Tipo de evento de geocerca no válido: '{tipoEventoGeocerca}'. Use 'LLEGADA_GEOCERCA' o 'SALIDA_GEOCERCA'.")
        };

        var ultimoEventoGeocerca = _eventos
            .LastOrDefault(e => e.TipoEvento is "LlegadaGeocerca" or "SalidaGeocerca");

        if (ultimoEventoGeocerca is not null && ultimoEventoGeocerca.TipoEvento == tipoNormalizado)
        {
            return (ultimoEventoGeocerca, true);
        }

        string descripcionPublica = tipoNormalizado == "LlegadaGeocerca"
            ? $"Repartidor ingresó a la zona de entrega (Geocerca < 100m, distancia: {Math.Round(distanciaMetros)}m)"
            : $"Repartidor salió de la zona de entrega (Permanencia: {Math.Max(0, tiempoPermanenciaSegundos ?? 0)}s)";

        var nuevoEvento = new EventoEnvio(
            Guid.NewGuid(),
            Id,
            tipoNormalizado,
            EstadoActual,
            EstadoActual,
            "MobileApp",
            responsableUsuarioId,
            latitud,
            longitud,
            descripcionPublica);

        _eventos.Add(nuevoEvento);
        SelloModificacion = Guid.NewGuid();

        return (nuevoEvento, false);
    }

    public void ReiniciarParaDemoEnTransito(Guid hojaDeRutaId)
    {
        HojaDeRutaId = hojaDeRutaId;
        EstadoActual = EstadoEnvio.EnTransito;
        SelloModificacion = Guid.NewGuid();
        _intentosEntrega.Clear();
        _eventos.RemoveAll(e => e.TipoEvento is "LlegadaGeocerca" or "SalidaGeocerca" or "ReinicioDemo" or "EntregaParcial");
        foreach (var bulto in _bultos)
        {
            bulto.ReiniciarEstadoParaDemo();
        }
        _eventos.Add(new EventoEnvio(
            Guid.NewGuid(),
            Id,
            "ReinicioDemo",
            null,
            EstadoEnvio.EnTransito,
            "DemoReset"));
    }
}

/// <summary>Datos de un bulto no entregado en la parada junto con su motivo del catálogo del operador (RF-32).</summary>
public record BultoNoEntregadoInput(
    string CodigoBulto,
    MotivoNoEntrega Motivo,
    string? Observaciones = null);

/// <summary>Resultado de dominio tras procesar una entrega a nivel de bultos (RF-31 a RF-34).</summary>
public record ResultadoEntregaParcialDominio(
    IntentoEntrega Intento,
    PruebaDeEntrega Prueba,
    EstadoEnvio EstadoFinalEnvio,
    int TotalBultosOperativos,
    int BultosEntregadosAcumulados,
    int BultosPendientes,
    string PoliticaAplicada,
    bool RecargoAplicado,
    IReadOnlyList<string> CodigosEntregadosEnVisita,
    IReadOnlyList<string> CodigosNoEntregadosEnVisita,
    IReadOnlyList<string> ConflictosResueltos);
