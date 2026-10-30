using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Deposito;

/// <summary>
/// Manejador de la consulta para obtener el detalle operativo de una hoja de ruta para su rendición en depósito (UC-16).
/// Expone bultos entregados, bultos que deben ser devueltos físicamente, envíos sin intento de entrega y motivos internos.
/// </summary>
public class ObtenerRutaParaRendicionQueryHandler : IRequestHandler<ObtenerRutaParaRendicionQuery, RutaParaRendicionDetalleDto>
{
    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IMotivoNoEntregaRepository _motivoNoEntregaRepository;
    private readonly ICurrentTenant _currentTenant;

    public ObtenerRutaParaRendicionQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        IMotivoNoEntregaRepository motivoNoEntregaRepository,
        ICurrentTenant currentTenant)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _vehiculoRepository = vehiculoRepository;
        _repartidorRepository = repartidorRepository;
        _motivoNoEntregaRepository = motivoNoEntregaRepository;
        _currentTenant = currentTenant;
    }

    public async Task<RutaParaRendicionDetalleDto> Handle(ObtenerRutaParaRendicionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HojaDeRutaId == Guid.Empty)
        {
            throw new DomainValidationException("El identificador de la hoja de ruta es inválido.");
        }

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(request.HojaDeRutaId, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{request.HojaDeRutaId}' no encontrada.");

        // Anti-IDOR: la ruta debe pertenecer al operador del usuario autenticado
        if (ruta.OperadorId != _currentTenant.OperadorId)
        {
            throw new UnauthorizedAccessException("Acceso denegado: la hoja de ruta no corresponde al operador actual.");
        }

        if (ruta.Estado is not ("Despachada" or "EnTransito" or "Finalizada" or "Rendida"))
        {
            throw new DomainValidationException($"Solo se puede consultar la rendición de una hoja de ruta despachada o rendida (estado actual: '{ruta.Estado}').");
        }

        var repartidor = ruta.RepartidorId.HasValue
            ? await _repartidorRepository.GetByIdAsync(ruta.RepartidorId.Value, cancellationToken)
            : null;

        var vehiculo = ruta.VehiculoId.HasValue
            ? await _vehiculoRepository.GetByIdAsync(ruta.VehiculoId.Value, cancellationToken)
            : null;

        var envios = (await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToList();

        var motivosInternos = (await _motivoNoEntregaRepository.ListarAsync(_currentTenant.OperadorId, soloParaRepartidor: false, cancellationToken))
            .Where(m => m.Activo && m.EsInterno)
            .OrderBy(m => m.Codigo)
            .Select(m => new MotivoInternoRendicionDto(m.Codigo, m.Descripcion))
            .ToList();

        var listaEnviosDto = new List<EnvioRendicionDetalleDto>();
        int bultosEntregadosTotal = 0;
        int bultosEsperadosRetornoTotal = 0;
        int totalBultosRuta = 0;

        foreach (var envio in envios)
        {
            var bultosQueViajaron = envio.Bultos
                .Where(b => b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito")
                .ToList();

            totalBultosRuta += bultosQueViajaron.Count;

            var esEntregado = envio.EstadoActual == EstadoEnvio.Entregado;
            var requiereRetorno = envio.EstadoActual is EstadoEnvio.EnDevolucion
                                                    or EstadoEnvio.Reprogramado
                                                    or EstadoEnvio.NoEntregado
                                                    or EstadoEnvio.EnTransito;
            var esRetornoSinIntento = envio.EstadoActual == EstadoEnvio.EnTransito;

            if (esEntregado)
            {
                bultosEntregadosTotal += bultosQueViajaron.Count;
            }
            else if (requiereRetorno)
            {
                bultosEsperadosRetornoTotal += bultosQueViajaron.Count;
            }

            var bultosDto = envio.Bultos.Select(b => new BultoRendicionDetalleDto(
                b.Id,
                b.Identificacion,
                b.Peso,
                b.EstadoBulto,
                CargadoEnVehiculo: b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito"
            )).ToList();

            listaEnviosDto.Add(new EnvioRendicionDetalleDto(
                envio.Id,
                envio.CodigoSeguimientoPublico,
                envio.NombreDestinatario,
                envio.DireccionDestino,
                envio.EstadoActual.ToString(),
                esEntregado,
                requiereRetorno,
                esRetornoSinIntento,
                bultosDto));
        }

        var tieneEnviosSinIntento = envios.Any(e => e.EstadoActual == EstadoEnvio.EnTransito);
        var numeroRuta = $"HR-{ruta.Fecha:yyyyMMdd}-{ruta.Id.ToString()[..4].ToUpperInvariant()}";
        var totalParadas = ruta.Paradas.Count(p => p.Estado != "Cancelada");

        return new RutaParaRendicionDetalleDto(
            ruta.Id,
            numeroRuta,
            ruta.Fecha,
            ruta.Estado,
            ruta.RepartidorId,
            repartidor?.Documento,
            ruta.VehiculoId,
            vehiculo?.Patente,
            vehiculo?.Tipo,
            totalParadas,
            totalBultosRuta,
            bultosEntregadosTotal,
            bultosEsperadosRetornoTotal,
            tieneEnviosSinIntento,
            listaEnviosDto,
            motivosInternos);
    }
}
