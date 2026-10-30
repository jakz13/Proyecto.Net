using MediatR;

namespace UltimaMilla.Application.Queries.Deposito.ListarEnviosAdmitidos;

/// <summary>
/// Consulta los envíos en estado 'Admitido' que se encuentran pendientes de recepción física en depósito (UC-14, RF-10).
/// </summary>
public record ListarEnviosAdmitidosQuery() : IRequest<IReadOnlyList<EnvioAdmitidoDto>>;

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

public record BultoResumenDto(
    Guid BultoId,
    string Identificacion,
    decimal Peso,
    decimal Alto,
    decimal Ancho,
    decimal Profundo,
    string EstadoBulto);
