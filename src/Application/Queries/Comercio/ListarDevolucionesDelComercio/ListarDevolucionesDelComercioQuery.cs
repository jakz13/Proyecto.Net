using MediatR;

namespace UltimaMilla.Application.Queries.Comercio.ListarDevolucionesDelComercio;

/// <summary>Devoluciones del comercio autenticado. Por defecto las abiertas; con SoloCerradas, las ya confirmadas (UC-32).</summary>
public record ListarDevolucionesDelComercioQuery(
    bool SoloCerradas = false,
    int Pagina = 1,
    int TamanoPagina = 20
) : IRequest<PaginaDevolucionesDto>;

public record PaginaDevolucionesDto(
    IReadOnlyList<DevolucionComercioDto> Items,
    int Total,
    int Pagina,
    int TamanoPagina,
    int TotalPaginas);

public record DevolucionComercioDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Destinatario,
    string EstadoEnvio,
    string EstadoDevolucion,
    DateTime? FechaInicio,
    DateTime? FechaCierre,
    bool PuedeConfirmarRecepcion);
