using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace UltimaMilla.Frontend.Contracts.Validaciones;

/// <summary>
/// El polígono de una zona es un texto JSON: lista de pares [latitud, longitud] o GeoJSON ([longitud, latitud]). Se exigen al menos tres
/// vértices con números finitos dentro del rango geográfico. Es la misma regla que aplica el dominio al guardar.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PoligonoValidoAttribute : ValidationAttribute
{
    public const string Mensaje = "El polígono no es válido: debe ser una lista de al menos 3 puntos [latitud, longitud] dentro del rango geográfico.";

    public PoligonoValidoAttribute() => ErrorMessage = Mensaje;

    public override bool IsValid(object? value)
    {
        if (value is null) return true; // la obligatoriedad la decide [Required]
        if (value is not string texto) return false;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        return EsValido(texto);
    }

    public static bool EsValido(string texto)
    {
        if (texto.Length > 200_000) return false;
        try
        {
            using var documento = JsonDocument.Parse(texto);
            var raiz = documento.RootElement;
            var esGeoJson = false;

            if (raiz.ValueKind == JsonValueKind.Object)
            {
                esGeoJson = true;
                if (raiz.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array && features.GetArrayLength() > 0)
                    raiz = features[0];
                if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("geometry", out var geometria))
                    raiz = geometria;
                if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("coordinates", out raiz))
                    return false;
            }

            while (raiz.ValueKind == JsonValueKind.Array && raiz.GetArrayLength() > 0 &&
                   raiz[0].ValueKind == JsonValueKind.Array && raiz[0].GetArrayLength() > 0 && raiz[0][0].ValueKind == JsonValueKind.Array)
            {
                raiz = raiz[0];
            }

            if (raiz.ValueKind != JsonValueKind.Array) return false;

            var vertices = 0;
            foreach (var par in raiz.EnumerateArray())
            {
                if (par.ValueKind != JsonValueKind.Array || par.GetArrayLength() < 2 ||
                    par[0].ValueKind != JsonValueKind.Number || par[1].ValueKind != JsonValueKind.Number)
                    return false;

                var a = par[0].GetDouble();
                var b = par[1].GetDouble();
                var (lat, lng) = esGeoJson ? (b, a) : (a, b);
                if (!double.IsFinite(lat) || !double.IsFinite(lng) || lat is < -90 or > 90 || lng is < -180 or > 180)
                    return false;
                vertices++;
            }

            return vertices >= 3;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            return false;
        }
    }
}
