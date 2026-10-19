using System.Net;

namespace UltimaMilla.Frontend.Infrastructure.Excepciones;

/// <summary>
/// Excepción que encapsula errores de comunicación o respuestas de error de la API (ProblemDetails).
/// </summary>
public class ApiExcepcion : Exception
{
    public HttpStatusCode CodigoEstado { get; }
    public IReadOnlyDictionary<string, string[]>? ErroresValidacion { get; }

    public ApiExcepcion(string mensaje)
        : this(HttpStatusCode.BadRequest, mensaje)
    {
    }

    public ApiExcepcion(HttpStatusCode codigoEstado, string mensaje, IReadOnlyDictionary<string, string[]>? erroresValidacion = null)
        : base(mensaje)
    {
        CodigoEstado = codigoEstado;
        ErroresValidacion = erroresValidacion;
    }

    /// <summary>
    /// Indica si el error fue originado por un conflicto de concurrencia optimista (HTTP 409).
    /// </summary>
    public bool EsConflictoConcurrencia => CodigoEstado == HttpStatusCode.Conflict;

    /// <summary>
    /// Indica si el error fue originado por exceso de peticiones o límite de tasa (HTTP 429).
    /// </summary>
    public bool EsLimiteDeTasa => CodigoEstado == (HttpStatusCode)429;
}
