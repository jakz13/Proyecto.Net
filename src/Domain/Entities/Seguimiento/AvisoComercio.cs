using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

public class AvisoComercio : Entity
{
    public Guid SuscripcionAvisoId { get; private set; }
    public Guid EnvioId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string FirmaEnviada { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;
    public int Intentos { get; private set; }
    public DateTime? ProximoReintento { get; private set; }
    public string? UltimaRespuestaHttp { get; private set; }
    public DateTime Timestamp { get; private set; }

    protected AvisoComercio() { }

    public AvisoComercio(
        Guid id,
        Guid suscripcionAvisoId,
        Guid envioId,
        string payload,
        string firmaEnviada,
        string estado = "Pendiente") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del aviso no puede ser vacío.");

        if (suscripcionAvisoId == Guid.Empty)
            throw new DomainValidationException("El identificador de la suscripción no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainValidationException("El payload del aviso no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(firmaEnviada))
            throw new DomainValidationException("La firma no puede ser vacía.");

        SuscripcionAvisoId = suscripcionAvisoId;
        EnvioId = envioId;
        Payload = payload.Trim();
        FirmaEnviada = firmaEnviada.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Trim();
        Intentos = 0;
        Timestamp = DateTime.UtcNow;
    }

    public void RegistrarFallo(string respuestaHttp, DateTime proximoReintento)
    {
        Intentos++;
        UltimaRespuestaHttp = respuestaHttp;
        ProximoReintento = proximoReintento;
        Estado = "Reintentando";
    }

    public void MarcarEntregado(string? respuestaHttp = null)
    {
        Intentos++;
        UltimaRespuestaHttp = respuestaHttp;
        ProximoReintento = null;
        Estado = "Entregado";
    }
}
