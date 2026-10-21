using MediatR;
using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;

namespace UltimaMilla.Application.Commands.Incidencias.CerrarIncidencia;

/// <summary>Cierra la incidencia indicando cómo se resolvió.</summary>
public record CerrarIncidenciaCommand(Guid IncidenciaId, string? Resolucion) : IRequest<IncidenciaResultDto>;
