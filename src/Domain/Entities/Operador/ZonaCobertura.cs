using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class ZonaCobertura : AggregateRoot, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string PoligonoGeografico { get; private set; } = string.Empty;
    public bool Activa { get; private set; }
    public string ColorHex { get; private set; } = "#2563eb";
    public DateTime FechaCreacion { get; private set; } = DateTime.UtcNow;

    private readonly List<FranjaHoraria> _franjasHorarias = new();
    public IReadOnlyCollection<FranjaHoraria> FranjasHorarias => _franjasHorarias.AsReadOnly();

    protected ZonaCobertura() { }

    public ZonaCobertura(
        Guid id,
        Guid operadorId,
        string nombre,
        string poligonoGeografico) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre de la zona de cobertura no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(poligonoGeografico))
            throw new DomainValidationException("El polígono geográfico de la zona no puede ser vacío.");

        OperadorId = operadorId;
        Nombre = nombre.Trim();
        PoligonoGeografico = poligonoGeografico.Trim();
        Activa = true;
    }

    public void Activar()
    {
        Activa = true;
    }

    public void Desactivar()
    {
        Activa = false;
    }

    public void ActualizarDatos(string nombre, string colorHex)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainValidationException("El nombre de la zona de cobertura no puede ser vacío.");

        Nombre = nombre.Trim();
        if (!string.IsNullOrWhiteSpace(colorHex))
            ColorHex = colorHex.Trim();
    }

    public void QuitarFranjasHorarias()
    {
        _franjasHorarias.Clear();
    }

    public void ActualizarPoligono(string nuevoPoligono)
    {
        if (string.IsNullOrWhiteSpace(nuevoPoligono))
            throw new DomainValidationException("El polígono geográfico no puede ser vacío.");

        PoligonoGeografico = nuevoPoligono.Trim();
    }

    /// <summary>
    /// Indica si la coordenada cae dentro del polígono de la zona. El polígono se guarda como texto JSON:
    /// una lista de pares [latitud, longitud] o un GeoJSON (donde el orden es [longitud, latitud]).
    /// Si el texto no se puede interpretar como polígono, la zona no contiene ningún punto.
    /// </summary>
    public bool Contiene(double latitud, double longitud)
    {
        var vertices = LeerVertices(PoligonoGeografico);
        if (vertices.Count < 3)
            return false;

        // Trazado de rayos: se cuentan los lados que cruza una semirrecta horizontal desde el punto
        var dentro = false;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            var (latI, lngI) = vertices[i];
            var (latJ, lngJ) = vertices[j];
            if ((lngI > longitud) != (lngJ > longitud) &&
                latitud < (latJ - latI) * (longitud - lngI) / (lngJ - lngI) + latI)
            {
                dentro = !dentro;
            }
        }

        return dentro;
    }

    private static List<(double Lat, double Lng)> LeerVertices(string texto)
    {
        var resultado = new List<(double, double)>();
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(texto);
            var raiz = documento.RootElement;
            var esGeoJson = false;

            if (raiz.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                esGeoJson = true;
                if (raiz.TryGetProperty("features", out var features) && features.GetArrayLength() > 0)
                    raiz = features[0];
                if (raiz.TryGetProperty("geometry", out var geometria))
                    raiz = geometria;
                if (!raiz.TryGetProperty("coordinates", out raiz))
                    return resultado;
            }

            // Se baja por los niveles de anidación hasta la lista de pares (el primer anillo en un GeoJSON)
            while (raiz.ValueKind == System.Text.Json.JsonValueKind.Array && raiz.GetArrayLength() > 0 &&
                   raiz[0].ValueKind == System.Text.Json.JsonValueKind.Array &&
                   raiz[0].GetArrayLength() > 0 && raiz[0][0].ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                raiz = raiz[0];
            }

            if (raiz.ValueKind != System.Text.Json.JsonValueKind.Array)
                return resultado;

            foreach (var par in raiz.EnumerateArray())
            {
                if (par.ValueKind != System.Text.Json.JsonValueKind.Array || par.GetArrayLength() < 2)
                    continue;
                var a = par[0].GetDouble();
                var b = par[1].GetDouble();
                resultado.Add(esGeoJson ? (b, a) : (a, b));
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            resultado.Clear();
        }

        return resultado;
    }

    public void AgregarFranjaHoraria(FranjaHoraria franja)
    {
        ArgumentNullException.ThrowIfNull(franja);

        if (franja.ZonaCoberturaId != Id)
            throw new DomainValidationException("La franja horaria no pertenece a esta zona de cobertura.");

        _franjasHorarias.Add(franja);
    }
}
