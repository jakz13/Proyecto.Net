using System.Globalization;
using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Queries.Seguimiento.ListarFranjasDisponibles;

public class ListarFranjasDisponiblesQueryHandler : IRequestHandler<ListarFranjasDisponiblesQuery, FranjasDisponiblesDto?>
{
    private const int LargoMaximoCodigo = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;

    public ListarFranjasDisponiblesQueryHandler(
        IEnvioRepository envioRepository,
        IDireccionRepository direccionRepository,
        IFranjaHorariaRepository franjaHorariaRepository)
    {
        _envioRepository = envioRepository;
        _direccionRepository = direccionRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
    }

    public async Task<FranjasDisponiblesDto?> Handle(ListarFranjasDisponiblesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var codigo = request.CodigoSeguimiento?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(codigo) || codigo.Length > LargoMaximoCodigo)
            return null;

        var envio = await _envioRepository.GetByCodigoSeguimientoAsync(codigo, cancellationToken);
        if (envio is null)
            return null;

        var pendiente = envio.EstadoActual is EstadoEnvio.Admitido or EstadoEnvio.EnDeposito or EstadoEnvio.Reprogramado;
        if (!pendiente || !envio.DireccionEntregaId.HasValue)
            return new FranjasDisponiblesDto(false, Array.Empty<FranjaDisponibleDto>());

        var zonaId = (await _direccionRepository.GetByIdsAsync(new[] { envio.DireccionEntregaId.Value }, cancellationToken))
            .FirstOrDefault()?.ZonaCoberturaId;
        if (!zonaId.HasValue)
            return new FranjasDisponiblesDto(false, Array.Empty<FranjaDisponibleDto>());

        var franjas = (await _franjaHorariaRepository.ListarActivasPorZonaAsync(zonaId.Value, cancellationToken))
            .Select(f => new FranjaDisponibleDto(
                f.Id,
                f.DiaSemana.ToString(),
                f.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture),
                f.HoraFin.ToString("HH:mm", CultureInfo.InvariantCulture),
                f.Id == envio.FranjaHorariaComprometidaId))
            .ToList();

        return new FranjasDisponiblesDto(franjas.Count > 0, franjas);
    }
}
