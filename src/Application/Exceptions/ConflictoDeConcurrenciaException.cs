namespace UltimaMilla.Application.Exceptions;

/// <summary>
/// Indica que otra operación modificó o asignó los mismos datos de forma simultánea
/// (por ejemplo, dos despachadores asignando el mismo envío a rutas distintas).
/// </summary>
public class ConflictoDeConcurrenciaException : Exception
{
    public ConflictoDeConcurrenciaException(string message) : base(message) { }

    public ConflictoDeConcurrenciaException(string message, Exception innerException) : base(message, innerException) { }
}
