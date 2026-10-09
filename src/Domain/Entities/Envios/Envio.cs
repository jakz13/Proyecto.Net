using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class Envio : AggregateRoot, IMustHaveComercioTenant, IHasConcurrencyToken
{
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
    public DateTime FechaAlta { get; private set; }
    public Guid SelloModificacion { get; private set; }
    public Guid? HojaDeRutaId { get; private set; }

    // Compatibility properties for existing services & tests
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

    protected Envio() { }

    // Rich domain constructor
    public Envio(
        Guid id,
        Guid comercioOperadorId,
        Guid destinatarioId,
        string modalidadServicio,
        string codigoSeguimientoPublico,
        Guid? direccionEntregaId = null,
        Guid? franjaHorariaComprometidaId = null,
        Guid? loteImportacionId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (comercioOperadorId == Guid.Empty)
            throw new DomainValidationException("El identificador de comercio-operador no puede ser vacío.");

        if (destinatarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del destinatario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(modalidadServicio))
            throw new DomainValidationException("La modalidad de servicio no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(codigoSeguimientoPublico))
            throw new DomainValidationException("El código de seguimiento no puede ser vacío.");

        ComercioOperadorId = comercioOperadorId;
        DestinatarioId = destinatarioId;
        ModalidadServicio = modalidadServicio.Trim();
        CodigoSeguimientoPublico = codigoSeguimientoPublico.Trim().ToUpperInvariant();
        DireccionEntregaId = direccionEntregaId;
        FranjaHorariaComprometidaId = franjaHorariaComprometidaId;
        LoteImportacionId = loteImportacionId;
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

    // Constructor supporting initial test suite & simple intake
    public Envio(Guid id, Guid comercioId, string direccionDestino, string nombreDestinatario) : base(id)
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
        CodigoSeguimientoPublico = $"TRK-{id.ToString()[..8].ToUpperInvariant()}";
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

    public void AgregarBulto(string identificacion, decimal peso, decimal alto, decimal ancho, decimal profundo)
    {
        var bulto = new Bulto(Guid.NewGuid(), Id, identificacion, peso, alto, ancho, profundo);
        _bultos.Add(bulto);
        SelloModificacion = Guid.NewGuid();
    }

    public void AsignarRuta(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        HojaDeRutaId = hojaDeRutaId;
        TransicionarEstado(EstadoEnvio.AsignadoARuta, "AsignacionRuta");
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
}
