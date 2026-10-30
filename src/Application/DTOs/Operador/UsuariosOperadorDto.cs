namespace UltimaMilla.Application.DTOs.Operador;

/// <summary>
/// DTO con la información detallada de un usuario del operador o comercio asociado (UC-01).
/// </summary>
public record UsuarioDetalleDto(
    Guid Id,
    string Nombre,
    string Email,
    Guid PerfilId,
    string NombrePerfil,
    Guid? OperadorId,
    Guid? ComercioId,
    string? NombreComercio,
    bool Activo,
    DateTime FechaAlta
);

/// <summary>
/// Contenedor paginado para los resultados de usuarios del operador.
/// </summary>
public record ResultadoPaginadoUsuariosDto(
    IReadOnlyList<UsuarioDetalleDto> Elementos,
    int TotalRegistros,
    int PaginaActual,
    int TamanoPagina
);

/// <summary>
/// DTO con la información de un perfil o rol disponible en el sistema (UC-01, RF-01).
/// </summary>
public record PerfilDetalleDto(
    Guid Id,
    string Nombre,
    string Descripcion
);

