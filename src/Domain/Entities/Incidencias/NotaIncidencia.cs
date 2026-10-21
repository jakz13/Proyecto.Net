using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Incidencias;

/// <summary>Entrada de la bitácora de seguimiento de una incidencia.</summary>
public class NotaIncidencia : Entity
{
    public Guid IncidenciaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Comentario { get; private set; } = string.Empty;
    public DateTime Fecha { get; private set; }

    protected NotaIncidencia() { }

    public NotaIncidencia(Guid id, Guid incidenciaId, Guid usuarioId, string comentario, DateTime fecha) : base(id)
    {
        if (string.IsNullOrWhiteSpace(comentario))
            throw new DomainValidationException("El comentario no puede ser vacío.");

        if (comentario.Length > 1000)
            throw new DomainValidationException("El comentario no puede superar los 1000 caracteres.");

        IncidenciaId = incidenciaId;
        UsuarioId = usuarioId;
        Comentario = comentario.Trim();
        Fecha = fecha;
    }
}
