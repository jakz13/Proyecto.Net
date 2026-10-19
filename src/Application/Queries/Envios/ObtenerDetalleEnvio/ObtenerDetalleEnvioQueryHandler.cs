using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Envios.ObtenerDetalleEnvio;

public class ObtenerDetalleEnvioQueryHandler : IRequestHandler<ObtenerDetalleEnvioQuery, DetalleEnvioDto>
{
    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerDetalleEnvioQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<DetalleEnvioDto> Handle(ObtenerDetalleEnvioQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EnvioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío es inválido.");

        var envio = await _envioRepository.GetByIdAsync(request.EnvioId, cancellationToken)
            ?? throw new NotFoundException($"Envío '{request.EnvioId}' no encontrado.");

        // Anti-IDOR: el envío debe pertenecer al operador del usuario autenticado
        if (envio.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el envío no corresponde al operador actual.");

        return new DetalleEnvioDto(
            envio.Id,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual,
            envio.ModalidadServicio,
            envio.TarifaCalculada,
            envio.NombreDestinatario,
            envio.DireccionDestino,
            envio.HojaDeRutaId,
            envio.FechaAlta,
            envio.Bultos
                .OrderBy(b => b.Identificacion)
                .Select(b => new DetalleBultoDto(b.Id, b.Identificacion, b.Peso, b.EstadoBulto, b.Prioridad?.ToString()))
                .ToList(),
            envio.Eventos
                .OrderBy(e => e.Timestamp)
                .Select(e => new DetalleEventoDto(
                    e.Timestamp, e.TipoEvento, e.EstadoAnterior, e.EstadoNuevo, e.Origen,
                    e.ResponsableUsuarioId, e.Latitud, e.Longitud, e.Payload))
                .ToList(),
            envio.IntentosEntrega
                .OrderBy(i => i.Timestamp)
                .Select(i => new DetalleIntentoDto(
                    i.Id, i.ParadaId, i.Resultado, i.Timestamp, i.OrigenDatos, i.MotivoNoEntregaId, i.Latitud, i.Longitud,
                    i.PruebaDeEntrega is null
                        ? null
                        : new PruebaEntregaDto(
                            i.PruebaDeEntrega.Tipo,
                            i.PruebaDeEntrega.NombreReceptor,
                            i.PruebaDeEntrega.DocumentoReceptor,
                            !string.IsNullOrWhiteSpace(i.PruebaDeEntrega.FirmaImagenUrl),
                            !string.IsNullOrWhiteSpace(i.PruebaDeEntrega.FotoUrl),
                            i.PruebaDeEntrega.TimestampDispositivo)))
                .ToList());
    }
}
