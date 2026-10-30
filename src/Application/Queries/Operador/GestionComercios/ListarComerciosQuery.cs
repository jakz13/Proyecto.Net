using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionComercios;

/// <summary>
/// Consulta para listar comercios clientes del operador con filtros y paginación (UC-01).
/// </summary>
public record ListarComerciosQuery(
    string? Busqueda = null,
    bool? SoloActivos = null,
    int Pagina = 1,
    int TamanoPagina = 10
) : IRequest<ResultadoPaginadoComerciosDto>;

