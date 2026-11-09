namespace UltimaMilla.Application.DTOs.Mobile;

/// <summary>Detalle del bulto que no pudo entregarse en la parada junto con el motivo del catálogo del operador (RF-32).</summary>
public record BultoNoEntregadoRequestDto(
    string CodigoBulto,
    string? MotivoCodigo = null,
    Guid? MotivoNoEntregaId = null,
    string? Observaciones = null);

public record EntregaExitosaResultDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoParada,
    DateTime FechaHoraEntregaUtc,
    bool Idempotente,
    string Mensaje,
    int TotalBultos = 0,
    int BultosEntregados = 0,
    int BultosPendientes = 0,
    IReadOnlyList<string>? CodigosBultosEntregados = null,
    IReadOnlyList<string>? CodigosBultosNoEntregados = null,
    IReadOnlyList<string>? ConflictosResueltos = null,
    bool ConflictoResuelto = false
);

/// <summary>Resultado detallado del registro de una entrega parcial o por bultos en una parada (RF-31 a RF-34).</summary>
public record EntregaParcialResultDto(
    Guid EnvioId,
    Guid HojaDeRutaId,
    Guid ParadaId,
    string CodigoSeguimiento,
    string EstadoEnvio,
    string EstadoParada,
    int TotalBultos,
    int BultosEntregados,
    int BultosPendientes,
    string AvanceEntrega,
    string PoliticaAplicada,
    bool RecargoAplicado,
    IReadOnlyList<string> CodigosBultosEntregados,
    IReadOnlyList<string> CodigosBultosNoEntregados,
    IReadOnlyList<string> ConflictosResueltos,
    DateTime FechaHoraEntregaUtc,
    bool Idempotente,
    string Mensaje
);
