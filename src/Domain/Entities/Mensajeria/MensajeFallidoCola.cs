using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Mensajeria;

public class MensajeFallidoCola : Entity
{
    public string TipoMensaje { get; private set; } = string.Empty;
    public string PayloadOriginal { get; private set; } = string.Empty;
    public string Excepcion { get; private set; } = string.Empty;
    public int Intentos { get; private set; }
    public DateTime PrimerFallo { get; private set; }
    public DateTime UltimoFallo { get; private set; }
    public string Estado { get; private set; } = string.Empty;

    protected MensajeFallidoCola() { }

    public MensajeFallidoCola(
        Guid id,
        string tipoMensaje,
        string payloadOriginal,
        string excepcion,
        string estado = "Fallido") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del mensaje fallido no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipoMensaje))
            throw new DomainValidationException("El tipo de mensaje no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(payloadOriginal))
            throw new DomainValidationException("El payload original no puede ser vacío.");

        TipoMensaje = tipoMensaje.Trim();
        PayloadOriginal = payloadOriginal.Trim();
        Excepcion = excepcion?.Trim() ?? string.Empty;
        Intentos = 1;
        PrimerFallo = DateTime.UtcNow;
        UltimoFallo = DateTime.UtcNow;
        Estado = string.IsNullOrWhiteSpace(estado) ? "Fallido" : estado.Trim();
    }

    public void RegistrarReintento(string nuevaExcepcion)
    {
        Intentos++;
        UltimoFallo = DateTime.UtcNow;
        Excepcion = nuevaExcepcion?.Trim() ?? string.Empty;
        Estado = "Reintentando";
    }

    public void Resolver()
    {
        Estado = "Resuelto";
    }
}
