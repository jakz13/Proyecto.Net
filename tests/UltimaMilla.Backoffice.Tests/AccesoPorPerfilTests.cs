using Xunit;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Cada perfil del Backoffice (administrador, despachador y operario) solo debe ver y poder abrir lo que le corresponde.
/// Se inicia sesión con cada perfil y se rastrean TODAS las páginas alcanzables siguiendo cada enlace (menú lateral, botones,
/// tarjetas, redirecciones): ninguno puede terminar en "Acceso denegado". Además el menú lateral de cada perfil debe ser exactamente
/// el conjunto de pantallas que ese perfil puede abrir.
/// </summary>
public class AccesoPorPerfilTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Administrador = "Administrador";
    private const string Despachador = "Despachador";
    private const string Operario = "Operario";

    /// <summary>
    /// Quién puede abrir cada carpeta de páginas. Es la regla de negocio contra la que se contrastan el servidor y las vistas:
    /// administración (solo administrador), planificación (solo despachador), depósito (solo operario), despacho (despachador y operario).
    /// </summary>
    private static readonly (string Ruta, string[] Perfiles)[] Pantallas =
    {
        ("/", new[] { Administrador, Despachador, Operario }),

        // Administración: solo el administrador configura la operación
        ("/Comercios", new[] { Administrador }),
        ("/Comercios/Nuevo", new[] { Administrador }),
        ("/Usuarios/Nuevo", new[] { Administrador }),
        ("/Zonas", new[] { Administrador }),
        ("/Zonas/Nueva", new[] { Administrador }),
        ("/Tarifas", new[] { Administrador }),
        ("/Tarifas/NuevaVersion", new[] { Administrador }),
        ("/ReglasOperativas", new[] { Administrador }),
        ("/ReglasOperativas/NuevaVersion", new[] { Administrador }),
        ("/MotivosNoEntrega", new[] { Administrador }),
        ("/IdentidadVisual", new[] { Administrador }),
        ("/Flota", new[] { Administrador }),
        ("/Mensajeria", new[] { Administrador }),
        ("/Observabilidad", new[] { Administrador }),

        // Planificación: solo el despachador
        ("/Rutas", new[] { Despachador }),
        ("/Rutas/Armar", new[] { Despachador }),
        ("/Envios", new[] { Despachador }),
        ("/Incidencias", new[] { Despachador }),
        ("/Tablero", new[] { Despachador }),

        // Reportes de gestión: administrador y despachador
        ("/Reportes", new[] { Administrador, Despachador }),

        // Despacho y retiros: despachador y operario
        ("/Despacho", new[] { Despachador, Operario }),
        ("/Retiros", new[] { Despachador, Operario }),

        // Depósito: solo el operario
        ("/Deposito", new[] { Operario }),
        ("/Deposito/Clasificacion", new[] { Operario }),
        ("/Deposito/Rendicion", new[] { Operario }),
    };

    /// <summary>Opciones que debe mostrar el menú lateral de cada perfil, según lo que la letra le asigna.</summary>
    private static readonly Dictionary<string, string[]> MenuEsperado = new()
    {
        [Administrador] = new[]
        {
            "/", "/Comercios", "/Zonas", "/Tarifas", "/ReglasOperativas", "/MotivosNoEntrega", "/IdentidadVisual",
            "/Flota", "/Mensajeria", "/Observabilidad", "/Reportes"
        },
        [Despachador] = new[] { "/", "/Rutas", "/Envios", "/Incidencias", "/Tablero", "/Despacho", "/Retiros", "/Reportes" },
        [Operario] = new[] { "/", "/Despacho", "/Retiros", "/Deposito", "/Deposito/Clasificacion", "/Deposito/Rendicion" },
    };

    private static readonly Regex Href = new("<a\\b[^>]*?\\bhref=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FormAction = new("<(?:form|button)\\b[^>]*?\\b(?:action|formaction)=\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Menu = new("<nav[^>]*id=\"sidebarPrincipal\".*?</nav>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly WebApplicationFactory<Program> _fabrica;

    public AccesoPorPerfilTests(WebApplicationFactory<Program> fabrica)
    {
        _fabrica = fabrica;
    }

    public static IEnumerable<object[]> Perfiles() => new[] { Administrador, Despachador, Operario }.Select(p => new object[] { p });

    private async Task<HttpClient> SesionComoAsync(string perfil)
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

        var login = await cliente.GetAsync("/Login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Token.Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "La página de inicio de sesión no trae el token antiforgery.");

        var respuesta = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = perfil,
            ["NombreUsuario"] = "Prueba " + perfil,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    private static bool EsAccesoDenegado(HttpResponseMessage respuesta) =>
        respuesta.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Found or HttpStatusCode.Forbidden
        && (respuesta.StatusCode == HttpStatusCode.Forbidden
            || (respuesta.Headers.Location?.ToString().Contains("/AccesoDenegado", StringComparison.OrdinalIgnoreCase) ?? false));

    private static bool EsRutaDePagina(string url, out string ruta)
    {
        ruta = string.Empty;
        var decodificada = WebUtility.HtmlDecode(url).Trim();
        if (decodificada.Length == 0 || decodificada.StartsWith('#') || decodificada.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || decodificada.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || decodificada.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            || decodificada.StartsWith("//"))
            return false;

        var sinConsulta = decodificada.Split('?', '#')[0];
        // Recursos estáticos, cierre de sesión y cambio de perfil (pantallas públicas) no son vistas con permisos
        if (Regex.IsMatch(sinConsulta, @"\.(css|js|png|jpe?g|gif|svg|ico|woff2?|map)$", RegexOptions.IgnoreCase)
            || sinConsulta.StartsWith("/lib") || sinConsulta.StartsWith("/css") || sinConsulta.StartsWith("/js") || sinConsulta.StartsWith("/bootstrap")
            || sinConsulta.Equals("/Logout", StringComparison.OrdinalIgnoreCase) || sinConsulta.Equals("/Login", StringComparison.OrdinalIgnoreCase)
            || sinConsulta.Equals("/AccesoDenegado", StringComparison.OrdinalIgnoreCase) || sinConsulta.Equals("/Error", StringComparison.OrdinalIgnoreCase)
            || sinConsulta.Equals("/Privacy", StringComparison.OrdinalIgnoreCase))
            return false;

        // Los manejadores (?handler=) devuelven datos de la misma página que ya se abrió: no son otra vista
        if (decodificada.Contains("handler=", StringComparison.OrdinalIgnoreCase))
            return false;

        ruta = decodificada.StartsWith('/') ? decodificada : "/" + decodificada;
        return true;
    }

    [Theory]
    [MemberData(nameof(Perfiles))]
    public async Task NingunEnlaceAlcanzableLlevaAUnaVistaProhibidaParaElPerfil(string perfil)
    {
        var cliente = await SesionComoAsync(perfil);
        var pendientes = new Queue<(string Url, string Origen)>();
        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prohibidos = new List<string>();

        pendientes.Enqueue(("/", "(inicio de sesión)"));
        // Además de todo lo que se descubra, se siembra cada pantalla permitida: algunas solo se llega a ellas desde un botón
        foreach (var (ruta, perfiles) in Pantallas.Where(p => p.Perfiles.Contains(perfil)))
            pendientes.Enqueue((ruta, "(pantalla permitida del perfil)"));

        while (pendientes.Count > 0 && vistas.Count < 400)
        {
            var (url, origen) = pendientes.Dequeue();
            if (!vistas.Add(url))
                continue;

            var respuesta = await cliente.GetAsync(url);

            if (EsAccesoDenegado(respuesta))
            {
                prohibidos.Add($"{perfil}: «{url}» (enlazado desde {origen}) lleva a Acceso denegado");
                continue;
            }

            // Redirecciones internas: se sigue el destino como si fuera un enlace más
            if ((int)respuesta.StatusCode is >= 300 and < 400)
            {
                var destino = respuesta.Headers.Location?.ToString();
                if (destino is not null && EsRutaDePagina(destino, out var rutaDestino))
                    pendientes.Enqueue((rutaDestino, $"redirección de {url}"));
                continue;
            }

            if (respuesta.StatusCode != HttpStatusCode.OK || respuesta.Content.Headers.ContentType?.MediaType != "text/html")
                continue;

            var html = await respuesta.Content.ReadAsStringAsync();
            foreach (var coincidencia in Href.Matches(html).Concat(FormAction.Matches(html)))
            {
                if (EsRutaDePagina(coincidencia.Groups[1].Value, out var enlace))
                    pendientes.Enqueue((enlace, url));
            }
        }

        Assert.True(prohibidos.Count == 0, "Enlaces a vistas que el perfil no puede abrir:\n" + string.Join("\n", prohibidos.Distinct()));
        Assert.True(vistas.Count > 10, "El rastreo casi no encontró páginas: la sesión no se inició bien.");
    }

    [Theory]
    [MemberData(nameof(Perfiles))]
    public async Task ElServidorDejaEntrarSoloALosPerfilesAutorizadosEnCadaPantalla(string perfil)
    {
        var cliente = await SesionComoAsync(perfil);
        var errores = new List<string>();

        foreach (var (ruta, perfiles) in Pantallas)
        {
            var respuesta = await cliente.GetAsync(ruta);
            var denegado = EsAccesoDenegado(respuesta);
            var debeEntrar = perfiles.Contains(perfil);

            if (debeEntrar && denegado)
                errores.Add($"{perfil} debería poder abrir {ruta} y recibió Acceso denegado");
            if (!debeEntrar && !denegado)
                errores.Add($"{perfil} NO debería poder abrir {ruta} y recibió {(int)respuesta.StatusCode}");
        }

        Assert.True(errores.Count == 0, string.Join("\n", errores));
    }

    [Theory]
    [MemberData(nameof(Perfiles))]
    public async Task ElMenuLateralMuestraExactamenteLasPantallasDelPerfil(string perfil)
    {
        var cliente = await SesionComoAsync(perfil);
        var html = await (await cliente.GetAsync("/")).Content.ReadAsStringAsync();

        var menu = Menu.Match(html);
        Assert.True(menu.Success, "No se encontró el menú lateral.");

        var enMenu = Href.Matches(menu.Value)
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Split('?')[0])
            .Where(h => EsRutaDePagina(h, out _) || h == "/")
            .Select(h => h.TrimEnd('/').Length == 0 ? "/" : h.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h)
            .ToList();

        var esperado = MenuEsperado[perfil].OrderBy(h => h).ToList();
        Assert.Equal(esperado, enMenu);
    }
}
