using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Common;

/// <summary>
/// Criterios únicos del sistema para los datos de contacto y de ubicación. Todo lo que entra (backoffice, portal, API de integración,
/// importación CSV) pasa por acá: el front muestra el mismo mensaje, pero el que manda es este (un cliente que llame a la API
/// sin pasar por la pantalla no puede guardar datos inválidos).
/// </summary>
public static class ContactoValidacion
{
    /// <summary>Un celular tiene exactamente nueve dígitos.</summary>
    public const int DigitosTelefono = 9;
    public const string MensajeTelefono = "El teléfono debe tener exactamente 9 dígitos, solo números (ej.: 099123456).";
    public const string MensajeEmail = "Ingrese un correo electrónico válido (ej.: nombre@empresa.com).";

    // Mismo patrón que EmailValidoAttribute del front: parte local de grupos alfanuméricos unidos por un solo separador (sin punto al borde ni
    // repetido) y dominio con etiquetas que no empiezan ni terminan en guion y extensión alfabética de 2 a 24 letras.
    private static readonly Regex PatronEmail = new(
        @"^[A-Za-z0-9]+([._%+\-][A-Za-z0-9]+)*@([A-Za-z0-9]([A-Za-z0-9\-]*[A-Za-z0-9])?\.)+[A-Za-z]{2,24}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    private static readonly Regex PatronNumeroPuerta = new(
        @"^[A-Za-z0-9][A-Za-z0-9 /\-.]{0,29}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    private static readonly Regex PatronCiudad = new(
        @"^\p{L}[\p{L} .'\-]{1,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    private static readonly Regex PatronColor = new(
        @"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    // ---------- Email ----------

    public static bool EmailEsValido(string? email)
    {
        var valor = email?.Trim();
        if (string.IsNullOrEmpty(valor) || valor.Length > 200 || valor.IndexOf('@') > 64)
            return false;
        try { return PatronEmail.IsMatch(valor); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    /// <summary>Valida y devuelve el email normalizado (minúsculas, sin espacios). <paramref name="sujeto"/> completa el mensaje ("del comercio").</summary>
    public static string ValidarEmail(string? email, string sujeto = "")
    {
        if (!EmailEsValido(email))
            throw new DomainValidationException(string.IsNullOrWhiteSpace(sujeto)
                ? MensajeEmail
                : $"El correo electrónico {sujeto} no es válido (ej.: nombre@empresa.com).");
        return email!.Trim().ToLowerInvariant();
    }

    /// <summary>Para campos opcionales: vacío es válido (devuelve null); si viene algo, debe ser un email válido.</summary>
    public static string? ValidarEmailOpcional(string? email, string sujeto = "") =>
        string.IsNullOrWhiteSpace(email) ? null : ValidarEmail(email, sujeto);

    // ---------- Teléfono ----------

    /// <summary>Quita los separadores habituales (espacios y guiones) y devuelve solo los dígitos, o null si no son exactamente nueve.</summary>
    public static string? NormalizarTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return null;

        var digitos = new char[telefono.Length];
        var n = 0;
        foreach (var c in telefono.Trim())
        {
            if (c is >= '0' and <= '9') { digitos[n++] = c; continue; }
            if (c is ' ' or '-') continue;
            return null; // letras, "+", paréntesis, etc.
        }

        return n == DigitosTelefono ? new string(digitos, 0, n) : null;
    }

    public static bool TelefonoEsValido(string? telefono) => NormalizarTelefono(telefono) is not null;

    /// <summary>Valida y devuelve el teléfono con solo sus nueve dígitos.</summary>
    public static string ValidarTelefono(string? telefono, string sujeto = "")
    {
        var normalizado = NormalizarTelefono(telefono);
        if (normalizado is null)
            throw new DomainValidationException(string.IsNullOrWhiteSpace(sujeto)
                ? MensajeTelefono
                : $"El teléfono {sujeto} debe tener exactamente 9 dígitos, solo números (ej.: 099123456).");
        return normalizado;
    }

    public static string? ValidarTelefonoOpcional(string? telefono, string sujeto = "") =>
        string.IsNullOrWhiteSpace(telefono) ? null : ValidarTelefono(telefono, sujeto);

    // ---------- Direcciones ----------

    /// <summary>Texto de dirección libre (domicilio fiscal, depósito): obligatorio, con largo razonable, al menos una letra y sin marcado ni caracteres de control.</summary>
    public static string ValidarDireccionTexto(string? direccion, string campo, int minimo = 5, int maximo = 200)
    {
        var valor = direccion?.Trim();
        if (string.IsNullOrEmpty(valor))
            throw new DomainValidationException($"{campo} no puede ser vacía.");
        if (valor.Length < minimo || valor.Length > maximo)
            throw new DomainValidationException($"{campo} debe tener entre {minimo} y {maximo} caracteres.");
        if (!valor.Any(char.IsLetter))
            throw new DomainValidationException($"{campo} debe incluir al menos una letra (ej.: Av. Italia 3456).");
        if (TieneCaracteresNoPermitidos(valor))
            throw new DomainValidationException($"{campo} contiene caracteres no permitidos.");
        return valor;
    }

    public static string ValidarCalle(string? calle)
    {
        var valor = calle?.Trim();
        if (string.IsNullOrEmpty(valor))
            throw new DomainValidationException("La calle de la dirección no puede ser vacía.");
        if (valor.Length < 2 || valor.Length > 200)
            throw new DomainValidationException("La calle debe tener entre 2 y 200 caracteres.");
        if (!valor.Any(char.IsLetter))
            throw new DomainValidationException("La calle debe incluir al menos una letra.");
        if (TieneCaracteresNoPermitidos(valor))
            throw new DomainValidationException("La calle contiene caracteres no permitidos.");
        return valor;
    }

    public static string ValidarNumeroPuerta(string? numero)
    {
        var valor = numero?.Trim();
        if (string.IsNullOrEmpty(valor))
            throw new DomainValidationException("El número de la dirección no puede ser vacío.");
        try
        {
            if (!PatronNumeroPuerta.IsMatch(valor))
                throw new DomainValidationException("El número de puerta no es válido (use letras, números, espacio, guion o barra; ej.: 1234, 1234 bis, S/N).");
        }
        catch (RegexMatchTimeoutException)
        {
            throw new DomainValidationException("El número de puerta no es válido.");
        }
        return valor;
    }

    public static string ValidarCiudad(string? ciudad)
    {
        var valor = ciudad?.Trim();
        if (string.IsNullOrEmpty(valor))
            throw new DomainValidationException("La ciudad de la dirección no puede ser vacía.");
        try
        {
            if (!PatronCiudad.IsMatch(valor))
                throw new DomainValidationException("La ciudad no es válida (solo letras, espacios, punto, apóstrofo o guion; entre 2 y 100 caracteres).");
        }
        catch (RegexMatchTimeoutException)
        {
            throw new DomainValidationException("La ciudad no es válida.");
        }
        return valor;
    }

    private static bool TieneCaracteresNoPermitidos(string valor) =>
        valor.Any(c => c is '<' or '>' || char.IsControl(c));

    // ---------- Zonas ----------

    public static string ValidarColorHex(string? color)
    {
        var valor = color?.Trim();
        if (string.IsNullOrEmpty(valor) || !PatronColor.IsMatch(valor))
            throw new DomainValidationException("El color debe tener el formato #RRGGBB (ej.: #2563EB).");
        return valor;
    }

    /// <summary>
    /// El polígono se guarda como texto JSON: lista de pares [latitud, longitud] o GeoJSON ([longitud, latitud]). Se exigen al menos tres
    /// vértices, todos números finitos dentro del rango geográfico. Un texto cualquiera dejaba una zona que no contenía ningún punto.
    /// </summary>
    public static string ValidarPoligono(string? poligono)
    {
        var texto = poligono?.Trim();
        if (string.IsNullOrEmpty(texto))
            throw new DomainValidationException("El polígono geográfico de la zona no puede ser vacío.");
        if (texto.Length > 200_000)
            throw new DomainValidationException("El polígono geográfico es demasiado grande.");

        const string mensajeFormato = "El polígono de la zona no es válido: debe ser una lista JSON de al menos 3 puntos [latitud, longitud] dentro del rango geográfico.";
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
                    throw new DomainValidationException(mensajeFormato);
            }

            while (raiz.ValueKind == JsonValueKind.Array && raiz.GetArrayLength() > 0 &&
                   raiz[0].ValueKind == JsonValueKind.Array && raiz[0].GetArrayLength() > 0 && raiz[0][0].ValueKind == JsonValueKind.Array)
            {
                raiz = raiz[0];
            }

            if (raiz.ValueKind != JsonValueKind.Array)
                throw new DomainValidationException(mensajeFormato);

            var vertices = 0;
            foreach (var par in raiz.EnumerateArray())
            {
                if (par.ValueKind != JsonValueKind.Array || par.GetArrayLength() < 2 ||
                    par[0].ValueKind != JsonValueKind.Number || par[1].ValueKind != JsonValueKind.Number)
                    throw new DomainValidationException(mensajeFormato);

                var a = par[0].GetDouble();
                var b = par[1].GetDouble();
                var (lat, lng) = esGeoJson ? (b, a) : (a, b);
                if (!double.IsFinite(lat) || !double.IsFinite(lng) || lat is < -90 or > 90 || lng is < -180 or > 180)
                    throw new DomainValidationException(mensajeFormato);
                vertices++;
            }

            if (vertices < 3)
                throw new DomainValidationException(mensajeFormato);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            throw new DomainValidationException(mensajeFormato);
        }

        return texto;
    }

    /// <summary>Formatea para mostrar un teléfono de nueve dígitos como 099 123 456.</summary>
    public static string FormatearTelefono(string? telefono)
    {
        var n = NormalizarTelefono(telefono);
        return n is null ? telefono ?? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{n[..3]} {n[3..6]} {n[6..]}");
    }
}
