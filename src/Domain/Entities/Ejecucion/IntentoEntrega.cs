using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class IntentoEntrega : AggregateRoot
{
    public Guid ParadaId { get; private set; }
    public Guid EnvioId { get; private set; }
    public string Resultado { get; private set; } = string.Empty;
    public Guid? MotivoNoEntregaId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public string OrigenDatos { get; private set; } = string.Empty;

    /// <summary>
    /// Versión exacta de la regla operativa con la que se procesó el intento (trazabilidad).
    /// Es null en intentos anteriores a la trazabilidad o cuando el operador no tenía regla publicada.
    /// </summary>
    public Guid? ReglaOperativaId { get; private set; }

    public PruebaDeEntrega? PruebaDeEntrega { get; private set; }

    protected IntentoEntrega() { }

    public IntentoEntrega(
        Guid id,
        Guid paradaId,
        Guid envioId,
        string resultado,
        string origenDatos,
        Guid? motivoNoEntregaId = null,
        double? latitud = null,
        double? longitud = null,
        Guid? reglaOperativaId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del intento de entrega no puede ser vacío.");

        if (paradaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(resultado))
            throw new DomainValidationException("El resultado del intento no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(origenDatos))
            throw new DomainValidationException("El origen de datos no puede ser vacío.");

        ParadaId = paradaId;
        EnvioId = envioId;
        Resultado = resultado.Trim();
        OrigenDatos = origenDatos.Trim();
        MotivoNoEntregaId = motivoNoEntregaId;
        Latitud = latitud;
        Longitud = longitud;
        ReglaOperativaId = reglaOperativaId;
        Timestamp = DateTime.UtcNow;
    }

    public void AdjuntarPruebaEntrega(PruebaDeEntrega prueba)
    {
        ArgumentNullException.ThrowIfNull(prueba);

        if (prueba.IntentoEntregaId != Id)
            throw new DomainValidationException("La prueba de entrega no corresponde a este intento.");

        PruebaDeEntrega = prueba;
    }
}
