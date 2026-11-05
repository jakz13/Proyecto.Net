using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Messaging;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Mensajeria.ProcesarMensaje;

public class ProcesarMensajeCommandHandler : IRequestHandler<ProcesarMensajeCommand, ResultadoProcesamientoMensaje>
{
    /// <summary>Cambios de estado que justifican avisar al destinatario sin que tenga que consultar (UC-35, RF-35).</summary>
    private static readonly HashSet<EstadoEnvio> EstadosNotificablesAlDestinatario = new()
    {
        EstadoEnvio.Admitido,
        EstadoEnvio.EnTransito,
        EstadoEnvio.Entregado,
        EstadoEnvio.EntregadoParcialmente,
        EstadoEnvio.NoEntregado,
        EstadoEnvio.Reprogramado
    };

    /// <summary>Valor de suscripción que recibe avisos de cualquier cambio de estado o evento de bulto.</summary>
    public const string TodosLosEventos = "*";

    private readonly IIdempotenciaConsumidorRepository _idempotenciaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IDestinatarioRepository _destinatarioRepository;
    private readonly INotificacionRepository _notificacionRepository;
    private readonly INotificacionDestinatarioService _notificacionService;
    private readonly IAvisoComercioRepository _avisoRepository;
    private readonly AvisoComercioEntregador _entregador;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenant? _currentTenant;
    private readonly IDistributedCacheService? _cacheService;

