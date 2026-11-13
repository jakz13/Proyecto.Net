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
    public const string RolPlataforma = "AdministradorPlataforma";

    /// <summary>Solo el administrador (letra, sección 3): configura la operación, las zonas, las tarifas, los usuarios y las reglas de negocio.</summary>
    public const string Administracion = "Administracion";

    /// <summary>Solo el despachador: planifica las rutas, asigna repartidores, monitorea la jornada y resuelve incidencias.</summary>
    public const string Operaciones = "Operaciones";

    /// <summary>Solo el operario de depósito: recibe, clasifica y rinde los bultos.</summary>
    public const string Deposito = "Deposito";

    /// <summary>Despachador y operario: el control del despacho de las hojas de ruta y los retiros, que pasan por las dos manos.</summary>
    public const string Despacho = "Despacho";

    /// <summary>Administrador y despachador: reportes de gestión y consulta de los catálogos con los que se planifica (comercios, flota).</summary>
    public const string Supervision = "Supervision";

    /// <summary>Cualquier persona del operador logístico, nunca un usuario de comercio.</summary>
    public const string PersonalDelOperador = "PersonalDelOperador";

    /// <summary>Consulta de un envío y su evidencia: despachador, operario y el comercio dueño del envío (el handler lo acota).</summary>

    /// <summary>
    /// Listado general de envíos del operador (con destinatarios y direcciones): lo ven quienes lo administran u operan y cada comercio con los suyos.
    /// El repartidor solo ve lo de su ruta y el administrador de la plataforma no accede a los datos de un operador.
    /// </summary>
    public const string ListadoDeEnvios = "ListadoDeEnvios";
    public const string ConsultaDeEnvios = "ConsultaDeEnvios";

    /// <summary>El repartidor en la calle (aplicación móvil).</summary>
    public const string Repartidor = "Repartidor";

    /// <summary>Usuario de un comercio cliente: ve y opera únicamente sobre lo suyo.</summary>
    public const string Comercio = "Comercio";

    /// <summary>Quien ofrece el servicio: activa y desactiva funcionalidades por operador. No es un perfil del operador.</summary>
    public const string Plataforma = "Plataforma";

    public static void Configurar(AuthorizationOptions opciones)
    {
        opciones.AddPolicy(Administracion, p => p.RequireAuthenticatedUser().RequireRole(RolAdministrador));
        opciones.AddPolicy(Operaciones, p => p.RequireAuthenticatedUser().RequireRole(RolDespachador));
        opciones.AddPolicy(Deposito, p => p.RequireAuthenticatedUser().RequireRole(RolOperario));
        opciones.AddPolicy(Despacho, p => p.RequireAuthenticatedUser().RequireRole(RolDespachador, RolOperario));
        opciones.AddPolicy(Supervision, p => p.RequireAuthenticatedUser().RequireRole(RolAdministrador, RolDespachador));
        opciones.AddPolicy(PersonalDelOperador, p => p.RequireAuthenticatedUser()
            .RequireRole(RolAdministrador, RolDespachador, RolOperario, RolRepartidor));
        opciones.AddPolicy(ConsultaDeEnvios, p => p.RequireAuthenticatedUser()
            .RequireRole(RolDespachador, RolOperario, RolComercio));
        opciones.AddPolicy(ListadoDeEnvios, p => p.RequireAuthenticatedUser()
            .RequireRole(RolAdministrador, RolDespachador, RolOperario, RolComercio));
        opciones.AddPolicy(Repartidor, p => p.RequireAuthenticatedUser().RequireRole(RolRepartidor));
        opciones.AddPolicy(Comercio, p => p.RequireAuthenticatedUser().RequireRole(RolComercio));
        opciones.AddPolicy(Plataforma, p => p.RequireAuthenticatedUser().RequireRole(RolPlataforma));
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
