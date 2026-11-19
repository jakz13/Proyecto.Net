using MediatR;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Deposito;

/// <summary>
/// Manejador de la consulta para obtener el detalle operativo de una hoja de ruta para su rendición en depósito.
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
    private readonly IRetiroRepository? _retiroRepository;

    public ObtenerRutaParaRendicionQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IVehiculoRepository vehiculoRepository,
        IRepartidorRepository repartidorRepository,
        IMotivoNoEntregaRepository motivoNoEntregaRepository,
        ICurrentTenant currentTenant,
        IRetiroRepository? retiroRepository = null)
    {
        _retiroRepository = retiroRepository;
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
                .Where(b => b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito" or "Entregado" or "NoEntregado" or "PendienteReenvio")
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
                CargadoEnVehiculo: b.EstadoBulto is "CargadoEnVehiculo" or "EnRetornoADeposito" or "Entregado" or "NoEntregado" or "PendienteReenvio"
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

        var retiros = await ListarRetirosDeLaRutaAsync(ruta, cancellationToken);

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
            motivosInternos,
            retiros);
    }

    /// <summary>Retiros en domicilio de la hoja (si es mixta): lo retirado vuelve con el repartidor y el operario confirma su recepción.</summary>
    private async Task<IReadOnlyList<RetiroRendicionDetalleDto>> ListarRetirosDeLaRutaAsync(
        HojaDeRuta ruta, CancellationToken cancellationToken)
    {
        if (_retiroRepository is null)
            return Array.Empty<RetiroRendicionDetalleDto>();

        var ids = ruta.Paradas.Where(p => p.Estado != "Cancelada").IdsDeRetiros().Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<RetiroRendicionDetalleDto>();

        var retiros = await _retiroRepository.GetByIdsAsync(ids, cancellationToken);
        return retiros
            .Where(r => r.OperadorId == _currentTenant.OperadorId)
            .OrderBy(r => r.CodigoSeguimientoPublico)
            .Select(r => new RetiroRendicionDetalleDto(
                r.Id,
                r.CodigoSeguimientoPublico,
                r.NombreContacto,
                r.DireccionRetiro,
                r.EstadoActual.ToString(),
                r.Bultos.Count,
                r.PesoTotal,
                FueRetirado: r.EstadoActual is EstadoRetiro.Retirado or EstadoRetiro.EnDeposito or EstadoRetiro.EntregadoAlComercio,
                PendienteDeRecepcion: r.EstadoActual == EstadoRetiro.Retirado,
                YaRecibidoEnDeposito: r.EstadoActual is EstadoRetiro.EnDeposito or EstadoRetiro.EntregadoAlComercio,
                BultosAEsperar: r.Bultos.Select(b => b.Identificacion).OrderBy(c => c).ToList()))
            .ToList();
    }
}
