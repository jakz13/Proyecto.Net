namespace UltimaMilla.Application.Exceptions;

/// <summary>El mensaje llegó al worker pero ningún proceso sabe atender su tipo (por ejemplo, al reencolar desde la cola de fallidos).</summary>
public class MensajeSinManejadorException : Exception
{
    public MensajeSinManejadorException(string tipoMensaje)
        : base($"No hay un proceso registrado que atienda mensajes de tipo '{tipoMensaje}'.")
    {
    }
}

/// <summary>El payload de un mensaje no tiene el formato o los datos mínimos esperados para su tipo.</summary>
public class MensajeInvalidoException : Exception
{
    public MensajeInvalidoException(string mensaje) : base(mensaje)
    {
    }
}
