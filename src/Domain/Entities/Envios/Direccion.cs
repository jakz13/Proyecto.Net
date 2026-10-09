namespace UltimaMilla.Domain.Entities.Envios;

public sealed record Direccion(
    string Calle,
    string Numero,
    string Ciudad,
    Guid? ZonaCoberturaId = null,
    double? Latitud = null,
    double? Longitud = null,
    string? Referencia = null,
    Guid? DireccionId = null)
{
    public override string ToString() => $"{Calle} {Numero}, {Ciudad}";
}
