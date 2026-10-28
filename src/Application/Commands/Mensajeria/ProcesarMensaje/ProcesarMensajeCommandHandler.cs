using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;

public class ProcesarMensajeCommandHandler : IRequestHandler<ProcesarMensajeCommand, ResultadoProcesamientoMensaje>
{
    /// <summary>Cambios de estado que justifican avisar al destinatario sin que tenga que consultar (UC-35).</summary>
    private static readonly HashSet<EstadoEnvio> EstadosNotificablesAlDestinatario = new()
    {
        EstadoEnvio.Admitido,
        EstadoEnvio.EnTransito,
        EstadoEnvio.Entregado,
        EstadoEnvio.NoEntregado,
        EstadoEnvio.Reprogramado
    };

    /// <summary>Valor de suscripción que recibe avisos de cualquier cambio de estado.</summary>
    public const string TodosLosEventos = "*";

    private readonly IIdempotenciaConsumidorRepository _idempotenciaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IDestinatarioRepository _destinatarioRepository;
    private readonly INotificacionRepository _notificacionRepository;
    private readonly INotificacionDestinatarioService _notificacionService;
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly AvisoComercioEntregador _entregador;
    private readonly IUnitOfWork _unitOfWork;

    public ProcesarMensajeCommandHandler(
        IIdempotenciaConsumidorRepository idempotenciaRepository,
        IEnvioRepository envioRepository,
        IDestinatarioRepository destinatarioRepository,
        INotificacionRepository notificacionRepository,
        INotificacionDestinatarioService notificacionService,
        IAvisoComercioRepository avisoRepository,
        AvisoComercioEntregador entregador,
        IUnitOfWork unitOfWork)
    {
        _idempotenciaRepository = idempotenciaRepository;
        _envioRepository = envioRepository;
        _destinatarioRepository = destinatarioRepository;
        _notificacionRepository = notificacionRepository;
        _notificacionService = notificacionService;
        _avisoRepository = avisoRepository;
        _entregador = entregador;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResultadoProcesamientoMensaje> Handle(ProcesarMensajeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MessageId == Guid.Empty)
            throw new DomainValidationException("El identificador del mensaje no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(request.TipoEvento))
            throw new DomainValidationException("El tipo de evento del mensaje no puede ser vacío.");

        var claveMensaje = request.MessageId.ToString();
        if (await _idempotenciaRepository.ExisteAsync(claveMensaje, cancellationToken))
            return new ResultadoProcesamientoMensaje(EstadoProcesamientoMensaje.Duplicado, 0, 0);

        if (!string.Equals(request.TipoEvento, EventoEnvioPublicado.TipoMensaje, StringComparison.Ordinal))
        {
            // Un reintento manual desde la cola de fallidos no puede resolverse en silencio si nadie sabe atenderlo (UC-42)
            if (string.Equals(request.AgregadoOrigen, EventoEnvioPublicado.OrigenColaFallidos, StringComparison.Ordinal))
                throw new MensajeSinManejadorException(request.TipoEvento);

            return new ResultadoProcesamientoMensaje(EstadoProcesamientoMensaje.SinConsumidor, 0, 0);
        }

        var evento = EventoEnvioPublicado.Deserializar(request.Payload)
            ?? throw new MensajeInvalidoException($"El payload del mensaje {claveMensaje} no es un evento de cambio de estado válido.");

        var envio = await _envioRepository.GetByIdAsync(evento.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"No se encontró el envío '{evento.EnvioId}' del evento.");

        // Defensa de aislamiento: el operador declarado por el evento debe ser el dueño real del envío
        if (envio.OperadorId != evento.OperadorId)
            throw new MensajeInvalidoException($"El envío '{evento.EnvioId}' no pertenece al operador del evento.");

        var notificaciones = await NotificarDestinatarioAsync(evento, envio, cancellationToken);
        var avisos = await GenerarAvisosAsync(evento, envio.ComercioOperadorId, cancellationToken);

        // Efectos y clave de idempotencia se confirman juntos: o quedan todos o no queda ninguno
        await _idempotenciaRepository.AddAsync(
            new ClaveIdempotenciaConsumidor(Guid.NewGuid(), claveMensaje, $"notificaciones={notificaciones};avisos={avisos.Count}"),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // La entrega HTTP va después de persistir: si falla, el aviso queda en reintento y no se pierde ni bloquea el mensaje
        await _entregador.EntregarAsync(avisos, cancellationToken);

        return new ResultadoProcesamientoMensaje(EstadoProcesamientoMensaje.Procesado, notificaciones, avisos.Count);
    }

    private async Task<int> NotificarDestinatarioAsync(EventoEnvioPublicado evento, Domain.Entities.Envios.Envio envio, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EstadoEnvio>(evento.EstadoNuevo, ignoreCase: true, out var estado) || !EstadosNotificablesAlDestinatario.Contains(estado))
            return 0;

        var destinatario = (await _destinatarioRepository.GetByIdsAsync(new[] { envio.DestinatarioId }, cancellationToken)).FirstOrDefault();
        if (destinatario is null)
            return 0;

        // Canal según el dato de contacto disponible; sin contacto no hay a quién notificar
        var (canal, contacto) = !string.IsNullOrWhiteSpace(destinatario.Email)
            ? ("Email", destinatario.Email!)
            : !string.IsNullOrWhiteSpace(destinatario.Telefono)
                ? ("SMS", destinatario.Telefono!)
                : (string.Empty, string.Empty);

        if (canal.Length == 0)
            return 0;

        await _notificacionService.EnviarAsync(
            new NotificacionDestinatario(envio.Id, envio.CodigoSeguimientoPublico, destinatario.Nombre, canal, contacto, estado),
            cancellationToken);

        await _notificacionRepository.AddAsync(
            new Notificacion(Guid.NewGuid(), envio.Id, canal, evento.TipoEventoDominio, estado),
            cancellationToken);

        return 1;
    }

    private async Task<List<AvisoComercio>> GenerarAvisosAsync(EventoEnvioPublicado evento, Guid comercioOperadorId, CancellationToken cancellationToken)
    {
        var suscripciones = await _avisoRepository.ListarSuscripcionesActivasAsync(comercioOperadorId, cancellationToken);
        var avisos = new List<AvisoComercio>();

        foreach (var suscripcion in suscripciones.Where(s => CoincideConEvento(s, evento)))
        {
            var avisoId = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new
            {
                avisoId,
                evento = EventoEnvioPublicado.TipoMensaje,
                envioId = evento.EnvioId,
                codigoSeguimiento = evento.CodigoSeguimiento,
                estadoAnterior = evento.EstadoAnterior,
                estado = evento.EstadoNuevo,
                ocurridoEn = evento.OcurridoEnUtc
            }, EventoEnvioPublicado.OpcionesJson);

            var aviso = new AvisoComercio(avisoId, suscripcion.Id, evento.EnvioId, payload, FirmaWebhook.Calcular(suscripcion.ClaveFirma, payload));
            await _avisoRepository.AddAsync(aviso, cancellationToken);
            avisos.Add(aviso);
        }

        return avisos;
    }

    private static bool CoincideConEvento(SuscripcionAviso suscripcion, EventoEnvioPublicado evento) =>
        string.Equals(suscripcion.TipoEvento, TodosLosEventos, StringComparison.Ordinal)
        || string.Equals(suscripcion.TipoEvento, evento.EstadoNuevo, StringComparison.OrdinalIgnoreCase);
}
