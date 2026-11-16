using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Common;

/// <summary>
/// Cédula de identidad: exactamente ocho dígitos, solo números (sin puntos, guiones ni letras).
/// Es el único criterio del sistema: destinatarios, repartidores y usuarios lo comparten.
/// </summary>
public static class Cedula
{
    public const int Digitos = 8;
    public const string Patron = @"^\d{8}$";
    public const string MensajeFormato = "La cédula debe tener exactamente 8 dígitos, solo números (sin puntos ni guiones).";

    public static bool EsValida(string? cedula)
    {
        var valor = cedula?.Trim();
        return valor is { Length: Digitos } && valor.All(c => c is >= '0' and <= '9');
    }

    /// <summary>Lanza la validación de dominio si la cédula no tiene el formato. <paramref name="sujeto"/> completa el mensaje ("del repartidor", "del destinatario").</summary>
    public static void Validar(string? cedula, string sujeto = "")
    {
        if (EsValida(cedula))
            return;

        var detalle = string.IsNullOrWhiteSpace(sujeto) ? "La cédula" : $"La cédula {sujeto}";
        throw new DomainValidationException($"{detalle} debe tener exactamente {Digitos} dígitos, solo números (sin puntos ni guiones).");
    }
}
