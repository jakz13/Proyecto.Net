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
    public string ColaOrigen { get; private set; } = string.Empty;
    public Guid? OperadorId { get; private set; }

    protected MensajeFallidoCola() { }

    public MensajeFallidoCola(
        Guid id,
        string tipoMensaje,
        string payloadOriginal,
        string excepcion,
        string estado = "Fallido",
        string? colaOrigen = null,
        Guid? operadorId = null) : base(id)
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
        ColaOrigen = colaOrigen?.Trim() ?? string.Empty;
        OperadorId = operadorId;
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

    /// <summary>Un reintento manual que volvió a fallar: el mensaje permanece en la cola con un intento más.</summary>
    public void RegistrarReintentoFallido(string nuevaExcepcion)
    {
        Intentos++;
        UltimoFallo = DateTime.UtcNow;
        Excepcion = nuevaExcepcion?.Trim() ?? string.Empty;
        Estado = "Fallido";
    }

    public void Resolver()
    {
        Estado = "Resuelto";
    }

    /// <summary>
    /// Descarta lógicamente el mensaje fallido sin eliminarlo físicamente, registrando el motivo.
    /// </summary>
    public void Descartar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainValidationException("El motivo de descarte es obligatorio.");

        // El motivo y la fecha quedan registrados en el detalle del mensaje (Excepcion); no se elimina nada
        Estado = "Descartado";
        Excepcion = string.IsNullOrWhiteSpace(Excepcion)
            ? $"[Descartado manualmente en {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. Motivo: {motivo.Trim()}]"
            : $"{Excepcion}\n[Descartado manualmente en {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. Motivo: {motivo.Trim()}]";
    }
}
