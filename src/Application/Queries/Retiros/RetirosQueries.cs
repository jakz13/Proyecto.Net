using MediatR;
using UltimaMilla.Application.DTOs.Retiros;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Retiros;

/// <summary>Retiros del comercio autenticado, con su estado (portal del comercio).</summary>
public record ListarRetirosDelComercioQuery(EstadoRetiro? Estado = null, int Pagina = 1, int TamanoPagina = 20)
    : IRequest<PaginaRetirosDto>;

/// <summary>Retiros del operador agrupados por estado: alimenta el tablero de operación y los reportes.</summary>
public record ListarRetirosDelOperadorQuery(EstadoRetiro? Estado = null) : IRequest<ResumenRetirosOperadorDto>;

public record ObtenerDetalleRetiroQuery(Guid RetiroId) : IRequest<RetiroDetalleDto>;

public class ListarRetirosDelComercioQueryHandler : IRequestHandler<ListarRetirosDelComercioQuery, PaginaRetirosDto>
{
    private const int TamanoMaximo = 100;

    private readonly IRetiroRepository _retiros;
    private readonly ICurrentTenant _tenant;

    public ListarRetirosDelComercioQueryHandler(IRetiroRepository retiros, ICurrentTenant tenant)
    {
        _retiros = retiros;
        _tenant = tenant;
    }

    public async Task<PaginaRetirosDto> Handle(ListarRetirosDelComercioQuery request, CancellationToken cancellationToken)
    {
        // El segundo nivel de aislamiento: un comercio solo lista los retiros de su propia relación con el operador
        var comercioOperadorId = _tenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: esta consulta corresponde a un usuario de comercio.");

        var pagina = Math.Max(1, request.Pagina);
        var tamano = Math.Clamp(request.TamanoPagina, 1, TamanoMaximo);

        var (items, total) = await _retiros.BuscarPorComercioAsync(
            _tenant.OperadorId, comercioOperadorId, request.Estado, pagina, tamano, cancellationToken);

        return new PaginaRetirosDto(items.Select(RetiroMapper.Resumir).ToList(), total, pagina, tamano);
    }
}

public class ListarRetirosDelOperadorQueryHandler : IRequestHandler<ListarRetirosDelOperadorQuery, ResumenRetirosOperadorDto>
{
    private static readonly EstadoRetiro[] EstadosEnCurso =
        { EstadoRetiro.Solicitado, EstadoRetiro.AsignadoARuta, EstadoRetiro.Retirado, EstadoRetiro.NoRetirado, EstadoRetiro.EnDeposito };

    private readonly IRetiroRepository _retiros;
    private readonly ICurrentTenant _tenant;

    public ListarRetirosDelOperadorQueryHandler(IRetiroRepository retiros, ICurrentTenant tenant)
    {
        _retiros = retiros;
        _tenant = tenant;
    }

    public async Task<ResumenRetirosOperadorDto> Handle(ListarRetirosDelOperadorQuery request, CancellationToken cancellationToken)
    {
        var conteos = await _retiros.ContarPorEstadoAsync(_tenant.OperadorId, cancellationToken);
        var items = await _retiros.ListarPorOperadorAsync(_tenant.OperadorId, request.Estado, cancellationToken);

        var porEstado = Enum.GetValues<EstadoRetiro>()
            .ToDictionary(e => e.ToString(), e => conteos.TryGetValue(e, out var n) ? n : 0);

        return new ResumenRetirosOperadorDto(
            porEstado,
            porEstado.Values.Sum(),
            EstadosEnCurso.Sum(e => conteos.TryGetValue(e, out var n) ? n : 0),
            items.Select(RetiroMapper.Resumir).ToList());
    }
}

public class ObtenerDetalleRetiroQueryHandler : IRequestHandler<ObtenerDetalleRetiroQuery, RetiroDetalleDto>
{
    private readonly IRetiroRepository _retiros;
    private readonly ICurrentTenant _tenant;

    public ObtenerDetalleRetiroQueryHandler(IRetiroRepository retiros, ICurrentTenant tenant)
    {
        _retiros = retiros;
        _tenant = tenant;
    }

    public async Task<RetiroDetalleDto> Handle(ObtenerDetalleRetiroQuery request, CancellationToken cancellationToken)
    {
        if (request.RetiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro es inválido.");

        var retiro = await _retiros.GetByIdAsync(request.RetiroId, cancellationToken);

        // Otro operador, o el retiro de otro comercio, se informa como inexistente para no revelar que existe
        if (retiro is null
            || retiro.OperadorId != _tenant.OperadorId
            || (_tenant.ComercioOperadorId.HasValue && retiro.ComercioOperadorId != _tenant.ComercioOperadorId.Value))
        {
            throw new NotFoundException($"Retiro '{request.RetiroId}' no encontrado.");
        }

        return RetiroMapper.Detallar(retiro);
    }
}
