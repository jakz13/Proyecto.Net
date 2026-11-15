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
    private readonly IRetiroRepository? _retiroRepository;

    public ObtenerVerificacionDespachoQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        ICurrentTenant currentTenant,
        IRetiroRepository? retiroRepository = null)
    {
        _retiroRepository = retiroRepository;
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
                .Where(b => b.EstadoBulto != "FaltanteEnRecepcion")
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

        // Los retiros también son paradas de la ruta: se listan aunque no tengan bultos que cargar en el depósito
        var paradasRetiro = new List<ParadaRetiroVerificacionDto>();
        var paradasDeRetiro = ruta.Paradas.Where(p => p.EsRetiro && p.Estado != "Cancelada").OrderBy(p => p.Orden).ToList();
        if (paradasDeRetiro.Count > 0 && _retiroRepository is not null)
        {
            var retiros = (await _retiroRepository.GetByIdsAsync(paradasDeRetiro.Select(p => p.RetiroId!.Value).Distinct().ToList(), cancellationToken))
                .Where(r => r.OperadorId == _currentTenant.OperadorId)
                .ToDictionary(r => r.Id);
            foreach (var parada in paradasDeRetiro)
            {
                if (retiros.TryGetValue(parada.RetiroId!.Value, out var retiro))
                    paradasRetiro.Add(new ParadaRetiroVerificacionDto(
                        parada.Orden, retiro.Id, retiro.CodigoSeguimientoPublico, retiro.NombreContacto, retiro.DireccionRetiro,
                        retiro.Bultos.Count, retiro.PesoTotal, retiro.VolumenTotal));
            }
        }

        // Se puede despachar con la carga registrada y algún bulto cargado; una ruta con solo retiros no tiene nada que cargar y sale directamente.
        // En ambos casos, sin otra ruta del repartidor pendiente de rendir.
        var soloRetiros = paradasRetiro.Count > 0 && bultos.Count == 0;
        var puedeDespachar = pendiente is null
            && ((ruta.Estado == "EnCarga" && bultos.Any(b => b.CargadoEfectivo)) || (soloRetiros && ruta.Estado is "Liberada" or "EnCarga"));

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
            bultos,
            paradasRetiro);
    }
}
