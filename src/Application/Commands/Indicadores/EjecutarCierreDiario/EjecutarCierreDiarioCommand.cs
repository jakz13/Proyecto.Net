using MediatR;

namespace UltimaMilla.Application.Commands.Indicadores.EjecutarCierreDiario;

/// <summary>UC-38: consolida el resumen del día de un operador y registra su cierre. Es idempotente por operador y fecha.</summary>
public record EjecutarCierreDiarioCommand(Guid OperadorId, DateOnly Fecha) : IRequest<ResultadoCierreDiario>;

/// <param name="YaExistia">El cierre del día ya estaba registrado: no se generó otro ni se alteró el original.</param>
public record ResultadoCierreDiario(Guid CierreId, bool YaExistia, string ResumenJson);
