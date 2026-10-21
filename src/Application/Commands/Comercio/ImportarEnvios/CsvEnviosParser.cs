using System.Globalization;
using System.Text;
using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Commands.Comercio.ImportarEnvios;

/// <summary>
/// Lee el archivo CSV de importación (separador coma o punto y coma, una fila por envío con un bulto).
/// Columnas obligatorias: referencia, modalidad, destinatario, calle, numero, ciudad, peso, alto, ancho, profundo.
/// Opcionales: documento, email, telefono. Peso en kg y medidas en cm.
/// </summary>
public static class CsvEnviosParser
{
    private static readonly string[] ColumnasObligatorias =
        { "referencia", "modalidad", "destinatario", "calle", "numero", "ciudad", "peso", "alto", "ancho", "profundo" };

    public static IReadOnlyList<FilaImportacion> Leer(string contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            throw new DomainValidationException("El archivo está vacío.");

        var lineas = contenido
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Select((texto, indice) => (Texto: texto, Numero: indice + 1))
            .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
            .ToList();

        var encabezado = lineas[0].Texto.TrimStart('﻿');
        var separador = encabezado.Count(c => c == ';') > encabezado.Count(c => c == ',') ? ';' : ',';
        var encabezados = Dividir(encabezado, separador)
            .Select(h => h.Trim().ToLowerInvariant())
            .ToList();

        var faltantes = ColumnasObligatorias.Where(c => !encabezados.Contains(c)).ToList();
        if (faltantes.Count > 0)
            throw new DomainValidationException($"Faltan columnas obligatorias en el archivo: {string.Join(", ", faltantes)}.");

        var filas = new List<FilaImportacion>();
        foreach (var (texto, numero) in lineas.Skip(1))
        {
            var celdas = Dividir(texto, separador);
            if (celdas.Count != encabezados.Count)
            {
                filas.Add(new FilaImportacion(numero, null, $"La fila tiene {celdas.Count} columnas y se esperaban {encabezados.Count}."));
                continue;
            }

            string Valor(string columna)
            {
                var indice = encabezados.IndexOf(columna);
                return indice < 0 ? string.Empty : celdas[indice].Trim();
            }

            string? Opcional(string columna) => string.IsNullOrWhiteSpace(Valor(columna)) ? null : Valor(columna);

            if (!TryDecimal(Valor("peso"), out var peso) || !TryDecimal(Valor("alto"), out var alto)
                || !TryDecimal(Valor("ancho"), out var ancho) || !TryDecimal(Valor("profundo"), out var profundo))
            {
                filas.Add(new FilaImportacion(numero, null, "Peso y medidas deben ser números (use punto para los decimales)."));
                continue;
            }

            filas.Add(new FilaImportacion(numero, new AltaEnvioCommand(
                Guid.Empty,
                Valor("modalidad"),
                new DestinatarioAltaDto(Valor("destinatario"), Opcional("documento"), Opcional("email"), Opcional("telefono")),
                new DireccionAltaDto(Valor("calle"), Valor("numero"), Valor("ciudad")),
                new List<BultoAltaDto> { new(peso, alto, ancho, profundo) },
                null,
                Valor("referencia"))));
        }

        return filas;
    }

    private static bool TryDecimal(string texto, out decimal valor) =>
        decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);

    /// <summary>Divide una línea respetando campos entre comillas dobles (con "" como comilla escapada).</summary>
    private static List<string> Dividir(string linea, char separador)
    {
        var celdas = new List<string>();
        var actual = new StringBuilder();
        var entreComillas = false;

        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreComillas = false;
                }
                else
                {
                    actual.Append(c);
                }
            }
            else if (c == '"')
            {
                entreComillas = true;
            }
            else if (c == separador)
            {
                celdas.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }

        celdas.Add(actual.ToString());
        return celdas;
    }
}
