using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Sesiones reales de despachador y operario para las pruebas. El administrador ya no puede planificar ni trabajar en el depósito
/// (la letra le asigna solo la configuración), así que estas pruebas actúan como lo haría cada perfil. Las cuentas se crean con la API
/// (por el administrador) la primera vez y se reutilizan; no se usa <c>despacho@gmail.com</c>, que otra prueba bloquea a propósito.
/// </summary>
internal static class UsuariosDePrueba
{
    private const string PasswordDespachador = "Despacha.Prueba.2026!";
    private const string PasswordOperario = "Opera.Prueba.2026!";
    private static readonly SemaphoreSlim Candado = new(1, 1);

    public static Task<HttpClient> DespachadorAsync(ApiFactory fabrica) =>
        ComoAsync(fabrica, "prueba.despachador@ultimamilla.com.uy", PasswordDespachador, DatabaseSeeder.DefaultPerfilDespachadorId, "Despachador");

    public static Task<HttpClient> OperarioAsync(ApiFactory fabrica) =>
        ComoAsync(fabrica, "prueba.operario@ultimamilla.com.uy", PasswordOperario, DatabaseSeeder.DefaultPerfilOperarioId, "Operario");

    /// <summary>Despachador del segundo operador (el de <c>admin2@gmail.com</c>, sin datos propios), creado por su administrador.</summary>
    public static Task<HttpClient> DespachadorDelOperadorDosAsync(ApiFactory fabrica) =>
        ComoAsync(fabrica, "prueba.despachador.dos@ultimamilla.com.uy", PasswordDespachador, DatabaseSeeder.DefaultPerfilDespachadorId, "Despachador",
            emailAdmin: "admin2@gmail.com");

    private static async Task<HttpClient> ComoAsync(
        ApiFactory fabrica, string email, string password, Guid perfilId, string apellido, string emailAdmin = "admin@gmail.com")
    {
        var cliente = fabrica.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        if (login.StatusCode == HttpStatusCode.Unauthorized)
        {
            await Candado.WaitAsync();
            try
            {
                var reintento = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
                if (reintento.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var admin = fabrica.CreateClient();
                    var sesionAdmin = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email = emailAdmin, password = "Admin.2026!" });
                    var token = (await sesionAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
                    admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var alta = await admin.PostAsJsonAsync("/api/usuarios", new { nombre = "Prueba", apellido, email, password, perfilId });
                    Assert.True(alta.StatusCode == HttpStatusCode.Created, $"No se pudo crear la cuenta de prueba {email}: {await alta.Content.ReadAsStringAsync()}");
                }

                login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
            }
            finally
            {
                Candado.Release();
            }
        }

        Assert.True(login.StatusCode == HttpStatusCode.OK, $"Inicio de sesión de {email}: {(int)login.StatusCode}");
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }
}
