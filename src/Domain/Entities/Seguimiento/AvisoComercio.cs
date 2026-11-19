using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Seguimiento;

/// <summary>
/// Representa el intento o registro de entrega de un aviso/webhook hacia los sistemas del comercio.
/// </summary>
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

    public void MarcarFallido(string respuestaHttp)
    {
        Intentos++;
        UltimaRespuestaHttp = respuestaHttp;
        ProximoReintento = null;
        Estado = "Fallido";
    }

    /// <summary>Los reintentos automáticos se agotaron: el aviso queda en la cola de fallidos para reenvío manual.</summary>
    public void MarcarAgotado(string respuestaHttp)
    {
        Intentos++;
        UltimaRespuestaHttp = respuestaHttp;
        ProximoReintento = null;
        Estado = "Agotado";
    }

    public void MarcarEntregado(string? respuestaHttp = null)
    {
        Intentos++;
        UltimaRespuestaHttp = respuestaHttp;
        ProximoReintento = null;
        Estado = "Entregado";
    }

    /// <summary>Estados en los que el aviso ya no se reintenta solo y espera una decisión humana: es lo que muestra la cola de fallidos.</summary>
    public static bool EsEstadoDeColaDeFallidos(string estado) => estado is "Agotado" or "Fallido";

    /// <summary>
    /// El administrador resuelve el aviso sin reenviarlo (el comercio ya no existe, la integración se dio de baja, etc.).
    /// Se conserva para auditoría y deja de contar como aviso fallido.
    /// </summary>
    public void Descartar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new DomainValidationException("El motivo de descarte es obligatorio.");

        if (!EsEstadoDeColaDeFallidos(Estado))
            throw new DomainValidationException($"Solo se puede descartar un aviso agotado o fallido; este está en estado '{Estado}'.");

        var detalle = $"[Descartado el {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC. Motivo: {motivo.Trim()}] {UltimaRespuestaHttp}".Trim();
        UltimaRespuestaHttp = detalle.Length <= LargoMaximoRespuesta ? detalle : detalle[..LargoMaximoRespuesta];
        ProximoReintento = null;
        Estado = "Descartado";
    }

    /// <summary>Coincide con el largo máximo de la columna de la última respuesta HTTP.</summary>
    private const int LargoMaximoRespuesta = 1000;

    /// <summary>Habilita el reenvío manual desde operación o soporte cuando el aviso quedó fallido o agotado.</summary>
    public void PrepararReenvioManual()
    {
        if (Estado == "Entregado")
            throw new DomainValidationException("El aviso ya fue entregado exitosamente y no requiere reenvío.");

        Estado = "Pendiente";
        Intentos = 0;
        ProximoReintento = null;
    }

    /// <summary>Reenvío solicitado por el propio comercio desde su portal: conserva el contador de intentos y se reintenta de inmediato.</summary>
    public void SolicitarReenvioManual()
    {
        if (Estado == "Entregado")
            throw new DomainValidationException("El aviso ya fue entregado exitosamente y no requiere reenvío manual.");

        Estado = "Reintentando";
        ProximoReintento = DateTime.UtcNow;
    }
}
