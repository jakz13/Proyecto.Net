using MediatR;
using UltimaMilla.Application.DTOs;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Planificacion;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Application.Queries.ListarEnvios;

public class ListarEnviosQueryHandler : IRequestHandler<ListarEnviosQuery, List<EnvioDto>>
{
    private const int MaximoEnvios = 500;

    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRutaRepository? _rutaRepository;
    private readonly IRepartidorRepository? _repartidorRepository;
    private readonly IUsuarioRepository? _usuarioRepository;

    public ListarEnviosQueryHandler(
        IEnvioRepository envioRepository,
        ICurrentTenant currentTenant,
        IRutaRepository? rutaRepository = null,
        IRepartidorRepository? repartidorRepository = null,
        IUsuarioRepository? usuarioRepository = null)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
        _rutaRepository = rutaRepository;
        _repartidorRepository = repartidorRepository;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<List<EnvioDto>> Handle(ListarEnviosQuery request, CancellationToken cancellationToken)
    {
        // Solo los envíos del operador actual; un usuario de comercio ve únicamente los de su comercio
        var envios = await _envioRepository.ListarRecientesPorOperadorAsync(_currentTenant.OperadorId, MaximoEnvios, cancellationToken);

        var enviosFiltrados = envios
            .Where(e => !_currentTenant.ComercioOperadorId.HasValue || e.ComercioOperadorId == _currentTenant.ComercioOperadorId)
            .ToList();

        // Si tenemos repositorio de rutas, resolvemos las hojas de ruta y repartidores asignados
        var hojasPorId = new Dictionary<Guid, HojaDeRuta>();
        var repartidoresPorId = new Dictionary<Guid, Repartidor>();
        var usuariosPorId = new Dictionary<Guid, Usuario>();

        if (_rutaRepository != null)
        {
            var rutaIds = enviosFiltrados
                .Where(e => e.HojaDeRutaId.HasValue)
                .Select(e => e.HojaDeRutaId!.Value)
                .Distinct()
                .ToList();

            if (rutaIds.Count > 0)
            {
                var rutas = await _rutaRepository.GetByIdsAsync(rutaIds, cancellationToken);
                hojasPorId = rutas.ToDictionary(r => r.Id);

                if (_repartidorRepository != null)
                {
                    var repartidorIds = rutas
                        .Where(r => r.RepartidorId.HasValue)
                        .Select(r => r.RepartidorId!.Value)
                        .Distinct()
                        .ToList();

                    if (repartidorIds.Count > 0)
                    {
                        var repartidores = await _repartidorRepository.GetByIdsAsync(repartidorIds, cancellationToken);
                        repartidoresPorId = repartidores.ToDictionary(r => r.Id);

                        if (_usuarioRepository != null)
                        {
                            var usuarioIds = repartidores.Select(r => r.UsuarioId).Distinct().ToList();
                            if (usuarioIds.Count > 0)
                            {
                                var usuarios = await _usuarioRepository.ObtenerPorIdsAsync(usuarioIds, cancellationToken);
                                usuariosPorId = usuarios.ToDictionary(u => u.Id);
                            }
                        }
                    }
                }
            }
        }

        return enviosFiltrados
            .Select(e =>
            {
                string? hojaDeRutaNumero = null;
                string? repartidorNombre = null;

                if (e.HojaDeRutaId.HasValue && hojasPorId.TryGetValue(e.HojaDeRutaId.Value, out var hoja))
                {
                    hojaDeRutaNumero = HojaDeRutaMapper.NumeroRuta(hoja);

                    if (hoja.RepartidorId.HasValue && repartidoresPorId.TryGetValue(hoja.RepartidorId.Value, out var rep))
                    {
                        if (usuariosPorId.TryGetValue(rep.UsuarioId, out var usr) && !string.IsNullOrWhiteSpace(usr.Nombre))
                        {
                            repartidorNombre = usr.Nombre;
                        }
                        else if (!string.IsNullOrWhiteSpace(rep.Documento))
                        {
                            repartidorNombre = $"Doc. {rep.Documento}";
                        }
                    }
                }

                return new EnvioDto(
                    e.Id,
                    e.ComercioId,
                    e.DireccionDestino,
                    e.NombreDestinatario,
                    e.FechaCreacion,
                    e.Estado.ToString(),
                    e.CodigoSeguimientoPublico,
                    hojaDeRutaNumero,
                    repartidorNombre,
                    null,
                    e.PesoTotal > 0 ? e.PesoTotal : null);
            })
            .ToList();
    }
}
