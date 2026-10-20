using MediatR;

namespace UltimaMilla.Application.Queries.Incidencias;

/// <summary>Incidencias del operador con filtros opcionales por estado y tipo, paginadas.</summary>
public record ListarIncidenciasQuery(string? Estado = null, string? Tipo = null, int Pagina = 1, int TamanoPagina = 20)
    : IRequest<PaginaIncidenciasDto>;

/// <summary>Detalle de una incidencia con su bitácora.</summary>
public record ObtenerIncidenciaQuery(Guid IncidenciaId) : IRequest<IncidenciaDetalleDto>;

public record PaginaIncidenciasDto(
    IReadOnlyList<IncidenciaResumenDto> Items,
    int Total,
    int Pagina,
    int TamanoPagina,
    int TotalPaginas);

public record IncidenciaResumenDto(
    Guid Id,
    string Tipo,
    string Severidad,
    string Estado,
    string Descripcion,
    Guid? EnvioId,
    Guid? HojaDeRutaId,
    Guid? ResponsableUsuarioId,
    DateTime FechaApertura,
    DateTime? FechaCierre);

public record IncidenciaDetalleDto(
    Guid Id,
    string Tipo,
    string Severidad,
    string Estado,
    string Descripcion,
    Guid? EnvioId,
    Guid? HojaDeRutaId,
    Guid? ResponsableUsuarioId,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    string? Resolucion,
    IReadOnlyList<NotaIncidenciaDto> Bitacora);

public record NotaIncidenciaDto(DateTime Fecha, Guid UsuarioId, string Comentario);
