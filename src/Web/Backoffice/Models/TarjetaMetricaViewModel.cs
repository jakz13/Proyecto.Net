namespace UltimaMilla.Backoffice.Models;

/// <summary>
/// Modelo de datos para el componente reutilizable de tarjeta métrica (_TarjetaMetrica.cshtml).
/// Permite estandarizar el tamaño, encabezados con ayuda contextual y acciones de los paneles.
/// </summary>
public record TarjetaMetricaViewModel(
    string Titulo,
    string Valor,
    string? Ayuda = null,
    string? ColorValor = null,
    string? BotonTexto = null,
    string? BotonUrl = null
);
