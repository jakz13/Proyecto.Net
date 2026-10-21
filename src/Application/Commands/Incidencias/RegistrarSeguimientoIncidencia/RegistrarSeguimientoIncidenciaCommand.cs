using MediatR;
using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;

namespace UltimaMilla.Application.Commands.Incidencias.RegistrarSeguimientoIncidencia;

/// <summary>Agrega una nota a la bitácora de la incidencia. Con TomarACargo, el usuario pasa a ser su responsable.</summary>
public record RegistrarSeguimientoIncidenciaCommand(
    Guid IncidenciaId,
    string? Comentario,
    bool TomarACargo = false
) : IRequest<IncidenciaResultDto>;
