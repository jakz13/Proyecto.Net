namespace UltimaMilla.Frontend.Contracts.Modelos.Deposito;

/// <summary>
/// Representa un envío recepcionado en depósito para clasificación o inclusión en rutas (UC-15).
/// </summary>
public record EnvioEnDepositoDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string NombreDestinatario,
    string DireccionDestino,
    string ModalidadServicio,
    decimal PesoTotal,
    int CantidadBultos,
    int CantidadClasificados,
    bool EstaClasificado,
    DateTime FechaAlta,
    IReadOnlyList<BultoClasificadoResumenDto> Bultos);

/// <summary>
/// Detalle del bulto físico recepcionado con su clasificación actual.
/// </summary>
public record BultoClasificadoResumenDto(
    Guid BultoId,
    string Identificacion,
    decimal Peso,
    string EstadoBulto,
    bool EstaClasificado,
    string? Prioridad,
    Guid? ZonaClasificadaId,
    string? TipoClasificacion,
    DateTime? FechaClasificacion);

/// <summary>
/// Solicitud de clasificación de uno o varios bultos de un envío en depósito (UC-15).
/// </summary>
public record ClasificarBultosRequestDto(
    IReadOnlyList<ItemClasificacionBultoDto> Bultos);

/// <summary>
/// Criterio operativo asignado a un bulto particular: zona, prioridad y tipo.
/// </summary>
public record ItemClasificacionBultoDto(
    string CodigoBulto,
    string Prioridad,
    Guid? ZonaCoberturaId = null,
    string? Tipo = null);

/// <summary>
/// Resultado del proceso de clasificación emitido por el backend.
/// </summary>
public record ResultadoClasificacionDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Estado,
    int TotalBultosRecibidos,
    int TotalClasificados,
    IReadOnlyList<string> BultosPendientes,
    bool ListoParaHojaDeRuta);
