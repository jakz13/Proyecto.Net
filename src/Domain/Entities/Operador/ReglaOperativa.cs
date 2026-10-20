using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class ReglaOperativa : Entity, IMustHaveTenant
{
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
    public int MaxParadasPorRuta { get; private set; } = 50;

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
        int maxParadasPorRuta = 50) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la regla operativa no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (maxIntentosEntrega <= 0)
            throw new DomainValidationException("El número máximo de intentos debe ser mayor a cero.");

        if (maxParadasPorRuta <= 0)
            throw new DomainValidationException("La cantidad máxima de paradas por ruta debe ser mayor a cero.");

        if (string.IsNullOrWhiteSpace(tipoPruebaEntregaExigida))
            throw new DomainValidationException("El tipo de prueba de entrega exigida no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(politicaDevolucion))
            throw new DomainValidationException("La política de devolución no puede ser vacía.");

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
    }

    public void FinalizarVigencia(DateTime fechaFin)
    {
        if (fechaFin < VigenteDesde)
            throw new DomainValidationException("La fecha de fin de vigencia no puede ser anterior a la de inicio.");

        VigenteHasta = fechaFin;
    }

    public void ValidarPruebaEntrega(string? firmaDigital, string? fotoEvidencia, string? documentoReceptor)
    {
        var tipoNormalizado = TipoPruebaEntregaExigida.ToLowerInvariant();

        bool exigeFirma = tipoNormalizado.Contains("firma");
        bool exigeFoto = tipoNormalizado.Contains("foto") || tipoNormalizado.Contains("imagen");
        bool exigeDocumento = tipoNormalizado.Contains("documento") || tipoNormalizado.Contains("ci") || tipoNormalizado.Contains("dni");

        if (exigeFirma && string.IsNullOrWhiteSpace(firmaDigital))
            throw new DomainValidationException($"La regla operativa vigente (v{Version}) exige firma digital obligatoria del receptor para completar la entrega.");

        if (exigeFoto && string.IsNullOrWhiteSpace(fotoEvidencia))
            throw new DomainValidationException($"La regla operativa vigente (v{Version}) exige fotografía de evidencia obligatoria en el domicilio para completar la entrega.");

        if (exigeDocumento && string.IsNullOrWhiteSpace(documentoReceptor))
            throw new DomainValidationException($"La regla operativa vigente (v{Version}) exige documento de identidad obligatorio del receptor para completar la entrega.");
    }

    public bool SuperaMaximoIntentos(int intentosRealizados) => intentosRealizados >= MaxIntentosEntrega;

    public int CalcularIntentosRestantes(int intentosRealizados) => Math.Max(0, MaxIntentosEntrega - intentosRealizados);
}
