using MediatR;
using UltimaMilla.Application.Common;
using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Operador.GestionTarifas;

/// <summary>
/// Manejador que publica una nueva versión del cuadro tarifario, cerrando la vigencia de las versiones previas
/// e invalidando de inmediato las entradas del operador en el caché distribuido.
/// </summary>
public class PublicarVersionCuadroTarifarioCommandHandler : IRequestHandler<PublicarVersionCuadroTarifarioCommand, CuadroTarifarioDetalleDto>
{
    private readonly ICuadroTarifarioRepository _cuadroRepositorio;
    private readonly IZonaCoberturaRepository _zonaRepositorio;
    private readonly ICurrentTenant _inquilinoActual;
    private readonly IUnitOfWork _unidadDeTrabajo;
    private readonly IDistributedCacheService? _cacheService;

    public PublicarVersionCuadroTarifarioCommandHandler(
        ICuadroTarifarioRepository cuadroRepositorio,
        IZonaCoberturaRepository zonaRepositorio,
        ICurrentTenant inquilinoActual,
        IUnitOfWork unidadDeTrabajo,
        IDistributedCacheService? cacheService = null)
    {
        _cuadroRepositorio = cuadroRepositorio;
        _zonaRepositorio = zonaRepositorio;
        _inquilinoActual = inquilinoActual;
        _unidadDeTrabajo = unidadDeTrabajo;
        _cacheService = cacheService;
    }

    public async Task<CuadroTarifarioDetalleDto> Handle(PublicarVersionCuadroTarifarioCommand solicitud, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (_inquilinoActual.OperadorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Operador no identificado en la sesión.");
        }

        var operadorId = _inquilinoActual.OperadorId;

        if (solicitud.ReglasTarifa == null || solicitud.ReglasTarifa.Count == 0)
        {
            throw new DomainValidationException("La nueva versión debe incluir al menos una regla de tarifa.");
        }

        // Obtener zonas del operador para validar integridad referencial y Anti-IDOR
        var zonasDelOperador = await _zonaRepositorio.ListarZonasDelOperadorAsync(operadorId, null, null, cancellationToken);
        var mapaNombresZonas = zonasDelOperador.ToDictionary(z => z.Id, z => z.Nombre);

        foreach (var reglaDto in solicitud.ReglasTarifa)
        {
            if (reglaDto.ZonaCoberturaId.HasValue && !mapaNombresZonas.ContainsKey(reglaDto.ZonaCoberturaId.Value))
            {
                throw new DomainValidationException("Una o más reglas hacen referencia a una zona de cobertura inexistente o ajena al operador.");
            }
        }

        var fechaVigenteDesdeUtc = solicitud.VigenteDesde.Kind == DateTimeKind.Utc
            ? solicitud.VigenteDesde
            : DateTime.SpecifyKind(solicitud.VigenteDesde, DateTimeKind.Utc);

        // 1. Cerrar versiones actualmente abiertas del operador
        var versionesAbiertas = await _cuadroRepositorio.ListarVersionesAbiertasAsync(operadorId, cancellationToken);
        foreach (var abierta in versionesAbiertas)
        {
            var fechaFin = fechaVigenteDesdeUtc < abierta.VigenteDesde ? abierta.VigenteDesde : fechaVigenteDesdeUtc;
            abierta.CerrarVigencia(fechaFin);
            await _cuadroRepositorio.ActualizarAsync(abierta, cancellationToken);
        }

        // 2. Determinar número de versión consecutivo
        var ultimaVersion = await _cuadroRepositorio.ObtenerUltimaVersionAsync(operadorId, cancellationToken);
        var nuevaVersion = ultimaVersion + 1;

        // 3. Crear nuevo cuadro tarifario
        var nuevoCuadro = new CuadroTarifario(
            Guid.NewGuid(),
            operadorId,
            nuevaVersion,
            fechaVigenteDesdeUtc);

        nuevoCuadro.RegistrarComentario(solicitud.Comentario);

        // 4. Agregar reglas de precio
        foreach (var r in solicitud.ReglasTarifa)
        {
            var regla = new ReglaTarifa(
                Guid.NewGuid(),
                nuevoCuadro.Id,
                r.ModalidadServicio,
                r.PesoDesde,
                r.PesoHasta,
                r.VolumenDesde,
                r.VolumenHasta,
                r.PrecioBase,
                r.Recargo,
                r.Bonificacion,
                r.ZonaCoberturaId);

            nuevoCuadro.AgregarRegla(regla);
        }

        await _cuadroRepositorio.AgregarAsync(nuevoCuadro, cancellationToken);
        await _unidadDeTrabajo.SaveChangesAsync(cancellationToken);

        var dto = MapearADto(nuevoCuadro, mapaNombresZonas);

        // 5. Invalidar las entradas del operador en el caché distribuido para que todas las instancias de la API respondan de inmediato con la nueva versión
        if (_cacheService is not null)
        {
            await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioVigente(operadorId), cancellationToken);
            await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioHistorial(operadorId), cancellationToken);
            foreach (var abierta in versionesAbiertas)
            {
                await _cacheService.RemoveAsync(ClavesCache.CuadroTarifarioPorId(operadorId, abierta.Id), cancellationToken);
            }
        }

        return dto;
    }

    private static CuadroTarifarioDetalleDto MapearADto(CuadroTarifario cuadro, IReadOnlyDictionary<Guid, string> nombresZonas)
    {
        var reglasDto = cuadro.ReglasTarifa.Select(r => new ReglaTarifaDetalleDto(
            r.Id,
            r.CuadroTarifarioId,
            r.ZonaCoberturaId,
            r.ZonaCoberturaId.HasValue && nombresZonas.TryGetValue(r.ZonaCoberturaId.Value, out var nombre) ? nombre : null,
            r.ModalidadServicio,
            r.PesoDesde,
            r.PesoHasta,
            r.VolumenDesde,
            r.VolumenHasta,
            r.PrecioBase,
            r.Recargo,
            r.Bonificacion,
            r.CalcularPrecioFinal()
        )).ToList();

        var esVigente = cuadro.EstaVigenteEn(DateTime.UtcNow);

        return new CuadroTarifarioDetalleDto(
            cuadro.Id,
            cuadro.OperadorId,
            cuadro.Version,
            cuadro.VigenteDesde,
            cuadro.VigenteHasta,
            cuadro.Comentario,
            esVigente,
            reglasDto
        );
    }
}
