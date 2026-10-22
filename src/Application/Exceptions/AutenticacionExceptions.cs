namespace UltimaMilla.Application.Exceptions;

/// <summary>
/// Excepción lanzada cuando el correo no existe o la contraseña no coincide.
/// Utiliza un mensaje genérico para mitigar la enumeración de usuarios.
/// </summary>
public class CredencialesInvalidasException : Exception
{
    public CredencialesInvalidasException(string mensaje = "Credenciales inválidas.")
        : base(mensaje)
    {
    }
}

/// <summary>
/// Excepción lanzada cuando la cuenta está bloqueada temporalmente por reiterados intentos fallidos.
/// </summary>
public class CuentaBloqueadaException : Exception
{
    public DateTime? BloqueadoHasta { get; }

    public CuentaBloqueadaException(string mensaje, DateTime? bloqueadoHasta = null)
        : base(mensaje)
    {
        BloqueadoHasta = bloqueadoHasta;
    }
}

/// <summary>
/// Excepción lanzada cuando la cuenta del usuario no está activa.
/// </summary>
public class CuentaInactivaException : Exception
{
    public CuentaInactivaException(string mensaje = "La cuenta de usuario se encuentra inactiva.")
        : base(mensaje)
    {
    }
}
