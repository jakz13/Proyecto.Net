using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Operador;
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

    private readonly List<IntentoEntrega> _intentosEntrega = new();
    public IReadOnlyCollection<IntentoEntrega> IntentosEntrega => _intentosEntrega.AsReadOnly();
    public int CantidadIntentos => _intentosEntrega.Count;

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
        Guid? loteImportacionId = null,
        string? direccionDestino = null,
        string? nombreDestinatario = null) : base(id)
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
        // Copia desnormalizada para listados y etiquetas sin necesidad de resolver las entidades relacionadas
        DireccionDestino = direccionDestino?.Trim() ?? string.Empty;
        NombreDestinatario = nombreDestinatario?.Trim() ?? string.Empty;
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

    public decimal PesoTotal => _bultos.Sum(b => b.Peso);
    public decimal VolumenTotal => _bultos.Sum(b => b.Volumen);

    public void AgregarBulto(string identificacion, decimal peso, decimal alto, decimal ancho, decimal profundo)
    {
        if (EstadoActual != EstadoEnvio.Admitido)
            throw new DomainValidationException($"No se pueden agregar bultos a un envío en estado '{EstadoActual}'.");

        if (_bultos.Any(b => b.Identificacion.Equals(identificacion?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainValidationException($"El envío ya contiene un bulto con la identificación '{identificacion}'.");

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

    public void DespacharEnRuta(Guid hojaDeRutaId, Guid? responsableUsuarioId = null, string? observaciones = null)
    {
        if (EstadoActual != EstadoEnvio.AsignadoARuta)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede despachar porque su estado es {EstadoActual} (se requiere estado AsignadoARuta).");

        HojaDeRutaId = hojaDeRutaId;
        foreach (var bulto in _bultos)
        {
            bulto.MarcarCargadoEnVehiculo();
        }

        TransicionarEstado(
            EstadoEnvio.EnTransito,
            "DespachoRuta",
            responsableUsuarioId,
            "MobileApp",
            null,
            null,
            observaciones);
    }

    public void DesasignarDeRutaPorFaltante(string motivo, Guid? responsableUsuarioId = null)
    {
        if (EstadoActual != EstadoEnvio.AsignadoARuta)
            throw new DomainValidationException($"El envío {CodigoSeguimientoPublico} no se puede desasignar porque su estado es {EstadoActual}.");

        HojaDeRutaId = null;
        foreach (var bulto in _bultos)
        {
            bulto.MarcarFaltanteEnCarga();
        }

        TransicionarEstado(
            EstadoEnvio.EnDeposito,
            "BultoFaltanteEnCarga",
            responsableUsuarioId,
            "MobileApp",
            null,
            null,
            motivo);
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

        // Evaluación de la prueba de entrega contra la regla operativa del operador
        reglaOperativa.ValidarPruebaEntrega(firmaDigitalBase64, fotoEvidenciaBase64, documentoReceptor);

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
            timestampDispositivo: fechaHoraDispositivoUtc);

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

        // Transición de estado a Entregado con EventoEnvio inmutable
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

        // Paso 1 de Estado: Transicionar el Envio a NoEntregado y registrar su EventoEnvio inmutable
        TransicionarEstado(
            EstadoEnvio.NoEntregado,
            "IntentoEntregaFallido",
            responsableUsuarioId,
            "MobileApp",
            latitud,
            longitud,
            payload: $"Motivo: {motivo.Codigo} - {motivo.Descripcion}. Observaciones: {observaciones.Trim()}. DispositivoUtc: {fechaHoraDispositivoUtc:O}");

        // Paso 2 de Estado: Evaluación automática de regla operativa
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
}
