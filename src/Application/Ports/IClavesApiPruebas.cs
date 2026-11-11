namespace UltimaMilla.Application.Ports;

/// <summary>
/// Clave de pruebas (sandbox) de la API pública de cada comercio. Se deriva de forma determinista de la relación comercio-operador,
/// de modo que no hay nada más que almacenar: el comercio la consulta cuando la necesite y con ella la API valida y calcula la tarifa
/// sin registrar ni publicar nada.
/// </summary>
public interface IClavesApiPruebas
{
    /// <summary>Prefijo que distingue una clave de pruebas de una de producción.</summary>
    const string Prefijo = "umt_";

    string Generar(Guid comercioOperadorId);

    /// <summary>Devuelve la relación comercio-operador de la clave, o <c>null</c> si no es una clave de pruebas válida.</summary>
    Guid? Validar(string? clave);
}
