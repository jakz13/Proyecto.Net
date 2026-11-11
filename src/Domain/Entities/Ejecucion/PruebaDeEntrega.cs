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

    /// <summary>Códigos de los bultos efectivamente entregados respaldados por esta prueba de entrega (RF-32). Se persiste.</summary>
    public IReadOnlyList<string> CodigosBultosEntregados { get; private set; } = Array.Empty<string>();

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
        DateTime? timestampDispositivo = null,
        IEnumerable<string>? codigosBultosEntregados = null) : base(Guid.NewGuid())
    {
        if (intentoEntregaId == Guid.Empty)
            throw new DomainValidationException("La prueba de entrega debe estar asociada a un intento válido.");

        if (string.IsNullOrWhiteSpace(tipo))
            throw new DomainValidationException("El tipo de prueba de entrega no puede ser vacío.");

        IntentoEntregaId = intentoEntregaId;
        Tipo = tipo.Trim();
        FirmaImagenUrl = firmaImagenUrl;
        FotoUrl = fotoUrl;
        NombreReceptor = nombreReceptor?.Trim();
        DocumentoReceptor = documentoReceptor?.Trim();
        Latitud = latitud;
        Longitud = longitud;
        TimestampDispositivo = timestampDispositivo ?? DateTime.UtcNow;

        if (codigosBultosEntregados is not null)
            AsociarBultosEntregados(codigosBultosEntregados);
    }

    public void AsociarBultosEntregados(IEnumerable<string> codigosBultos)
    {
        var normalizados = (codigosBultos ?? Enumerable.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        CodigosBultosEntregados = normalizados;
    }
}
