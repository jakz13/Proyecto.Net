using MediatR;

namespace UltimaMilla.Application.Queries.Comercio.GenerarEtiquetasEnvios;

/// <summary>
/// Genera los datos de las etiquetas físicas para uno o más envíos admitidos del comercio (UC-27).
/// </summary>
public record GenerarEtiquetasEnviosQuery(IReadOnlyList<Guid> EnvioIds) : IRequest<EtiquetasGeneradasResultDto>;

public record EtiquetasGeneradasResultDto(
    IReadOnlyList<EtiquetaBultoItemDto> Etiquetas,
    int TotalEnviosSeleccionados,
    int TotalEtiquetasGeneradas);

public record EtiquetaBultoItemDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string IdentificacionBulto,
    int NumeroBulto,
    int TotalBultos,
    string ComercioRemitente,
    string OperadorLogistico,
    string NombreDestinatario,
    string DireccionDestino,
    string? TelefonoDestinatario,
    string ModalidadServicio,
    decimal PesoKg,
    string? Observaciones,
    DateTime FechaAdmision);

