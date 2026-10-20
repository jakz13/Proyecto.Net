using MediatR;

namespace UltimaMilla.Application.Queries.Comercio.ListarEnviosDelComercio;

/// <summary>Listado de los envíos del comercio autenticado, con filtros y paginación (UC-28).</summary>
public record ListarEnviosDelComercioQuery(
    string? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    string? Texto = null,
    int Pagina = 1,
    int TamanoPagina = 20
) : IRequest<PaginaEnviosComercioDto>;

public record PaginaEnviosComercioDto(
    IReadOnlyList<EnvioComercioDto> Items,
    int Total,
    int Pagina,
    int TamanoPagina,
    int TotalPaginas);

public record EnvioComercioDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    int Estado,
    string EstadoNombre,
    string Destinatario,
    string Direccion,
    string ModalidadServicio,
    decimal Tarifa,
    DateTime FechaAlta);
