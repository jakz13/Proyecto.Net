using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador.GestionUsuarios;

/// <summary>
/// Consulta para listar usuarios del operador con filtros de texto, perfil, estado y paginación (UC-01, RF-01).
/// </summary>
public record ListarUsuariosQuery(
    string? Busqueda = null,
    Guid? PerfilId = null,
    bool? SoloActivos = null,
    int Pagina = 1,
    int TamanoPagina = 10
) : IRequest<ResultadoPaginadoUsuariosDto>;

