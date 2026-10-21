using System.Globalization;
using System.Text.Json;
using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Seguimiento.SolicitarReprogramacion;

public class SolicitarReprogramacionCommandHandler : IRequestHandler<SolicitarReprogramacionCommand, ResultadoReprogramacionDto>
{
    private const int LargoMaximoCodigo = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly ISolicitudReprogramacionRepository _solicitudRepository;
    private readonly IOutboxStorage _outboxStorage;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SolicitarReprogramacionCommandHandler(
        IEnvioRepository envioRepository,
        IDireccionRepository direccionRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ISolicitudReprogramacionRepository solicitudRepository,
        IOutboxStorage outboxStorage,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _envioRepository = envioRepository;
        _direccionRepository = direccionRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _solicitudRepository = solicitudRepository;
        _outboxStorage = outboxStorage;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResultadoReprogramacionDto> Handle(SolicitarReprogramacionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Mismo mensaje para un código mal formado y uno inexistente: no se permite enumerar envíos
        var codigo = request.CodigoSeguimiento?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo) || codigo.Length > LargoMaximoCodigo)
            throw new NotFoundException("No se encontró un envío con el código indicado.");

        var consultado = await _envioRepository.GetByCodigoSeguimientoAsync(codigo, cancellationToken)
            ?? throw new NotFoundException("No se encontró un envío con el código indicado.");

        // La consulta por código no rastrea la entidad; se vuelve a leer para poder modificarla
        var envio = await _envioRepository.GetByIdAsync(consultado.Id, cancellationToken)
            ?? throw new NotFoundException("No se encontró un envío con el código indicado.");

        var solicitud = new SolicitudReprogramacion(
            Guid.NewGuid(), envio.Id, request.FranjaHorariaId.ToString(), request.OrigenIp, fechaSolicitud: _clock.UtcNow);

        var evaluacion = await EvaluarAsync(envio, request.FranjaHorariaId, cancellationToken);

        if (evaluacion.Motivo is not null)
        {
            solicitud.Rechazar(evaluacion.Motivo);
        }
        else
        {
            envio.ReprogramarFranjaPorDestinatario(request.FranjaHorariaId);
            solicitud.Aprobar();
            await _envioRepository.UpdateAsync(envio, cancellationToken);

            // Permite avisar al comercio y al despachador del cambio de franja
            await _outboxStorage.SaveRangeAsync(new[]
            {
                new OutboxMessage(Guid.NewGuid(), nameof(Domain.Entities.Envios.Envio), envio.Id, "ReprogramacionAceptadaIntegrationEvent",
                    JsonSerializer.Serialize(new { EnvioId = envio.Id, FranjaHorariaId = request.FranjaHorariaId, Timestamp = _clock.UtcNow }))
            }, cancellationToken);
        }

        await _solicitudRepository.AddAsync(solicitud, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return evaluacion.Motivo is null
            ? new ResultadoReprogramacionDto(true, solicitud.Estado, null, Describir(evaluacion.Franja!))
            : new ResultadoReprogramacionDto(false, solicitud.Estado, evaluacion.Motivo, null);
    }

    /// <summary>Aplica las reglas del operador. Devuelve el motivo del rechazo, o la franja elegida si se acepta.</summary>
    private async Task<(string? Motivo, FranjaHoraria? Franja)> EvaluarAsync(
        Domain.Entities.Envios.Envio envio, Guid franjaId, CancellationToken cancellationToken)
    {
        if (envio.EstadoActual is EstadoEnvio.AsignadoARuta or EstadoEnvio.EnTransito)
            return ("El envío ya está en preparación para salir a reparto: no admite cambios de franja.", null);

        if (envio.EstadoActual is not (EstadoEnvio.Admitido or EstadoEnvio.EnDeposito or EstadoEnvio.Reprogramado))
            return ("El envío ya no está pendiente de entrega.", null);

        var regla = await _envioRepository.GetReglaOperativaVigenteAsync(envio.OperadorId, cancellationToken);
        if (regla is not null)
        {
            var reprogramaciones = await _solicitudRepository.ContarAprobadasAsync(envio.Id, cancellationToken);
            if (regla.SuperaMaximoIntentos(envio.CantidadIntentos + reprogramaciones))
                return ("Se alcanzó el máximo de reprogramaciones permitido para este envío.", null);
        }

        if (envio.FranjaHorariaComprometidaId == franjaId)
            return ("La franja elegida es la que ya tiene asignada.", null);

        var zonaId = envio.DireccionEntregaId.HasValue
            ? (await _direccionRepository.GetByIdsAsync(new[] { envio.DireccionEntregaId.Value }, cancellationToken)).FirstOrDefault()?.ZonaCoberturaId
            : null;
        if (!zonaId.HasValue)
            return ("No hay franjas alternativas disponibles para este envío.", null);

        var franja = (await _franjaHorariaRepository.GetByIdsAsync(new[] { franjaId }, cancellationToken)).FirstOrDefault();
        if (franja is null || !franja.Activa || franja.ZonaCoberturaId != zonaId.Value)
            return ("La franja elegida no está disponible para su domicilio.", null);

        return (null, franja);
    }

    private static string Describir(FranjaHoraria franja) =>
        $"{franja.DiaSemana} {franja.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture)}-{franja.HoraFin.ToString("HH:mm", CultureInfo.InvariantCulture)}";
}
