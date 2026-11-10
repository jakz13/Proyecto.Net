using Microsoft.AspNetCore.Authorization;

namespace UltimaMilla.Api.Auth;

/// <summary>
/// Autorización de la API por perfil (letra 6.3). Autenticarse no alcanza: cada endpoint declara qué perfiles pueden usarlo,
/// de modo que, por ejemplo, un usuario de comercio no pueda llamar directamente a los endpoints de administración del operador.
/// Los nombres de perfil son los que el login emite en el claim de rol.
/// </summary>
public static class PoliticasApi
{
    public const string RolAdministrador = "Administrador";
    public const string RolDespachador = "Despachador";
    public const string RolOperario = "Operario";
    public const string RolRepartidor = "Repartidor";
    public const string RolComercio = "Comercio";

    /// <summary>Solo el administrador del operador: usuarios, zonas, tarifas, reglas, identidad visual.</summary>
    public const string Administracion = "Administracion";

    /// <summary>Administrador y despachador: planificación, rutas, incidencias, tablero y reportes.</summary>
    public const string Operaciones = "Operaciones";

    /// <summary>Operación de depósito: administrador, despachador y operario.</summary>
    public const string Deposito = "Deposito";

    /// <summary>Cualquier persona del operador logístico, nunca un usuario de comercio.</summary>
    public const string PersonalDelOperador = "PersonalDelOperador";

    /// <summary>El repartidor en la calle (aplicación móvil).</summary>
    public const string Repartidor = "Repartidor";

    /// <summary>Usuario de un comercio cliente: ve y opera únicamente sobre lo suyo.</summary>
    public const string Comercio = "Comercio";

    public static void Configurar(AuthorizationOptions opciones)
    {
        opciones.AddPolicy(Administracion, p => p.RequireAuthenticatedUser().RequireRole(RolAdministrador));
        opciones.AddPolicy(Operaciones, p => p.RequireAuthenticatedUser().RequireRole(RolAdministrador, RolDespachador));
        opciones.AddPolicy(Deposito, p => p.RequireAuthenticatedUser().RequireRole(RolAdministrador, RolDespachador, RolOperario));
        opciones.AddPolicy(PersonalDelOperador, p => p.RequireAuthenticatedUser()
            .RequireRole(RolAdministrador, RolDespachador, RolOperario, RolRepartidor));
        opciones.AddPolicy(Repartidor, p => p.RequireAuthenticatedUser().RequireRole(RolRepartidor));
        opciones.AddPolicy(Comercio, p => p.RequireAuthenticatedUser().RequireRole(RolComercio));
    }
}

/// <summary>Extensiones para declarar la autorización de los grupos de endpoints.</summary>
public static class AutorizacionExtensiones
{
    /// <summary>
    /// Los grupos de administración se pueden consultar con un perfil más amplio que el que los modifica: la lectura la fija la política del grupo
    /// y cualquier método que no sea de lectura exige además esta política. Ambas se combinan (deben cumplirse las dos).
    /// </summary>
    public static TBuilder ExigirParaEscritura<TBuilder>(this TBuilder builder, string politica) where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (contexto, siguiente) =>
        {
            var metodo = contexto.HttpContext.Request.Method;
            if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo))
                return await siguiente(contexto);

            var autorizacion = contexto.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var resultado = await autorizacion.AuthorizeAsync(contexto.HttpContext.User, politica);
            return resultado.Succeeded ? await siguiente(contexto) : Results.Forbid();
        });
}
