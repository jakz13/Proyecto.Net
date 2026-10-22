using MediatR;
using UltimaMilla.Application.DTOs.Operador;

namespace UltimaMilla.Application.Queries.Operador;

public record ListarReglasOperativasQuery : IRequest<IReadOnlyList<ReglaOperativaDto>>;

public record ObtenerReglaOperativaVigenteQuery : IRequest<ReglaOperativaDto?>;

public record ObtenerReglaOperativaQuery(Guid ReglaId) : IRequest<ReglaOperativaDto>;

public record SimularReglaOperativaQuery(
    string ModalidadServicio,
    DateTime FechaDespacho,
    int IntentosRealizados,
    DateTime? FechaUltimoIntento) : IRequest<ResultadoSimulacionReglasDto>;

public record ListarMotivosNoEntregaQuery(string? Busqueda = null, bool? SoloActivos = null) : IRequest<IReadOnlyList<MotivoNoEntregaDto>>;

public record ObtenerResumenMotivosNoEntregaQuery : IRequest<ResumenMotivosNoEntregaDto>;

public record ObtenerMotivoNoEntregaQuery(Guid MotivoId) : IRequest<MotivoNoEntregaDto>;

/// <summary>Motivos activos y no internos que descarga la app del repartidor.</summary>
public record ListarMotivosParaRepartidorQuery : IRequest<IReadOnlyList<MotivoNoEntregaDto>>;
