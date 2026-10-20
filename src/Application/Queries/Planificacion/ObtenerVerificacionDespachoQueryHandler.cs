using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Planificacion;

public class ObtenerVerificacionDespachoQueryHandler : IRequestHandler<ObtenerVerificacionDespachoQuery, VerificacionDespachoDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerVerificacionDespachoQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _currentTenant = currentTenant;
    }

    public async Task<VerificacionDespachoDto> Handle(ObtenerVerificacionDespachoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");

        var envios = (await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToList();

        var cargaRegistrada = ruta.EscaneosCarga.Count > 0;
        var bultos = envios
            .OrderBy(e => ruta.Paradas.FirstOrDefault(p => p.EnvioId == e.Id)?.Orden ?? int.MaxValue)
            .SelectMany(e => e.Bultos
                .Where(b => b.EstadoBulto is "Recibido" or "Danado" or "CargadoEnVehiculo")
                .OrderBy(b => b.Identificacion)
                .Select(b =>
                {
                    var cargado = ruta.BultoFueCargado(b.Id);
                    return new BultoVerificacionDto(
                        b.Id, e.Id, e.CodigoSeguimientoPublico, b.Identificacion, e.NombreDestinatario, e.DireccionDestino,
                        b.Peso, b.Volumen, cargado,
                        cargado ? null : cargaRegistrada
                            ? "El bulto no fue escaneado al cargar el vehículo."
                            : "Todavía no se registró el escaneo de carga.");
                }))
            .ToList();

        var vehiculo = ruta.VehiculoId.HasValue
            ? (await _vehiculoRepository.GetByIdsAsync(new[] { ruta.VehiculoId.Value }, cancellationToken)).FirstOrDefault()
            : null;

        var pendiente = ruta.RepartidorId.HasValue
            ? await _rutaRepository.GetRutaSinRendirAsync(ruta.RepartidorId.Value, ruta.Id, cancellationToken)
            : null;

        // Se puede despachar solo con la carga registrada, algún bulto cargado y sin otra ruta del repartidor pendiente de rendir
        var puedeDespachar = ruta.Estado == "EnCarga" && bultos.Any(b => b.CargadoEfectivo) && pendiente is null;

        return new VerificacionDespachoDto(
            ruta.Id,
            HojaDeRutaMapper.NumeroRuta(ruta),
            ruta.Fecha,
            ruta.Estado,
            ruta.RepartidorId,
            ruta.VehiculoId,
            vehiculo?.Patente,
            vehiculo?.CapacidadPeso ?? 0m,
            vehiculo?.CapacidadVolumen ?? 0m,
            pendiente is not null,
            pendiente is null ? null : HojaDeRutaMapper.NumeroRuta(pendiente),
            puedeDespachar,
            bultos);
    }
}
