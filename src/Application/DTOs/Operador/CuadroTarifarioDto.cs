namespace UltimaMilla.Application.DTOs.Operador;

/// <summary>
/// DTO para la creación de una regla tarifaria dentro de un nuevo cuadro (UC-03, RF-03).
/// </summary>
public record CrearReglaTarifaDto(
    Guid? ZonaCoberturaId,
    string ModalidadServicio,
    decimal PesoDesde,
    decimal PesoHasta,
    decimal VolumenDesde,
    decimal VolumenHasta,
    decimal PrecioBase,
    decimal Recargo = 0,
    decimal Bonificacion = 0
);

/// <summary>
/// DTO con el detalle de una regla tarifaria configurada.
/// </summary>
public record ReglaTarifaDetalleDto(
    Guid Id,
    Guid CuadroTarifarioId,
    Guid? ZonaCoberturaId,
    string? NombreZona,
    string ModalidadServicio,
    decimal PesoDesde,
    decimal PesoHasta,
    decimal VolumenDesde,
    decimal VolumenHasta,
    decimal PrecioBase,
    decimal Recargo,
    decimal Bonificacion,
    decimal PrecioFinal
);

/// <summary>
/// DTO con la información completa y versionada de un cuadro tarifario (UC-03).
/// </summary>
public record CuadroTarifarioDetalleDto(
    Guid Id,
    Guid OperadorId,
    int Version,
    DateTime VigenteDesde,
    DateTime? VigenteHasta,
    string? Comentario,
    bool EsVigente,
    IReadOnlyList<ReglaTarifaDetalleDto> ReglasTarifa
)
{
    public int TotalReglas => ReglasTarifa.Count;
}

/// <summary>
/// DTO para simular el cálculo de cotización bajo la versión tarifaria vigente.
/// </summary>
public record SimularTarifaDto(
    decimal PesoKg,
    decimal VolumenM3,
    string ModalidadServicio,
    Guid? ZonaCoberturaId = null
);

/// <summary>
/// DTO con el resultado del cálculo simulado de tarifa.
/// </summary>
public record CotizacionSimuladaDto(
    bool Exitoso,
    decimal PrecioFinal,
    decimal PrecioBase,
    decimal Recargo,
    decimal Bonificacion,
    int VersionCuadroAplicada,
    string? ReglaAplicada,
    string? Mensaje
);

