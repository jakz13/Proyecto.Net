using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionZonas;

/// <summary>
/// Consulta para listar zonas de cobertura del operador con filtros de texto y estado (UC-02).
/// </summary>
public record ListarZonasCoberturaQuery(
    string? Busqueda = null,
    bool? SoloActivas = null
) : IRequest<IReadOnlyList<ZonaCoberturaDetalleDto>>;

