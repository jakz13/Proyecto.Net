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
        DateTime? vigenteHasta = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la regla operativa no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (maxIntentosEntrega <= 0)
            throw new DomainValidationException("El número máximo de intentos debe ser mayor a cero.");

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
    }

    public void FinalizarVigencia(DateTime fechaFin)
    {
        if (fechaFin < VigenteDesde)
            throw new DomainValidationException("La fecha de fin de vigencia no puede ser anterior a la de inicio.");

        VigenteHasta = fechaFin;
    }
}