    public ProcesarMensajeCommandHandler(
        IIdempotenciaConsumidorRepository idempotenciaRepository,
        IEnvioRepository envioRepository,
        IDestinatarioRepository destinatarioRepository,
        INotificacionRepository notificacionRepository,
        INotificacionDestinatarioService notificacionService,
        IAvisoComercioRepository avisoRepository,
        AvisoComercioEntregador entregador,
        IUnitOfWork unitOfWork,
        ICurrentTenant? currentTenant = null,
        IDistributedCacheService? cacheService = null)
    {
        _idempotenciaRepository = idempotenciaRepository;
        _envioRepository = envioRepository;
        _destinatarioRepository = destinatarioRepository;
        _notificacionRepository = notificacionRepository;
        _notificacionService = notificacionService;
        _avisoRepository = avisoRepository;
        _entregador = entregador;
        _unitOfWork = unitOfWork;
        _currentTenant = currentTenant;
        _cacheService = cacheService;
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

        // Fija el operador declarado por el evento en el alcance actual antes de consultar la base
        _currentTenant?.EstablecerOperadorDeProceso(evento.OperadorId);

        var envio = await _envioRepository.GetByIdAsync(evento.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"No se encontró el envío '{evento.EnvioId}' del evento.");

        // Defensa de aislamiento: el operador declarado por el evento debe ser el dueño real del envío
        if (envio.OperadorId != evento.OperadorId)
            throw new MensajeInvalidoException($"El envío '{evento.EnvioId}' no pertenece al operador del evento.");

        // Invalidación del caché de seguimiento público al procesar un evento del envío (RNF 6.7)
        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorCodigo(envio.CodigoSeguimientoPublico), cancellationToken);
            await _cacheService.RemoveAsync(ClavesCache.SeguimientoPorOperadorYCodigo(envio.OperadorId, envio.CodigoSeguimientoPublico), cancellationToken);
            await _cacheService.RemoveAsync(ClavesCache.EnvioPorId(envio.Id), cancellationToken);
        }

        var notificaciones = await NotificarDestinatarioAsync(evento, envio, cancellationToken);
        var avisos = await GenerarAvisosAsync(evento, envio, envio.ComercioOperadorId, cancellationToken);

        // Efectos y clave de idempotencia se confirman juntos: o quedan todos o no queda ninguno
        await _idempotenciaRepository.AddAsync(
            new ClaveIdempotenciaConsumidor(Guid.NewGuid(), claveMensaje, $"notificaciones={notificaciones};avisos={avisos.Count}"),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // La entrega HTTP va después de persistir: si falla, el aviso queda en reintento y no se pierde ni bloquea el mensaje
        await _entregador.EntregarAsync(avisos, cancellationToken);

        return new ResultadoProcesamientoMensaje(EstadoProcesamientoMensaje.Procesado, notificaciones, avisos.Count);
    }

    private async Task<int> NotificarDestinatarioAsync(EventoEnvioPublicado evento, Envio envio, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EstadoEnvio>(evento.EstadoNuevo, ignoreCase: true, out var estado) || !EstadosNotificablesAlDestinatario.Contains(estado))
            return 0;

        var destinatario = (await _destinatarioRepository.GetByIdsAsync(new[] { envio.DestinatarioId }, cancellationToken)).FirstOrDefault();
        if (destinatario is null)
            return 0;

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

    private async Task<List<AvisoComercio>> GenerarAvisosAsync(
        EventoEnvioPublicado evento,
        Envio envio,
        Guid comercioOperadorId,
        CancellationToken cancellationToken)
    {
        var suscripciones = await _avisoRepository.ListarSuscripcionesActivasAsync(comercioOperadorId, cancellationToken);
        var avisos = new List<AvisoComercio>();

        var bultosEvento = evento.Bultos is { Count: > 0 }
            ? evento.Bultos
            : envio.Bultos
                .Where(b => b.EstadoBulto != MaquinaEstadosBulto.FaltanteEnRecepcion)
                .Select(b => new EventoBultoPublicado(b.Identificacion, b.EstadoBulto, b.MotivoNoEntregaCodigo))
                .ToList();

        foreach (var suscripcion in suscripciones.Where(s => CoincideConEvento(s, evento)))
        {
            var avisoId = Guid.NewGuid();
            string payload;

            if (string.Equals(suscripcion.VersionContrato, SuscripcionAviso.ContratoV2, StringComparison.OrdinalIgnoreCase))
            {
                int totalBultos = bultosEvento.Count;
                int bultosEntregados = bultosEvento.Count(b => string.Equals(b.EstadoBulto, MaquinaEstadosBulto.Entregado, StringComparison.Ordinal));
                int bultosPendientes = Math.Max(0, totalBultos - bultosEntregados);

                // Contrato v2 (RF-36): incorpora versión explícita, resumen de avance y eventos a nivel de bulto
                payload = JsonSerializer.Serialize(new
                {
                    versionContrato = SuscripcionAviso.ContratoV2,
                    avisoId,
                    evento = EventoEnvioPublicado.TipoMensaje,
                    tipoEventoDominio = evento.TipoEventoDominio,
                    envioId = evento.EnvioId,
                    codigoSeguimiento = evento.CodigoSeguimiento,
                    estadoAnterior = evento.EstadoAnterior,
                    estado = evento.EstadoNuevo,
                    ocurridoEn = evento.OcurridoEnUtc,
                    resumenBultos = new
                    {
                        totalBultos,
                        bultosEntregados,
                        bultosPendientes,
                        avance = $"{bultosEntregados} de {totalBultos} bultos entregados"
                    },
                    bultos = bultosEvento.Select(b => new
                    {
                        identificacion = b.Identificacion,
                        estado = b.EstadoBulto,
                        motivoNoEntrega = b.MotivoNoEntrega
                    }).ToList()
                }, EventoEnvioPublicado.OpcionesJson);
            }
            else
            {
                // Contrato v1 (RF-36): preserva intacto el formato original para no afectar integraciones existentes
                payload = JsonSerializer.Serialize(new
                {
                    avisoId,
                    evento = EventoEnvioPublicado.TipoMensaje,
                    envioId = evento.EnvioId,
                    codigoSeguimiento = evento.CodigoSeguimiento,
                    estadoAnterior = evento.EstadoAnterior,
                    estado = evento.EstadoNuevo,
                    ocurridoEn = evento.OcurridoEnUtc
                }, EventoEnvioPublicado.OpcionesJson);
            }

            var aviso = new AvisoComercio(avisoId, suscripcion.Id, evento.EnvioId, payload, FirmaWebhook.Calcular(suscripcion.ClaveFirma, payload));
            await _avisoRepository.AddAsync(aviso, cancellationToken);
            avisos.Add(aviso);
        }

        return avisos;
    }

    private static bool CoincideConEvento(SuscripcionAviso suscripcion, EventoEnvioPublicado evento)
    {
        var tipoSuscripto = NormalizarTipoEventoSuscripto(suscripcion.TipoEvento);

        return string.Equals(tipoSuscripto, TodosLosEventos, StringComparison.Ordinal)
            || string.Equals(tipoSuscripto, evento.EstadoNuevo, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoSuscripto, evento.TipoEventoDominio, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(tipoSuscripto, "EventoBulto", StringComparison.OrdinalIgnoreCase)
                && (evento.Bultos is { Count: > 0 } || string.Equals(evento.EstadoNuevo, nameof(EstadoEnvio.EntregadoParcialmente), StringComparison.OrdinalIgnoreCase)));
    }

    private static string NormalizarTipoEventoSuscripto(string tipoEvento)
    {
        if (string.IsNullOrWhiteSpace(tipoEvento))
            return string.Empty;

        var limpio = tipoEvento.Trim();
        if (limpio.StartsWith("v1:", StringComparison.OrdinalIgnoreCase) || limpio.StartsWith("v2:", StringComparison.OrdinalIgnoreCase))
            return limpio[3..].Trim();

        return limpio;
    }
}
