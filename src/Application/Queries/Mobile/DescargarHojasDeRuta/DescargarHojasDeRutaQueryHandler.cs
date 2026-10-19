using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Mobile.DescargarHojasDeRuta;

public class DescargarHojasDeRutaQueryHandler : IRequestHandler<DescargarHojasDeRutaQuery, DescargaHojasDeRutaDto>
{
    // Estados en los que la hoja de ruta le corresponde al repartidor: liberada para cargar, cargando o ya en la calle
    private static readonly string[] EstadosDescargables = { "Liberada", "EnCarga", "Despachada", "EnTransito" };

    private readonly IRutaRepository _rutaRepository;
    private readonly IEnvioRepository _envioRepository;
    private readonly IRepartidorRepository _repartidorRepository;
    private readonly IDestinatarioRepository _destinatarioRepository;
    private readonly IDireccionRepository _direccionRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public DescargarHojasDeRutaQueryHandler(
        IRutaRepository rutaRepository,
        IEnvioRepository envioRepository,
        IRepartidorRepository repartidorRepository,
        IDestinatarioRepository destinatarioRepository,
        IDireccionRepository direccionRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _rutaRepository = rutaRepository;
        _envioRepository = envioRepository;
        _repartidorRepository = repartidorRepository;
        _destinatarioRepository = destinatarioRepository;
        _direccionRepository = direccionRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    public async Task<DescargaHojasDeRutaDto> Handle(DescargarHojasDeRutaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var usuarioId = _currentTenant.UsuarioId
            ?? throw new UnauthorizedAccessException("Acceso denegado: no se pudo identificar al usuario.");

        // Solo el repartidor dueño de las rutas puede descargarlas: se resuelve por el usuario autenticado, nunca por un id recibido
        var repartidor = await _repartidorRepository.GetByUsuarioIdAsync(usuarioId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario autenticado no es un repartidor.");

        if (repartidor.OperadorId != _currentTenant.OperadorId)
            throw new UnauthorizedAccessException("Acceso denegado: el repartidor no corresponde al operador actual.");

        var ahora = _clock.UtcNow;
        var fecha = request.Fecha ?? DateOnly.FromDateTime(ahora);

        var configuracion = await ObtenerConfiguracionAsync(cancellationToken);
        var rutas = await _rutaRepository.GetByRepartidorYFechaAsync(repartidor.Id, fecha, EstadosDescargables, cancellationToken);

        if (rutas.Count == 0)
        {
            return new DescargaHojasDeRutaDto(ahora, fecha, Array.Empty<HojaDeRutaDescargaDto>(), configuracion,
                "No hay hojas de ruta liberadas para la jornada indicada.");
        }

        var paradasVigentes = rutas.SelectMany(r => r.Paradas).Where(p => p.Estado != "Cancelada").ToList();
        var envios = (await _envioRepository.GetByIdsAsync(paradasVigentes.Select(p => p.EnvioId), cancellationToken))
            .Where(e => e.OperadorId == _currentTenant.OperadorId)
            .ToDictionary(e => e.Id);

        var destinatarios = (await _destinatarioRepository.GetByIdsAsync(envios.Values.Select(e => e.DestinatarioId), cancellationToken))
            .ToDictionary(d => d.Id);
        var direcciones = (await _direccionRepository.GetByIdsAsync(
                envios.Values.Where(e => e.DireccionEntregaId.HasValue).Select(e => e.DireccionEntregaId!.Value), cancellationToken))
            .ToDictionary(d => d.Id);
        var franjas = (await _franjaHorariaRepository.GetByIdsAsync(
                paradasVigentes.Where(p => p.FranjaHorariaId.HasValue).Select(p => p.FranjaHorariaId!.Value), cancellationToken))
            .ToDictionary(f => f.Id);

        var resultado = rutas
            .Select(ruta => new HojaDeRutaDescargaDto(
                ruta.Id,
                ruta.Fecha,
                ruta.Estado,
                ruta.VehiculoId,
                ruta.SelloModificacion,
                ruta.Paradas
                    .Where(p => p.Estado != "Cancelada" && envios.ContainsKey(p.EnvioId))
                    .OrderBy(p => p.Orden)
                    .Select(p => ConstruirParada(p, envios[p.EnvioId], destinatarios, direcciones, franjas))
                    .ToList()))
            .ToList();

        return new DescargaHojasDeRutaDto(ahora, fecha, resultado, configuracion,
            $"Se descargaron {resultado.Count} hoja(s) de ruta con {resultado.Sum(r => r.Paradas.Count)} parada(s).");
    }

    private async Task<ConfiguracionOfflineDto> ObtenerConfiguracionAsync(CancellationToken cancellationToken)
    {
        var regla = await _envioRepository.GetReglaOperativaVigenteAsync(_currentTenant.OperadorId, cancellationToken);
        var motivos = await _envioRepository.ListarMotivosNoEntregaActivosAsync(_currentTenant.OperadorId, cancellationToken);

        return new ConfiguracionOfflineDto(
            regla?.TipoPruebaEntregaExigida,
            regla?.MaxIntentosEntrega,
            motivos.Select(m => new MotivoNoEntregaDescargaDto(m.Id, m.Codigo, m.Descripcion)).ToList());
    }

    private static ParadaDescargaDto ConstruirParada(
        Parada parada,
        Envio envio,
        IReadOnlyDictionary<Guid, Destinatario> destinatarios,
        IReadOnlyDictionary<Guid, Direccion> direcciones,
        IReadOnlyDictionary<Guid, FranjaHoraria> franjas)
    {
        destinatarios.TryGetValue(envio.DestinatarioId, out var destinatario);
        Direccion? direccion = null;
        if (envio.DireccionEntregaId.HasValue)
            direcciones.TryGetValue(envio.DireccionEntregaId.Value, out direccion);

        string? ventana = null;
        if (parada.FranjaHorariaId.HasValue && franjas.TryGetValue(parada.FranjaHorariaId.Value, out var franja))
            ventana = $"{franja.HoraInicio:HH\\:mm}-{franja.HoraFin:HH\\:mm}";

        return new ParadaDescargaDto(
            parada.Id,
            envio.Id,
            parada.Orden,
            parada.Estado,
            envio.CodigoSeguimientoPublico,
            envio.EstadoActual.ToString(),
            envio.ModalidadServicio,
            envio.SelloModificacion,
            ventana,
            new DireccionDescargaDto(
                direccion?.ToString() ?? envio.DireccionDestino,
                direccion?.Latitud,
                direccion?.Longitud,
                direccion?.Referencia),
            new DestinatarioDescargaDto(
                destinatario?.Nombre ?? envio.NombreDestinatario,
                destinatario?.Telefono,
                destinatario?.Email),
            envio.Bultos
                .Where(b => b.EstadoBulto is not "FaltanteEnRecepcion")
                .OrderBy(b => b.Identificacion)
                .Select(b => new BultoDescargaDto(b.Id, b.Identificacion, b.Peso, b.Prioridad?.ToString()))
                .ToList());
    }
}
