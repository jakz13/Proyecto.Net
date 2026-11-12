using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class ReglaOperativa : Entity, IMustHaveTenant
{
    public const string PoliticaNuevaVisitaSinRecargo = "NuevaVisitaSinRecargo";
    public const string PoliticaNuevaVisitaConRecargo = "NuevaVisitaConRecargo";
    public const string PoliticaDevolucionAlComercio = "DevolucionAlComercio";

    public static readonly IReadOnlySet<string> PoliticasEntregaParcialPermitidas =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PoliticaNuevaVisitaSinRecargo,
            PoliticaNuevaVisitaConRecargo,
            PoliticaDevolucionAlComercio
        };

    public Guid OperadorId { get; private set; }
    public int Version { get; private set; }
    public DateTime VigenteDesde { get; private set; }
    public DateTime? VigenteHasta { get; private set; }
    public int MaxIntentosEntrega { get; private set; }
    public TimeSpan PlazoEntreIntentos { get; private set; }
    public string TipoPruebaEntregaExigida { get; private set; } = string.Empty;
    public string PoliticaDevolucion { get; private set; } = string.Empty;
    public TimeSpan PlazoComprometidoEstandar { get; private set; }
    public TimeSpan PlazoComprometidoUrgente { get; private set; }
    public int MaxParadasPorRuta { get; private set; }
    public string? Comentario { get; private set; }

    /// <summary>Indica si el operador admite entregas parciales a nivel de bulto. Forma parte de la versión de la regla y se persiste.</summary>
    public bool AdmiteEntregaParcial { get; private set; } = true;

    /// <summary>
    /// Define qué sucede con los bultos pendientes tras una entrega parcial:
    /// NuevaVisitaSinRecargo, NuevaVisitaConRecargo o DevolucionAlComercio. Se persiste.
    /// </summary>
    public string PoliticaEntregaParcial { get; private set; } = PoliticaNuevaVisitaSinRecargo;

    protected ReglaOperativa() { }

    public ReglaOperativa(
        Guid id,
        Guid operadorId,
        int version,
        DateTime vigenteDesde,
        int maxIntentosEntrega,
        TimeSpan plazoEntreIntentos,
        string tipoPruebaEntregaExigida,
        string politicaDevolucion,
        TimeSpan plazoComprometidoEstandar,
        TimeSpan plazoComprometidoUrgente,
        DateTime? vigenteHasta = null,
        int maxParadasPorRuta = 50,
        bool admiteEntregaParcial = true,
        string politicaEntregaParcial = PoliticaNuevaVisitaSinRecargo) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la regla operativa no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (version <= 0)
            throw new DomainValidationException("La versión de la regla operativa debe ser mayor a cero.");

        if (maxIntentosEntrega <= 0)
            throw new DomainValidationException("El máximo de intentos de entrega debe ser mayor a cero.");

        if (maxParadasPorRuta <= 0)
            throw new DomainValidationException("El máximo de paradas por ruta debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(tipoPruebaEntregaExigida))
            throw new DomainValidationException("El tipo de prueba de entrega exigida es obligatorio.");

        if (string.IsNullOrWhiteSpace(politicaDevolucion))
            throw new DomainValidationException("La política de devolución es obligatoria.");

        var politicaParcialNormalizada = NormalizarPoliticaEntregaParcial(politicaEntregaParcial);

        OperadorId = operadorId;
        Version = version;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
        MaxIntentosEntrega = maxIntentosEntrega;
        PlazoEntreIntentos = plazoEntreIntentos;
        TipoPruebaEntregaExigida = tipoPruebaEntregaExigida.Trim();
        PoliticaDevolucion = politicaDevolucion.Trim();
        PlazoComprometidoEstandar = plazoComprometidoEstandar;
        PlazoComprometidoUrgente = plazoComprometidoUrgente;
        MaxParadasPorRuta = maxParadasPorRuta;
        AdmiteEntregaParcial = admiteEntregaParcial;
        PoliticaEntregaParcial = politicaParcialNormalizada;
    }

    public void ConfigurarEntregaParcial(bool admiteEntregaParcial, string? politicaEntregaParcial = null)
    {
        var politicaNormalizada = NormalizarPoliticaEntregaParcial(politicaEntregaParcial ?? PoliticaEntregaParcial);
        AdmiteEntregaParcial = admiteEntregaParcial;
        PoliticaEntregaParcial = politicaNormalizada;
    }

    private static string NormalizarPoliticaEntregaParcial(string? politica)
    {
        if (string.IsNullOrWhiteSpace(politica))
            return PoliticaNuevaVisitaSinRecargo;

        var limpia = politica.Trim();
        if (string.Equals(limpia, PoliticaNuevaVisitaSinRecargo, StringComparison.OrdinalIgnoreCase))
            return PoliticaNuevaVisitaSinRecargo;
        if (string.Equals(limpia, PoliticaNuevaVisitaConRecargo, StringComparison.OrdinalIgnoreCase))
            return PoliticaNuevaVisitaConRecargo;
        if (string.Equals(limpia, PoliticaDevolucionAlComercio, StringComparison.OrdinalIgnoreCase))
            return PoliticaDevolucionAlComercio;

        throw new DomainValidationException(
            $"La política de entrega parcial '{politica}' no es válida. Valores admitidos: {PoliticaNuevaVisitaSinRecargo}, {PoliticaNuevaVisitaConRecargo}, {PoliticaDevolucionAlComercio}.");
    }

    public bool EstaVigenteEn(DateTime fecha)
    {
        return fecha >= VigenteDesde && (!VigenteHasta.HasValue || fecha <= VigenteHasta.Value);
    }

    public void FinalizarVigencia(DateTime fechaFin)
    {
        if (fechaFin < VigenteDesde)
            throw new DomainValidationException("La fecha de fin de vigencia no puede ser anterior al inicio.");

        VigenteHasta = fechaFin;
    }

    public void RegistrarComentario(string? comentario)
    {
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
    }

    public bool SuperaMaximoIntentos(int intentosAcumulados)
    {
        return intentosAcumulados >= MaxIntentosEntrega;
    }

    public int CalcularIntentosRestantes(int intentosAcumulados)
    {
        return Math.Max(0, MaxIntentosEntrega - intentosAcumulados);
    }

    public void ValidarPruebaEntrega(string? firmaBase64, string? fotoBase64, string? documentoReceptor)
    {
        var regla = TipoPruebaEntregaExigida.ToUpperInvariant();

        if (regla.Contains("FIRMA") && string.IsNullOrWhiteSpace(firmaDigitalOrFallback(firmaBase64)))
        {
            throw new DomainValidationException($"La regla operativa vigente exige firma digital del receptor ({TipoPruebaEntregaExigida}).");
        }

        if (regla.Contains("FOTO") && string.IsNullOrWhiteSpace(fotoBase64))
        {
            throw new DomainValidationException($"La regla operativa vigente exige fotografía de evidencia ({TipoPruebaEntregaExigida}).");
        }

        if (regla.Contains("DOCUMENTO") && string.IsNullOrWhiteSpace(documentoReceptor))
        {
            throw new DomainValidationException($"La regla operativa vigente exige documento de identidad del receptor ({TipoPruebaEntregaExigida}).");
        }
    }

    private static string? firmaDigitalOrFallback(string? firma) => firma;
}
