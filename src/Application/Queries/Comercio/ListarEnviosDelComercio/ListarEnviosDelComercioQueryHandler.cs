using MediatR;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Queries.Comercio.ListarEnviosDelComercio;

public class ListarEnviosDelComercioQueryHandler : IRequestHandler<ListarEnviosDelComercioQuery, PaginaEnviosComercioDto>
{
    private const int TamanoMaximoPagina = 100;
    private const int LargoMaximoTexto = 100;

    private readonly IEnvioRepository _envioRepository;
    private readonly ICurrentTenant _currentTenant;

    public ListarEnviosDelComercioQueryHandler(IEnvioRepository envioRepository, ICurrentTenant currentTenant)
    {
        _envioRepository = envioRepository;
        _currentTenant = currentTenant;
    }

    public async Task<PaginaEnviosComercioDto> Handle(ListarEnviosDelComercioQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // El comercio se toma siempre del usuario autenticado: nunca de un parámetro del cliente (anti-IDOR)
        var comercioId = _currentTenant.ComercioOperadorId
            ?? throw new UnauthorizedAccessException("Acceso denegado: el usuario no pertenece a un comercio.");

        if (request.Pagina < 1)
            throw new DomainValidationException("La página debe ser mayor o igual a 1.");

        if (request.TamanoPagina < 1 || request.TamanoPagina > TamanoMaximoPagina)
            throw new DomainValidationException($"El tamaño de página debe estar entre 1 y {TamanoMaximoPagina}.");

        if (request.Desde.HasValue && request.Hasta.HasValue && request.Desde > request.Hasta)
            throw new DomainValidationException("La fecha desde no puede ser posterior a la fecha hasta.");

        if (request.Texto is { Length: > LargoMaximoTexto })
            throw new DomainValidationException($"El texto de búsqueda no puede superar los {LargoMaximoTexto} caracteres.");

        EstadoEnvio? estado = null;
        if (!string.IsNullOrWhiteSpace(request.Estado))
        {
            // Solo nombres de estado: un número suelto no debe colarse como valor del enum
            if (int.TryParse(request.Estado, out _) || !Enum.TryParse<EstadoEnvio>(request.Estado.Trim(), true, out var parseado) || !Enum.IsDefined(parseado))
                throw new DomainValidationException($"El estado '{request.Estado}' no es válido.");

            estado = parseado;
        }

        var filtro = new FiltroEnviosComercio(
            estado,
            request.Desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            request.Texto?.Trim(),
            request.Pagina,
            request.TamanoPagina);

        var (items, total) = await _envioRepository.BuscarPorComercioAsync(_currentTenant.OperadorId, comercioId, filtro, cancellationToken);

        return new PaginaEnviosComercioDto(
            items.Select(e => new EnvioComercioDto(
                e.Id, e.CodigoSeguimientoPublico, (int)e.EstadoActual, e.EstadoActual.ToString(),
                e.NombreDestinatario, e.DireccionDestino, e.ModalidadServicio, e.TarifaCalculada, e.FechaAlta)).ToList(),
            total,
            request.Pagina,
            request.TamanoPagina,
            (int)Math.Ceiling(total / (double)request.TamanoPagina));
    }
}
