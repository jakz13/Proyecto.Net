namespace UltimaMilla.Application.DTOs.Operador;

/// <summary>
/// DTO con la información detallada de un comercio cliente y su relación con el operador (UC-01).
/// </summary>
public record ComercioDetalleDto(
    Guid Id,
    string RazonSocial,
    string NombreFantasia,
    string Rut,
    string Email,
    string Telefono,
    string Direccion,
    bool Activo,
    DateTime FechaAlta,
    string SelloModificacion
);

/// <summary>
/// Contenedor paginado para los resultados de comercios del operador.
/// </summary>
public record ResultadoPaginadoComerciosDto(
    IReadOnlyList<ComercioDetalleDto> Elementos,
    int TotalRegistros,
    int PaginaActual,
    int TamanoPagina
);

