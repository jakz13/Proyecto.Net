using MediatR;
using UltimaMilla.Application.DTOs.Mobile;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;

namespace UltimaMilla.Application.Queries.Mobile.ObtenerRutaActivaRepartidor;

public class ObtenerRutaActivaRepartidorQueryHandler : IRequestHandler<ObtenerRutaActivaRepartidorQuery, RutaActivaRepartidorDto>
{
    private static readonly string[] EstadosRutaActiva = { "Liberada", "EnCarga", "Despachada", "EnTransito" };

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IVehiculoRepository _vehiculoRepository;
    private readonly IDestinatarioRepository _destinatarioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRetiroRepository? _retiroRepository;

    public ObtenerRutaActivaRepartidorQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRepartidorRepository repartidorRepository,
        IVehiculoRepository vehiculoRepository,
        IDestinatarioRepository destinatarioRepository,
        IDireccionRepository direccionRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ICurrentTenant currentTenant,
        IRetiroRepository? retiroRepository = null)
    {
        _rutaRepository = rutaRepository ?? throw new ArgumentNullException(nameof(rutaRepository));
        _envioRepository = envioRepository ?? throw new ArgumentNullException(nameof(envioRepository));
        _repartidorRepository = repartidorRepository ?? throw new ArgumentNullException(nameof(repartidorRepository));
        _vehiculoRepository = vehiculoRepository ?? throw new ArgumentNullException(nameof(vehiculoRepository));
        _destinatarioRepository = destinatarioRepository ?? throw new ArgumentNullException(nameof(destinatarioRepository));
        _direccionRepository = direccionRepository ?? throw new ArgumentNullException(nameof(direccionRepository));
        _franjaHorariaRepository = franjaHorariaRepository ?? throw new ArgumentNullException(nameof(franjaHorariaRepository));
        _currentTenant = currentTenant ?? throw new ArgumentNullException(nameof(currentTenant));
        _retiroRepository = retiroRepository;
    }

    public async Task<RutaActivaRepartidorDto> Handle(ObtenerRutaActivaRepartidorQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuarioORepartidorId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario autenticado.");

        if (usuarioORepartidorId == Guid.Empty)
            throw new UnauthorizedAccessException("Acceso denegado: el identificador del usuario en el token es inválido.");

        var repartidor = await _repartidorRepository.GetByUsuarioIdAsync(usuarioORepartidorId, cancellationToken)
            ?? await _repartidorRepository.GetByIdAsync(usuarioORepartidorId, cancellationToken)
            ?? throw new NotFoundException("El usuario autenticado no tiene un perfil de repartidor registrado.");

        if (repartidor.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el repartidor no pertenece al operador actual.");

        var rutasActivas = await _rutaRepository.ListarAsync(
            _currentTenant.OperadorId,
            fecha: null,
            repartidorId: repartidor.Id,
            estados: EstadosRutaActiva,
            cancellationToken);

        var rutaResumen = rutasActivas
            .OrderBy(r => r.Estado switch
            {
                "EnTransito" => 0,
                "Despachada" => 1,
                "EnCarga" => 2,
                "Liberada" => 3,
                _ => 4
            })
            .ThenByDescending(r => r.Fecha)
            .FirstOrDefault()
            ?? throw new NotFoundException("El repartidor autenticado no posee ninguna hoja de ruta asignada (Liberada, Despachada o EnTransito).");

        var ruta = await _rutaRepository.GetByIdWithParadasYAsignacionesAsync(rutaResumen.Id, cancellationToken)
            ?? throw new NotFoundException($"Hoja de ruta '{rutaResumen.Id}' no encontrada.");

        var paradasVigentes = ruta.Paradas
            .Where(p => p.Estado != "Cancelada")
            .OrderBy(p => p.Orden)
            .ToList();

        var enviosPorRuta = await _envioRepository.GetByHojaDeRutaIdAsync(ruta.Id, cancellationToken);
        var enviosPorIds = paradasVigentes.Count > 0
            ? await _envioRepository.GetByIdsAsync(paradasVigentes.IdsDeEnvios(), cancellationToken)
            : new List<Envio>();

        var enviosDict = enviosPorRuta
            .Concat(enviosPorIds)
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var destinatarioIds = enviosDict.Values
            .Where(e => e.DestinatarioId != Guid.Empty)
            .Select(e => e.DestinatarioId)
            .Distinct()
            .ToList();

        var destinatariosDict = destinatarioIds.Count > 0
            ? (await _destinatarioRepository.GetByIdsAsync(destinatarioIds, cancellationToken)).ToDictionary(d => d.Id)
            : new Dictionary<Guid, Destinatario>();

        var direccionIds = enviosDict.Values
            .Where(e => e.DireccionEntregaId.HasValue && e.DireccionEntregaId.Value != Guid.Empty)
            .Select(e => e.DireccionEntregaId!.Value)
            .Concat(paradasVigentes.Where(p => p.DireccionId != Guid.Empty).Select(p => p.DireccionId))
            .Distinct()
            .ToList();

        var direccionesDict = direccionIds.Count > 0
            ? (await _direccionRepository.GetByIdsAsync(direccionIds, cancellationToken)).ToDictionary(d => d.Id)
            : new Dictionary<Guid, Direccion>();

        var franjaIds = paradasVigentes
            .Where(p => p.FranjaHorariaId.HasValue && p.FranjaHorariaId.Value != Guid.Empty)
            .Select(p => p.FranjaHorariaId!.Value)
            .Distinct()
            .ToList();

        var franjasDict = franjaIds.Count > 0
            ? (await _franjaHorariaRepository.GetByIdsAsync(franjaIds, cancellationToken)).ToDictionary(f => f.Id)
            : new Dictionary<Guid, Domain.Entities.Operador.FranjaHoraria>();

        var vehiculoId = ruta.VehiculoId ?? repartidor.VehiculoAsignadoId;
        var vehiculo = vehiculoId.HasValue && vehiculoId.Value != Guid.Empty
            ? await _vehiculoRepository.GetByIdAsync(vehiculoId.Value, cancellationToken)
            : null;

        var paradasDto = new List<ParadaNavegacionDto>();

        // Las paradas de retiro de una hoja de ruta mixta (RF 39) se muestran junto a las de entrega
        var retirosDict = _retiroRepository is null
            ? new Dictionary<Guid, Retiro>()
            : (await _retiroRepository.GetByIdsAsync(paradasVigentes.IdsDeRetiros(), cancellationToken))
                .Where(r => r.OperadorId == _currentTenant.OperadorId)
                .ToDictionary(r => r.Id);

        foreach (var parada in paradasVigentes)
        {
            Retiro? retiro = null;
            if (parada.RetiroId.HasValue)
                retirosDict.TryGetValue(parada.RetiroId.Value, out retiro);

            Envio? envio = null;
            if (parada.EnvioId.HasValue)
                enviosDict.TryGetValue(parada.EnvioId.Value, out envio);

            Destinatario? destinatario = null;
            if (envio is not null && envio.DestinatarioId != Guid.Empty)
            {
                destinatariosDict.TryGetValue(envio.DestinatarioId, out destinatario);
            }

            Direccion? direccion = null;
            if (envio?.DireccionEntregaId.HasValue == true)
            {
                direccionesDict.TryGetValue(envio.DireccionEntregaId.Value, out direccion);
            }
            if (direccion is null && parada.DireccionId != Guid.Empty)
            {
                direccionesDict.TryGetValue(parada.DireccionId, out direccion);
            }

            var codigoSeguimiento = envio?.CodigoSeguimientoPublico ?? retiro?.CodigoSeguimientoPublico ?? $"PAR-{parada.Orden:D2}";
            var direccionFormateada = !string.IsNullOrWhiteSpace(envio?.DireccionDestino)
                ? envio.DireccionDestino
                : (direccion?.ToString() ?? retiro?.DireccionRetiro ?? "Dirección sin especificar");

            (double? lat, double? lon) = (direccion?.Latitud, direccion?.Longitud);
            // Sin coordenadas registradas la parada no se geolocaliza: la app la muestra sin mapa en lugar de ubicarla en un lugar inventado

            string? ventanaHoraria = null;
            if (parada.FranjaHorariaId.HasValue && franjasDict.TryGetValue(parada.FranjaHorariaId.Value, out var franja))
            {
                ventanaHoraria = $"{franja.HoraInicio:HH\\:mm} - {franja.HoraFin:HH\\:mm}";
            }

            var contacto = new DestinatarioContactoDto(
                Nombre: destinatario?.Nombre ?? envio?.NombreDestinatario ?? retiro?.NombreContacto ?? "Destinatario no especificado",
                Telefono: destinatario?.Telefono ?? retiro?.TelefonoContacto,
                Email: destinatario?.Email ?? retiro?.EmailContacto,
                ReferenciaEntrega: direccion?.Referencia
            );

            var bultosVigentes = envio?.Bultos
                .Where(b => b.EstadoBulto is not "FaltanteEnRecepcion")
                .OrderBy(b => b.Identificacion)
                .ToList();

            var resumenBultos = retiro is not null
                ? new ResumenBultosParadaDto(
                    CantidadBultos: retiro.Bultos.Count,
                    CodigosBultos: retiro.Bultos.OrderBy(b => b.Identificacion, StringComparer.Ordinal).Select(b => b.Identificacion).ToList(),
                    PesoTotalKg: retiro.PesoTotal)
                : new ResumenBultosParadaDto(
                    CantidadBultos: bultosVigentes?.Count ?? 0,
                    CodigosBultos: bultosVigentes?.Select(b => b.Identificacion).ToList() ?? new List<string>(),
                    PesoTotalKg: bultosVigentes?.Sum(b => b.Peso) ?? 0m
                );

            bool esParadaActual = parada.Estado == "EnCamino" || parada.Estado == "EnSitio";

            paradasDto.Add(new ParadaNavegacionDto(
                ParadaId: parada.Id,
                EnvioId: parada.EnvioId ?? parada.RetiroId ?? Guid.Empty,
                Orden: parada.Orden,
                EstadoParada: parada.Estado,
                CodigoSeguimiento: codigoSeguimiento,
                DireccionFormateada: direccionFormateada,
                Latitud: lat,
                Longitud: lon,
                VentanaHoraria: ventanaHoraria,
                Destinatario: contacto,
                ResumenBultos: resumenBultos,
                EsParadaActual: esParadaActual,
                Tipo: parada.Tipo,
                RetiroId: parada.RetiroId
            ));
        }

        var motivos = await _envioRepository.ListarMotivosNoEntregaActivosAsync(_currentTenant.OperadorId, cancellationToken);
        var motivosDto = motivos
            .Select(m => new MotivoNoEntregaDescargaDto(m.Id, m.Codigo, m.Descripcion))
            .ToList();

        var numeroRuta = $"HR-{ruta.Fecha:yyyyMMdd}-01";

        return new RutaActivaRepartidorDto(
            HojaDeRutaId: ruta.Id,
            NumeroRuta: numeroRuta,
            Fecha: ruta.Fecha,
            Estado: ruta.Estado,
            RepartidorId: repartidor.Id,
            VehiculoId: vehiculoId,
            PatenteVehiculo: vehiculo?.Patente,
            TipoVehiculo: vehiculo is not null ? $"{vehiculo.Tipo} ({vehiculo.CapacidadPeso:F0} kg)" : null,
            Paradas: paradasDto,
            MotivosNoEntrega: motivosDto
        );
    }
}

