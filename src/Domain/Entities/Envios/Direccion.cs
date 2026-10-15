using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>
/// Dirección de entrega de un envío. Se persiste como entidad para que las paradas y los envíos la referencien por identificador.
/// </summary>
public class Direccion : Entity
{
    public string Calle { get; private set; } = string.Empty;
    public string Numero { get; private set; } = string.Empty;
    public string Ciudad { get; private set; } = string.Empty;
    public Guid? ZonaCoberturaId { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public string? Referencia { get; private set; }

    protected Direccion() { }

    public Direccion(
        Guid id,
        string calle,
        string numero,
        string ciudad,
        Guid? zonaCoberturaId = null,
        double? latitud = null,
        double? longitud = null,
        string? referencia = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la dirección no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(calle))
            throw new DomainValidationException("La calle de la dirección no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(numero))
            throw new DomainValidationException("El número de la dirección no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(ciudad))
            throw new DomainValidationException("La ciudad de la dirección no puede ser vacía.");

        if (latitud is < -90 or > 90)
            throw new DomainValidationException("La latitud debe estar entre -90 y 90.");

        if (longitud is < -180 or > 180)
            throw new DomainValidationException("La longitud debe estar entre -180 y 180.");

        if (zonaCoberturaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        Calle = calle.Trim();
        Numero = numero.Trim();
        Ciudad = ciudad.Trim();
        ZonaCoberturaId = zonaCoberturaId;
        Latitud = latitud;
        Longitud = longitud;
        Referencia = referencia?.Trim();
    }

    public override string ToString() => $"{Calle} {Numero}, {Ciudad}";
}
