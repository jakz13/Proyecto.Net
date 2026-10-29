namespace UltimaMilla.Application.Ports;

public interface ICurrentTenant
{
    Guid OperadorId { get; }
    Guid? UsuarioId { get; }

    /// <summary>Relación comercio-operador del usuario cuando es un usuario de comercio; null en los demás perfiles.</summary>
    Guid? ComercioOperadorId { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// Fija el operador activo del alcance actual cuando la operación se ejecuta sin sesión HTTP de usuario
    /// (tareas del Worker por operador, eventos de mensajería, clave de API de comercio o seguimiento público).
    /// </summary>
    void EstablecerOperadorDeProceso(Guid operadorId)
    {
    }
}
