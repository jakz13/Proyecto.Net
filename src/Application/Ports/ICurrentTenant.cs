namespace UltimaMilla.Application.Ports;

public interface ICurrentTenant
{
    Guid OperadorId { get; }
    Guid? UsuarioId { get; }
    bool IsAuthenticated { get; }
}
