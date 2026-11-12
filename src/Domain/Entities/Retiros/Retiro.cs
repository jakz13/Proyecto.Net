using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Retiros;

/// <summary>
/// Retiro en domicilio (cambio de requerimientos): a pedido del comercio, el operador concurre al domicilio del
/// comprador, retira uno o más bultos y los hace llegar al comercio. No es una devolución de un envío que no pudo entregarse:
/// el flujo comienza en el comprador y termina en el comercio.
/// Pertenece a un operador (primer nivel de aislamiento) y a la relación comercio-operador que lo pidió (segundo nivel).
/// </summary>
public class Retiro : AggregateRoot, IMustHaveTenant, IMustHaveComercioTenant, IHasConcurrencyToken
{
    public const string OrigenPortal = "PortalComercio";
    public const string OrigenApi = "ApiComercio";
    public const string OrigenBackoffice = "Backoffice";
    public const string OrigenMobile = "MobileApp";

    public Guid OperadorId { get; private set; }
    public Guid ComercioOperadorId { get; private set; }

    /// <summary>Código con el que el comprador sigue el retiro por el mismo enlace público que los envíos. Empieza con "RET-".</summary>
    public string CodigoSeguimientoPublico { get; private set; } = string.Empty;

    /// <summary>Identificador del retiro en el sistema del comercio; permite reintentar la solicitud sin duplicarla.</summary>
    public string? ReferenciaExterna { get; private set; }

    public string NombreContacto { get; private set; } = string.Empty;
    public string? TelefonoContacto { get; private set; }
    public string? EmailContacto { get; private set; }
    public Guid DireccionRetiroId { get; private set; }
    public string DireccionRetiro { get; private set; } = string.Empty;
    public Guid? FranjaHorariaId { get; private set; }
    public DateOnly? FechaSolicitada { get; private set; }
    public string Motivo { get; private set; } = string.Empty;

    public EstadoRetiro EstadoActual { get; private set; }
    public decimal TarifaCalculada { get; private set; }
    public Guid? ReglaTarifaAplicadaId { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }
    public DateTime FechaSolicitud { get; private set; }

    // Datos del retiro efectivo, capturados por el repartidor en el domicilio
    public DateTime? FechaRetiro { get; private set; }
    public string? NombreEntrega { get; private set; }
    public string? DocumentoEntrega { get; private set; }
    public string? FotoEstadoBase64 { get; private set; }
    public string? FirmaBase64 { get; private set; }
    public double? LatitudRetiro { get; private set; }
    public double? LongitudRetiro { get; private set; }
    public string? MotivoNoRetiroCodigo { get; private set; }

    public Guid SelloModificacion { get; private set; }

    private readonly List<BultoRetiro> _bultos = new();
    public IReadOnlyCollection<BultoRetiro> Bultos => _bultos.AsReadOnly();

    private readonly List<EventoRetiro> _eventos = new();
    public IReadOnlyCollection<EventoRetiro> Eventos => _eventos.AsReadOnly();

    public decimal PesoTotal => _bultos.Sum(b => b.Peso);
    public decimal VolumenTotal => _bultos.Sum(b => b.Volumen);

    protected Retiro() { }

