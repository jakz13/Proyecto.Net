using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// El administrador de la plataforma da de alta una empresa de envíos y su administrador inicial es obligatorio: con esa cuenta la empresa
/// entra al Backoffice y crea los demás perfiles. Empresa y administrador se guardan juntos o no se guarda nada.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AltaDeEmpresaDeEnviosApiTests
{
    private readonly ApiFactory _factory;

    public AltaDeEmpresaDeEnviosApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        return cliente;
    }

    private static object Alta(string nombre, string email, string password = "Clave.Segura2026!") => new
    {
        razonSocial = nombre + " S.A.",
        nombreComercial = nombre,
        administrador = new { nombre = "Admin " + nombre, email, password }
    };

    [FactPostgres]
    public async Task LaPlataformaCreaLaEmpresaConSuAdministrador_YEsteEntraYCreaOtrosPerfiles()
    {
        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var email = $"admin.{sufijo}@empresa.uy";

        var alta = await plataforma.PostAsJsonAsync("/api/plataforma/operadores", Alta("Empresa " + sufijo, email));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var operadorId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("operadorId").GetGuid();

        // Aparece en el listado de la plataforma, con todas las funcionalidades habilitadas
        var listado = await plataforma.GetFromJsonAsync<JsonElement>("/api/plataforma/operadores");
        Assert.Contains(listado.EnumerateArray(), o => o.GetProperty("operadorId").GetGuid() == operadorId);

        // Su administrador entra con la cuenta creada y ve solo su empresa
        var admin = await ClienteComoAsync(email, "Clave.Segura2026!");
        var identidad = await admin.GetFromJsonAsync<JsonElement>("/api/v1/identidad-visual");
        Assert.Equal("Empresa " + sufijo, identidad.GetProperty("nombreComercial").GetString());

        // ...y puede empezar a crear los demás perfiles
        var perfiles = await admin.GetFromJsonAsync<JsonElement>("/api/perfiles");
        var despachador = perfiles.EnumerateArray().First(p => p.GetProperty("nombre").GetString() == "Despachador").GetProperty("id").GetGuid();
        var usuario = await admin.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Despachador " + sufijo, email = $"desp.{sufijo}@empresa.uy", password = "Clave.Segura2026!", perfilId = despachador
        });
        Assert.Equal(HttpStatusCode.Created, usuario.StatusCode);
    }

    [FactPostgres]
    public async Task ElAdministradorInicialEsObligatorio_YNoSeGuardaNadaSiFalta()
    {
        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var sinAdmin = await plataforma.PostAsJsonAsync("/api/plataforma/operadores", new { razonSocial = "Sin Admin S.A.", nombreComercial = "Sin Admin " + sufijo });
        Assert.Equal(HttpStatusCode.BadRequest, sinAdmin.StatusCode);

        var sinClave = await plataforma.PostAsJsonAsync("/api/plataforma/operadores", new
        {
            razonSocial = "X S.A.", nombreComercial = "Sin Clave " + sufijo, administrador = new { nombre = "Admin", email = $"a.{sufijo}@x.uy", password = "" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinClave.StatusCode);

        var claveDebil = await plataforma.PostAsJsonAsync("/api/plataforma/operadores", Alta("Debil " + sufijo, $"d.{sufijo}@x.uy", "123"));
        Assert.Equal(HttpStatusCode.BadRequest, claveDebil.StatusCode);

        // Nada de lo anterior dejó una empresa a medias
        var listado = await plataforma.GetFromJsonAsync<JsonElement>("/api/plataforma/operadores");
        Assert.DoesNotContain(listado.EnumerateArray(), o => (o.GetProperty("nombre").GetString() ?? "").EndsWith(sufijo));
    }

    [FactPostgres]
    public async Task NoSeAdmiteUnNombreOUnCorreoRepetido_YSoloLaPlataformaDaDeAlta()
    {
        var plataforma = await ClienteComoAsync("plataforma@gmail.com", "Plataforma.2026!");
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var nombre = "Repetida " + sufijo;

        Assert.Equal(HttpStatusCode.Created, (await plataforma.PostAsJsonAsync("/api/plataforma/operadores", Alta(nombre, $"r1.{sufijo}@x.uy"))).StatusCode);

        // Mismo nombre (aunque cambien mayúsculas) y mismo correo
        Assert.Equal(HttpStatusCode.BadRequest, (await plataforma.PostAsJsonAsync("/api/plataforma/operadores", Alta(nombre.ToUpperInvariant(), $"r2.{sufijo}@x.uy"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await plataforma.PostAsJsonAsync("/api/plataforma/operadores", Alta("Otra " + sufijo, $"r1.{sufijo}@x.uy"))).StatusCode);

        // El administrador de una empresa no crea otras empresas
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/plataforma/operadores", Alta("Intrusa " + sufijo, $"i.{sufijo}@x.uy"))).StatusCode);
    }
}
