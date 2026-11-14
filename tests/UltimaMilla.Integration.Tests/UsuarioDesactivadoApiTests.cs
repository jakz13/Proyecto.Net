using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>Un token abierto deja de valer en cuanto el administrador desactiva al usuario (auditoría del 14/11, D-12).</summary>
[Collection(ColeccionApi.Nombre)]
public class UsuarioDesactivadoApiTests
{
    private readonly ApiFactory _factory;

    public UsuarioDesactivadoApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteComoAsync(string email, string password)
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    [FactPostgres]
    public async Task UnTokenAbierto_DejaDeValerCuandoElAdministradorDesactivaAlUsuario()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var email = $"desactivar.{sufijo}@ultimamilla.com.uy";
        var admin = await ClienteComoAsync("admin@gmail.com", "Admin.2026!");

        var alta = await admin.PostAsJsonAsync("/api/usuarios", new
        {
            nombre = "Usuario", apellido = "Desactivable", email, password = "Despacho.2026!", perfilId = DatabaseSeeder.DefaultPerfilDespachadorId
        });
        Assert.True(alta.StatusCode == HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var usuarioId = (await alta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var despachador = await ClienteComoAsync(email, "Despacho.2026!");
        Assert.Equal(HttpStatusCode.OK, (await despachador.GetAsync("/api/planificacion/hojas-de-ruta/")).StatusCode);

        var baja = await admin.PatchAsJsonAsync($"/api/usuarios/{usuarioId}/estado", new { activo = false });
        Assert.True(baja.IsSuccessStatusCode, await baja.Content.ReadAsStringAsync());

        // El token seguía vigente (vence a las 8 horas), pero el usuario ya no está activo
        var conTokenViejo = await despachador.GetAsync("/api/planificacion/hojas-de-ruta/");
        Assert.Equal(HttpStatusCode.Unauthorized, conTokenViejo.StatusCode);
        Assert.Contains(UltimaMilla.Api.Auth.UsuarioActivoMiddleware.CodigoError, await conTokenViejo.Content.ReadAsStringAsync());

        // Y al reactivarlo vuelve a funcionar (la baja no destruye nada)
        var alta2 = await admin.PatchAsJsonAsync($"/api/usuarios/{usuarioId}/estado", new { activo = true });
        Assert.True(alta2.IsSuccessStatusCode, await alta2.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await despachador.GetAsync("/api/planificacion/hojas-de-ruta/")).StatusCode);
    }
}
