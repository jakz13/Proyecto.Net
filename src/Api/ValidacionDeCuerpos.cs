using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Api;

/// <summary>
/// Un elemento nulo dentro de una lista del cuerpo ({"lista":[null]}) es un dato mal formado: se rechaza con 400 y un mensaje claro
/// en lugar de reventar con 500 al leer sus propiedades (defecto D-02/D-03 de la auditoría del 14/11).
/// </summary>
public static class ValidacionDeCuerpos
{
    public static IEnumerable<T> SinNulos<T>(this IEnumerable<T?> lista, string mensaje) where T : class =>
        lista.Select(elemento => elemento ?? throw new DomainValidationException(mensaje));
}
