namespace UltimaMilla.Frontend.Contracts.Modelos;

/// <summary>
/// Envoltorio genérico para resultados paginados devueltos por la API.
/// </summary>
public record ResultadoPaginadoDto<T>
{
    public IReadOnlyList<T> Elementos { get; init; } = Array.Empty<T>();
    public int TotalRegistros { get; init; }
    public int PaginaActual { get; init; } = 1;
    public int TamanoPagina { get; init; } = 10;
    public int TotalPaginas => TamanoPagina > 0 ? (int)Math.Ceiling((double)TotalRegistros / TamanoPagina) : 0;
    public bool TienePaginaAnterior => PaginaActual > 1;
    public bool TienePaginaSiguiente => PaginaActual < TotalPaginas;
}
