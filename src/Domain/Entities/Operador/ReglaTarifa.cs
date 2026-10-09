using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class ReglaTarifa : Entity
{
    public Guid CuadroTarifarioId { get; private set; }
    public Guid? ZonaCoberturaId { get; private set; }
    public string ModalidadServicio { get; private set; } = string.Empty;
    public decimal PesoDesde { get; private set; }
    public decimal PesoHasta { get; private set; }
    public decimal VolumenDesde { get; private set; }
    public decimal VolumenHasta { get; private set; }
    public decimal PrecioBase { get; private set; }
    public decimal Recargo { get; private set; }
    public decimal Bonificacion { get; private set; }

    protected ReglaTarifa() { }

    public ReglaTarifa(
        Guid id,
        Guid cuadroTarifarioId,
        string modalidadServicio,
        decimal pesoDesde,
        decimal pesoHasta,
        decimal volumenDesde,
        decimal volumenHasta,
        decimal precioBase,
        decimal recargo = 0,
        decimal bonificacion = 0,
        Guid? zonaCoberturaId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la regla de tarifa no puede ser vacío.");

        if (cuadroTarifarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del cuadro tarifario no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(modalidadServicio))
            throw new DomainValidationException("La modalidad de servicio no puede ser vacía.");

        if (pesoDesde < 0 || pesoHasta < pesoDesde)
            throw new DomainValidationException("El rango de peso especificado es inválido.");

        if (volumenDesde < 0 || volumenHasta < volumenDesde)
            throw new DomainValidationException("El rango de volumen especificado es inválido.");

        if (precioBase < 0)
            throw new DomainValidationException("El precio base no puede ser negativo.");

        CuadroTarifarioId = cuadroTarifarioId;
        ZonaCoberturaId = zonaCoberturaId;
        ModalidadServicio = modalidadServicio.Trim();
        PesoDesde = pesoDesde;
        PesoHasta = pesoHasta;
        VolumenDesde = volumenDesde;
        VolumenHasta = volumenHasta;
        PrecioBase = precioBase;
        Recargo = recargo;
        Bonificacion = bonificacion;
    }

    public decimal CalcularPrecioFinal()
    {
        var final = PrecioBase + Recargo - Bonificacion;
        return final < 0 ? 0 : final;
    }

    public bool AplicaPara(decimal peso, decimal volumen, string modalidadServicio, Guid? zonaCoberturaId = null)
    {
        if (!ModalidadServicio.Equals(modalidadServicio, StringComparison.OrdinalIgnoreCase))
            return false;

        if (ZonaCoberturaId.HasValue && zonaCoberturaId.HasValue && ZonaCoberturaId.Value != zonaCoberturaId.Value)
            return false;

        return peso >= PesoDesde && peso <= PesoHasta && volumen >= VolumenDesde && volumen <= VolumenHasta;
    }
}
