using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class PruebaDeEntrega : Entity
{
    public Guid IntentoEntregaId { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string? FirmaImagenUrl { get; private set; }
    public string? FotoUrl { get; private set; }
    public string? NombreReceptor { get; private set; }
    public string? DocumentoReceptor { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public DateTime TimestampDispositivo { get; private set; }

    protected PruebaDeEntrega() { }

    public PruebaDeEntrega(
        Guid intentoEntregaId,
        string tipo,
        string? firmaImagenUrl = null,
        string? fotoUrl = null,
        string? nombreReceptor = null,
        string? documentoReceptor = null,
        double? latitud = null,
        double? longitud = null,
        DateTime? timestampDispositivo = null) : base(intentoEntregaId)
    {
        if (intentoEntregaId == Guid.Empty)
            throw new DomainValidationException("El identificador del intento de entrega no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(tipo))
            throw new DomainValidationException("El tipo de prueba de entrega no puede ser vacío.");

        IntentoEntregaId = intentoEntregaId;
        Tipo = tipo.Trim();
        FirmaImagenUrl = firmaImagenUrl?.Trim();
        FotoUrl = fotoUrl?.Trim();
        NombreReceptor = nombreReceptor?.Trim();
        DocumentoReceptor = documentoReceptor?.Trim();
        Latitud = latitud;
        Longitud = longitud;
        TimestampDispositivo = timestampDispositivo ?? DateTime.UtcNow;
    }
}