    public Retiro(
        Guid id,
        Guid operadorId,
        Guid comercioOperadorId,
        string codigoSeguimientoPublico,
        string nombreContacto,
        Guid direccionRetiroId,
        string direccionRetiro,
        string motivo,
        string? telefonoContacto = null,
        string? emailContacto = null,
        Guid? franjaHorariaId = null,
        DateOnly? fechaSolicitada = null,
        string? referenciaExterna = null,
        string origen = OrigenPortal,
        Guid? responsableUsuarioId = null,
        DateTime? fechaSolicitud = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(codigoSeguimientoPublico))
            throw new DomainValidationException("El código de seguimiento del retiro no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombreContacto))
            throw new DomainValidationException("El nombre de la persona que entrega los bultos es obligatorio.");

        if (direccionRetiroId == Guid.Empty || string.IsNullOrWhiteSpace(direccionRetiro))
            throw new DomainValidationException("El domicilio de retiro es obligatorio.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainValidationException("El motivo del retiro es obligatorio.");

        if (franjaHorariaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la franja horaria no puede ser vacío.");

        ValidacionTexto.LargoMaximo(nombreContacto, 200, "El nombre de contacto");
        ValidacionTexto.LargoMaximo(telefonoContacto, 50, "El teléfono de contacto");
        ValidacionTexto.LargoMaximo(emailContacto, 200, "El email de contacto");
        ValidacionTexto.LargoMaximo(motivo, 300, "El motivo del retiro");
        ValidacionTexto.LargoMaximo(referenciaExterna, 100, "La referencia externa");

        OperadorId = operadorId;
        ComercioOperadorId = comercioOperadorId;
        CodigoSeguimientoPublico = codigoSeguimientoPublico.Trim().ToUpperInvariant();
        NombreContacto = nombreContacto.Trim();
        TelefonoContacto = telefonoContacto?.Trim();
        EmailContacto = emailContacto?.Trim().ToLowerInvariant();
        DireccionRetiroId = direccionRetiroId;
        DireccionRetiro = direccionRetiro.Trim();
        FranjaHorariaId = franjaHorariaId;
        FechaSolicitada = fechaSolicitada;
        Motivo = motivo.Trim();
        ReferenciaExterna = string.IsNullOrWhiteSpace(referenciaExterna) ? null : referenciaExterna.Trim();
        FechaSolicitud = fechaSolicitud ?? DateTime.UtcNow;
        EstadoActual = EstadoRetiro.Solicitado;
        SelloModificacion = Guid.NewGuid();

        _eventos.Add(new EventoRetiro(
            Guid.NewGuid(), Id, "SolicitudRetiro", null, EstadoRetiro.Solicitado, origen, responsableUsuarioId, timestamp: FechaSolicitud));
    }

    public void AgregarBulto(string descripcion, decimal peso, decimal alto, decimal ancho, decimal profundo)
    {
        if (EstadoActual != EstadoRetiro.Solicitado)
            throw new DomainValidationException("Solo se pueden agregar bultos a un retiro que todavía no fue asignado a una ruta.");

        var identificacion = $"{CodigoSeguimientoPublico}-B{_bultos.Count + 1:00}";
        _bultos.Add(new BultoRetiro(Guid.NewGuid(), Id, identificacion, descripcion, peso, alto, ancho, profundo));
        SelloModificacion = Guid.NewGuid();
    }

    /// <summary>El retiro tiene su propia tarifa dentro del cuadro tarifario vigente.</summary>
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

    /// <summary>Un retiro está disponible para entrar en una hoja de ruta mientras esté solicitado.</summary>
    public string? ObtenerMotivoNoAsignable()
    {
        if (EstadoActual != EstadoRetiro.Solicitado)
            return $"El retiro {CodigoSeguimientoPublico} está en estado '{EstadoActual}' y no puede asignarse a una ruta.";

        if (_bultos.Count == 0)
            return $"El retiro {CodigoSeguimientoPublico} no tiene bultos declarados.";

        return null;
    }

    public void AsignarARuta(Guid hojaDeRutaId, Guid? responsableUsuarioId = null)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        var motivo = ObtenerMotivoNoAsignable();
        if (motivo is not null)
            throw new DomainValidationException(motivo);

        HojaDeRutaId = hojaDeRutaId;
        Transicionar(EstadoRetiro.AsignadoARuta, "AsignacionRuta", OrigenBackoffice, responsableUsuarioId);
    }

    /// <summary>Vuelve a quedar pendiente de asignación: el despachador lo sacó de la ruta o la ruta se rindió sin visitarlo.</summary>
    public void LiberarDeRuta(string motivo, Guid? responsableUsuarioId = null, string origen = OrigenBackoffice)
    {
        Transicionar(EstadoRetiro.Solicitado, "RetiroLiberadoDeRuta", origen, responsableUsuarioId, payload: motivo);
        HojaDeRutaId = null;
    }

    /// <summary>
    /// Registra el retiro efectivo en el domicilio: identificación de los bultos por escaneo, fotografía de su estado y firma
    /// de quien los entrega, con la posición y la hora del dispositivo. Todos los bultos solicitados deben escanearse.
    /// </summary>
    public void RegistrarRetiro(
        IEnumerable<string> codigosBultosEscaneados,
        string? fotoEstadoBase64,
        string? firmaBase64,
        string nombreEntrega,
        string? documentoEntrega,
        double? latitud,
        double? longitud,
        DateTime fechaHoraDispositivoUtc,
        Guid? responsableUsuarioId = null,
        bool resolverConflicto = false,
        Guid? hojaDeRutaId = null)
    {
        var porConflicto = resolverConflicto && EstadoActual != EstadoRetiro.AsignadoARuta
            && MaquinaEstadosRetiro.EsTransicionValidaPorConflicto(EstadoActual, EstadoRetiro.Retirado);

        if (EstadoActual != EstadoRetiro.AsignadoARuta && !porConflicto)
            throw new DomainValidationException(
                $"El retiro {CodigoSeguimientoPublico} no se puede retirar porque su estado es '{EstadoActual}' (se requiere '{EstadoRetiro.AsignadoARuta}').");

        if (string.IsNullOrWhiteSpace(nombreEntrega))
            throw new DomainValidationException("El nombre de quien entrega los bultos es obligatorio.");

        if (string.IsNullOrWhiteSpace(fotoEstadoBase64))
            throw new DomainValidationException("El retiro exige una fotografía del estado de los bultos.");

        if (string.IsNullOrWhiteSpace(firmaBase64))
            throw new DomainValidationException("El retiro exige la firma de quien entrega los bultos.");

        var escaneados = (codigosBultosEscaneados ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var propios = _bultos.Select(b => b.Identificacion).ToHashSet(StringComparer.Ordinal);

        var ajenos = escaneados.Where(c => !propios.Contains(c)).ToList();
        if (ajenos.Count > 0)
            throw new DomainValidationException($"Los siguientes bultos no pertenecen al retiro {CodigoSeguimientoPublico}: {string.Join(", ", ajenos)}.");

        var faltantes = propios.Where(c => !escaneados.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (faltantes.Count > 0)
            throw new DomainValidationException(
                $"Faltan bultos por escanear para registrar el retiro {CodigoSeguimientoPublico}: {string.Join(", ", faltantes)}. " +
                "Si no se pueden retirar, registre el retiro como no realizado con su motivo.");

        FechaRetiro = fechaHoraDispositivoUtc;
        NombreEntrega = nombreEntrega.Trim();
        DocumentoEntrega = documentoEntrega?.Trim();
        FotoEstadoBase64 = fotoEstadoBase64;
        FirmaBase64 = firmaBase64;
        LatitudRetiro = latitud;
        LongitudRetiro = longitud;
        MotivoNoRetiroCodigo = null;
        if (porConflicto && hojaDeRutaId.HasValue)
            HojaDeRutaId = hojaDeRutaId;

        Transicionar(
            EstadoRetiro.Retirado, "RetiroRealizado", OrigenMobile, responsableUsuarioId, latitud, longitud,
            payload: $"Entregó: {NombreEntrega}. Bultos: {escaneados.Count}. DispositivoUtc: {fechaHoraDispositivoUtc:O}"
                + (porConflicto ? ". Conflicto de sincronización resuelto: prevaleció el retiro físico con evidencia." : string.Empty),
            instante: fechaHoraDispositivoUtc,
            porConflicto: porConflicto);
    }

    /// <summary>Registra que el repartidor no pudo retirar los bultos, con un motivo del catálogo del operador.</summary>
    public void RegistrarNoRetiro(
        string codigoMotivo,
        string? observaciones,
        double? latitud,
        double? longitud,
        DateTime fechaHoraDispositivoUtc,
        Guid? responsableUsuarioId = null,
        bool resolverConflicto = false,
        Guid? hojaDeRutaId = null)
    {
        var porConflicto = resolverConflicto && EstadoActual != EstadoRetiro.AsignadoARuta
            && MaquinaEstadosRetiro.EsTransicionValidaPorConflicto(EstadoActual, EstadoRetiro.NoRetirado);

        if (EstadoActual != EstadoRetiro.AsignadoARuta && !porConflicto)
            throw new DomainValidationException(
                $"El retiro {CodigoSeguimientoPublico} no puede registrarse como no realizado porque su estado es '{EstadoActual}'.");

        if (string.IsNullOrWhiteSpace(codigoMotivo))
            throw new DomainValidationException("Debe indicar el motivo por el que no se pudo realizar el retiro.");

        ValidacionTexto.LargoMaximo(codigoMotivo, 50, "El código del motivo");
        ValidacionTexto.LargoMaximo(observaciones, 300, "Las observaciones");

        MotivoNoRetiroCodigo = codigoMotivo.Trim().ToUpperInvariant();
        LatitudRetiro = latitud;
        LongitudRetiro = longitud;
        if (porConflicto && hojaDeRutaId.HasValue)
            HojaDeRutaId = hojaDeRutaId;

        Transicionar(
            EstadoRetiro.NoRetirado, "RetiroNoRealizado", OrigenMobile, responsableUsuarioId, latitud, longitud,
            payload: (string.IsNullOrWhiteSpace(observaciones) ? MotivoNoRetiroCodigo : $"{MotivoNoRetiroCodigo}: {observaciones.Trim()}")
                + (porConflicto ? ". Conflicto de sincronización resuelto: prevaleció lo ocurrido en el domicilio." : string.Empty),
            instante: fechaHoraDispositivoUtc,
            porConflicto: porConflicto);
    }

    /// <summary>Tras un retiro no realizado, el despachador decide una nueva visita: vuelve a estar solicitado.</summary>
    public void ReprogramarVisita(Guid? responsableUsuarioId = null)
    {
        Transicionar(EstadoRetiro.Solicitado, "RetiroReprogramado", OrigenBackoffice, responsableUsuarioId);
        HojaDeRutaId = null;
        MotivoNoRetiroCodigo = null;
    }

    /// <summary>El repartidor rinde los bultos retirados al regresar y el operario de depósito los recibe.</summary>
    public void RecibirEnDeposito(Guid? responsableUsuarioId = null) =>
        Transicionar(EstadoRetiro.EnDeposito, "RecepcionEnDeposito", OrigenBackoffice, responsableUsuarioId);

    /// <summary>Cierra el flujo: los bultos llegan al comercio.</summary>
    public void EntregarAlComercio(string? receptor, Guid? responsableUsuarioId = null)
    {
        ValidacionTexto.LargoMaximo(receptor, 200, "El nombre de quien recibe en el comercio");
        Transicionar(
            EstadoRetiro.EntregadoAlComercio, "EntregaAlComercio", OrigenBackoffice, responsableUsuarioId,
            payload: string.IsNullOrWhiteSpace(receptor) ? null : $"Recibió: {receptor.Trim()}");
    }

    public void Cancelar(string motivo, Guid? responsableUsuarioId = null, string origen = OrigenBackoffice)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainValidationException("Debe indicar el motivo de la cancelación del retiro.");

        ValidacionTexto.LargoMaximo(motivo, 300, "El motivo de la cancelación");
        Transicionar(EstadoRetiro.Cancelado, "RetiroCancelado", origen, responsableUsuarioId, payload: motivo.Trim());
        HojaDeRutaId = null;
    }

    /// <summary>Único punto por el que cambia el estado: valida contra la tabla declarativa y deja el evento inmutable en el historial.</summary>
    private void Transicionar(
        EstadoRetiro nuevoEstado,
        string tipoEvento,
        string origen,
        Guid? responsableUsuarioId,
        double? latitud = null,
        double? longitud = null,
        string? payload = null,
        DateTime? instante = null,
        bool porConflicto = false)
    {
        if (!MaquinaEstadosRetiro.EsTransicionValida(EstadoActual, nuevoEstado)
            && !(porConflicto && MaquinaEstadosRetiro.EsTransicionValidaPorConflicto(EstadoActual, nuevoEstado)))
            throw new DomainValidationException(
                $"Transición inválida del retiro {CodigoSeguimientoPublico}: de '{EstadoActual}' a '{nuevoEstado}' no está permitida.");

        var anterior = EstadoActual;
        EstadoActual = nuevoEstado;
        SelloModificacion = Guid.NewGuid();
        _eventos.Add(new EventoRetiro(
            Guid.NewGuid(), Id, tipoEvento, anterior, nuevoEstado, origen, responsableUsuarioId, latitud, longitud, payload, instante));
    }
}
