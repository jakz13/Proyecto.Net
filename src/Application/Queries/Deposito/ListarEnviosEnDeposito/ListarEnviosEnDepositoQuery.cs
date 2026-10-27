using MediatR;

namespace UltimaMilla.Application.Queries.Deposito.ListarEnviosEnDeposito;

/// <summary>
/// Consulta los envíos en estado 'EnDeposito' disponibles para clasificación de bultos o inclusión en rutas (UC-15).
/// </summary>
public record ListarEnviosEnDepositoQuery() : IRequest<IReadOnlyList<EnvioEnDepositoDto>>;

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
