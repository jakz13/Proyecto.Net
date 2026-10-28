using MediatR;

namespace UltimaMilla.Application.Commands.Indicadores.RecalcularIndicadoresCumplimiento;

/// <summary>UC-37: agrega los resultados de entrega de un día por zona, repartidor y comercio y actualiza los indicadores de cumplimiento.</summary>
public record RecalcularIndicadoresCumplimientoCommand(Guid OperadorId, DateOnly Fecha) : IRequest<ResultadoRecalculoIndicadores>;

public record ResultadoRecalculoIndicadores(string Periodo, int TotalEnvios, int IndicadoresGenerados, int IndicadoresActualizados);
