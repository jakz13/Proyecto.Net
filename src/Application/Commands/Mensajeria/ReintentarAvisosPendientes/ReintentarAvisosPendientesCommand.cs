using MediatR;

namespace UltimaMilla.Application.Commands.Mensajeria.ReintentarAvisosPendientes;

/// <summary>Retoma la entrega de avisos a comercios que quedaron pendientes o en reintento y cuyo plazo de espera ya venció (UC-36).</summary>
public record ReintentarAvisosPendientesCommand(int TamanoLote = 50) : IRequest<ResultadoEntregaAvisos>;
