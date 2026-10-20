namespace UltimaMilla.Application.Ports;

public interface ICurrentTenant
{
    Guid OperadorId { get; }
    Guid? UsuarioId { get; }

    /// <summary>Relación comercio-operador del usuario cuando es un usuario de comercio; null en los demás perfiles.</summary>
    Guid? ComercioOperadorId { get; }
    bool IsAuthenticated { get; }
}
