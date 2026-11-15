using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Integration.Tests;

/// <summary>La dirección del depósito la escribe el administrador en la configuración de datos del operador; no viene fijada en Montevideo.</summary>
[Collection(ColeccionApi.Nombre)]
public class DepositoDelOperadorApiTests
{
    private readonly ApiFactory _factory;

    public DepositoDelOperadorApiTests(ApiFactory factory) => _factory = factory;

    [FactPostgres]
    public async Task ElOperadorNoTieneDepositoHastaQueElAdministradorLoCarga_YSeConservaAlGuardarOtrosDatos()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@gmail.com", password = "Admin.2026!" });
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());

        var inicial = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/identidad-visual");
        Assert.True(inicial.GetProperty("direccionDeposito").ValueKind == JsonValueKind.Null, "No debe haber un depósito preestablecido.");

        // El administrador lo carga (aquí directo en la base para no depender del servicio de mapas)
        using (var alcance = _factory.Services.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            using var _ = db.HabilitarEscrituraAdministrativa();
            var identidad = await db.IdentidadesVisuales.IgnoreQueryFilters().FirstAsync(i => i.OperadorId == DatabaseSeeder.DefaultOperadorId);
            identidad.ActualizarDeposito("Av. Italia 3456, Montevideo", -34.8941, -56.1389);
            await db.SaveChangesAsync();

            // La identidad se sirve desde la caché: se invalida como lo hace el guardado real
            await alcance.ServiceProvider.GetRequiredService<UltimaMilla.Application.Ports.IDistributedCacheService>()
                .RemoveAsync(UltimaMilla.Application.Common.ClavesCache.IdentidadVisualPorOperador(DatabaseSeeder.DefaultOperadorId));
        }

        var cargada = await cliente.GetFromJsonAsync<JsonElement>("/api/v1/identidad-visual");
        Assert.Equal("Av. Italia 3456, Montevideo", cargada.GetProperty("direccionDeposito").GetString());
        Assert.Equal(-34.8941, cargada.GetProperty("depositoLatitud").GetDouble(), 4);

        // Guardar la identidad con la misma dirección no vuelve a ubicarla ni la pierde
        var guardado = await cliente.PutAsJsonAsync("/api/v1/identidad-visual", new
        {
            nombreComercial = cargada.GetProperty("nombreComercial").GetString(),
            colorPrimario = cargada.GetProperty("colorPrimario").GetString(),
            colorSecundario = cargada.GetProperty("colorSecundario").GetString(),
            emailContacto = cargada.GetProperty("emailContacto").GetString(),
            telefonoContacto = cargada.GetProperty("telefonoContacto").GetString(),
            direccionDeposito = "Av. Italia 3456, Montevideo"
        });
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        Assert.Equal(-34.8941, (await guardado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("depositoLatitud").GetDouble(), 4);

        // Vaciarla la quita
        var vaciado = await cliente.PutAsJsonAsync("/api/v1/identidad-visual", new
        {
            nombreComercial = cargada.GetProperty("nombreComercial").GetString(),
            colorPrimario = cargada.GetProperty("colorPrimario").GetString(),
            colorSecundario = cargada.GetProperty("colorSecundario").GetString(),
            emailContacto = cargada.GetProperty("emailContacto").GetString(),
            telefonoContacto = cargada.GetProperty("telefonoContacto").GetString(),
            direccionDeposito = ""
        });
        Assert.Equal(HttpStatusCode.OK, vaciado.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await vaciado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("direccionDeposito").ValueKind);
    }
}
