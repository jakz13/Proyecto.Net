namespace UltimaMilla.Backoffice.Pages.Tarifas;

/// <summary>
/// Modalidades con las que el sistema da de alta envíos y retiros (portal, API y depósito): la regla de tarifa debe llamarse exactamente así,
/// porque la correspondencia entre la regla y el envío es por nombre. Si el formulario ofreciera otros nombres ("Estándar 24-48h", "Mismo Día"…),
/// publicar una versión dejaría al operador sin tarifa y todas las altas fallarían (defecto D-06 de la auditoría del 14/11).
/// </summary>
public static class ModalidadesTarifa
{
    public static readonly IReadOnlyList<(string Valor, string Etiqueta)> Opciones = new (string, string)[]
    {
        ("Estandar", "Estándar (24-48 h)"),
        ("Urgente", "Urgente (express / mismo día)"),
        ("Retiro", "Retiro en domicilio")
    };

    public static bool Coincide(string? a, string? b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Un valor que ya está en la regla pero no figura en la lista (dato anterior): se conserva como opción para no cambiarlo sin que nadie lo note.</summary>
    public static bool EsDesconocida(string? modalidad) =>
        !string.IsNullOrWhiteSpace(modalidad) && !Opciones.Any(o => Coincide(o.Valor, modalidad));
}
