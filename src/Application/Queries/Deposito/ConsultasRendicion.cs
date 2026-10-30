using MediatR;

namespace UltimaMilla.Application.Queries.Deposito;

/// <summary>
/// Consulta las hojas de ruta despachadas o finalizadas pendientes de rendición por el operario de depósito (UC-16, RF-22).
/// </summary>
public record ListarRutasPendientesRendicionQuery() : IRequest<IReadOnlyList<RutaPendienteRendicionResumenDto>>;

/// <summary>
/// Consulta el detalle de una hoja de ruta para contrastar bultos entregados, devueltos y sin intento en su rendición (UC-16).
/// </summary>
public record ObtenerRutaParaRendicionQuery(Guid HojaDeRutaId) : IRequest<RutaParaRendicionDetalleDto>;

public record RutaPendienteRendicionResumenDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string Estado,
    Guid? RepartidorId,
    string? RepartidorDocumento,
    Guid? VehiculoId,
    string? VehiculoPatente,
    string? VehiculoTipo,
    int CantidadParadas,
    int TotalBultos,
    int BultosEntregadosEstimados,
    int BultosRetornoEstimados,
    DateTime? FechaDespacho);

public record RutaParaRendicionDetalleDto(
    Guid HojaDeRutaId,
    string NumeroRuta,
    DateOnly Fecha,
    string Estado,
    Guid? RepartidorId,
    string? RepartidorDocumento,
    Guid? VehiculoId,
    string? VehiculoPatente,
    string? VehiculoTipo,
    int TotalParadas,
    int TotalBultos,
    int BultosEntregadosCount,
    int BultosEsperadosRetornoCount,
    bool TieneEnviosSinIntento,
    IReadOnlyList<EnvioRendicionDetalleDto> Envios,
    IReadOnlyList<MotivoInternoRendicionDto> MotivosInternosDisponibles);

public record EnvioRendicionDetalleDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Destinatario,
    string Direccion,
    string EstadoEnvio,
    bool EsEntregado,
    bool RequiereRetorno,
    bool EsRetornoSinIntento,
    IReadOnlyList<BultoRendicionDetalleDto> Bultos);

public record BultoRendicionDetalleDto(
    Guid BultoId,
    string Identificacion,
    decimal PesoKg,
    string EstadoBulto,
    bool CargadoEnVehiculo);

public record MotivoInternoRendicionDto(
    string Codigo,
    string Descripcion);
