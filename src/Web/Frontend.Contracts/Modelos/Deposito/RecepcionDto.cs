namespace UltimaMilla.Frontend.Contracts.Modelos.Deposito;

/// <summary>
/// Representa un envío admitido listo para su recepción física en el muelle de depósito (UC-14, RF-10).
/// </summary>
public record EnvioAdmitidoDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string NombreDestinatario,
    string DireccionDestino,
    string? TelefonoDestinatario,
    string ModalidadServicio,
    decimal PesoTotal,
    int CantidadBultos,
    DateTime FechaAlta,
    IReadOnlyList<BultoResumenDto> Bultos);

/// <summary>
/// Detalle del bulto declarado por el comercio.
/// </summary>
public record BultoResumenDto(
    Guid BultoId,
    string Identificacion,
    decimal Peso,
    decimal Alto,
    decimal Ancho,
    decimal Profundo,
    string EstadoBulto);

/// <summary>
/// Solicitud de confirmación de recepción en depósito enviada al backend.
/// </summary>
public record RecepcionarBultosRequestDto(
    IReadOnlyList<string> CodigosBultosEscaneados,
    IReadOnlyList<string>? CodigosBultosDanados = null,
    string? Observaciones = null);

/// <summary>
/// Resultado del proceso de contraste y recepción emitido por el backend.
/// </summary>
public record ResultadoRecepcionDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string Estado,
    bool Recepcionado,
    bool Idempotente,
    bool TieneDiscrepancias,
    IReadOnlyList<string> Correctos,
    IReadOnlyList<string> Faltantes,
    IReadOnlyList<string> Sobrantes,
    IReadOnlyList<string> Danados,
    string Mensaje);
