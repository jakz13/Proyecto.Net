using MediatR;
using UltimaMilla.Application.DTOs.Retiros;

namespace UltimaMilla.Application.Commands.Retiros.GestionRetiros;

/// <summary>El comercio (o el operador) cancela un retiro que todavía no está en una hoja de ruta.</summary>
public record CancelarRetiroCommand(Guid RetiroId, string Motivo) : IRequest<RetiroDetalleDto>;

/// <summary>Tras un retiro no realizado, el despachador decide una nueva visita: el retiro vuelve a estar solicitado.</summary>
public record ReprogramarRetiroCommand(Guid RetiroId) : IRequest<RetiroDetalleDto>;

/// <summary>El operario de depósito recibe los bultos retirados que trajo el repartidor.</summary>
public record RecibirRetiroEnDepositoCommand(Guid RetiroId) : IRequest<RetiroDetalleDto>;

/// <summary>El operador hace llegar los bultos al comercio y cierra el flujo.</summary>
public record EntregarRetiroAlComercioCommand(Guid RetiroId, string? Receptor) : IRequest<RetiroDetalleDto>;

/// <summary>El despachador saca un retiro de una hoja de ruta que todavía no salió.</summary>
public record SacarRetiroDeRutaCommand(Guid RetiroId) : IRequest<RetiroDetalleDto>;
