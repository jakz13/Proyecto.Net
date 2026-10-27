namespace UltimaMilla.Frontend.Contracts.Modelos.Deposito;

/// <summary>
/// Resumen de una hoja de ruta pendiente de rendición al regreso a depósito (UC-16).
/// </summary>
public record RutaPendienteRendicionDto(
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

/// <summary>
/// Detalle completo de una hoja de ruta para contrastar bultos y procesar la rendición (UC-16).
/// </summary>
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
    IReadOnlyList<EnvioRendicionDto> Envios,
    IReadOnlyList<MotivoInternoOpcionDto> MotivosInternosDisponibles);

/// <summary>
/// Información de un envío de la hoja de ruta durante la rendición.
/// </summary>
public record EnvioRendicionDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Destinatario,
    string Direccion,
    string EstadoEnvio,
    bool EsEntregado,
    bool RequiereRetorno,
    bool EsRetornoSinIntento,
    IReadOnlyList<BultoItemRendicionDto> Bultos);

/// <summary>
/// Bulto individual asociado a un envío que viajó en la hoja de ruta.
/// </summary>
public record BultoItemRendicionDto(
    Guid BultoId,
    string Identificacion,
    decimal PesoKg,
    string EstadoBulto,
    bool CargadoEnVehiculo);

/// <summary>
/// Motivo interno activo del catálogo del operador utilizable para envíos que volvieron sin intento.
/// </summary>
public record MotivoInternoOpcionDto(
    string Codigo,
    string Descripcion);

/// <summary>
/// Solicitud enviada para registrar la rendición de la hoja de ruta al regreso del repartidor.
/// </summary>
public record RendirHojaDeRutaRequestDto(
    IReadOnlyList<string> CodigosBultosDevueltos,
    string? MotivoInternoCodigo = null,
    string? Observaciones = null);

/// <summary>
/// Resultado del proceso de rendición de la hoja de ruta.
/// </summary>
public record ResultadoRendicionRutaDto(
    Guid HojaDeRutaId,
    string EstadoRuta,
    int BultosEntregados,
    int BultosDevueltos,
    int BultosPendientes,
    IReadOnlyList<string> EnviosReintegradosADeposito,
    IReadOnlyList<string> EnviosEnDevolucion,
    IReadOnlyList<string> EnviosPendientes,
    IReadOnlyList<string> CodigosSobrantes,
    bool Idempotente,
    string Mensaje);
