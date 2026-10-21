using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Persistence;
using DatabaseSeeder = UltimaMilla.Infrastructure.Persistence.DatabaseSeeder;

namespace UltimaMilla.Integration.Tests;

/// <summary>Altas por portal, API (UC-26) y archivo (UC-25) contra Postgres real.</summary>
[Collection(ColeccionApi.Nombre)]
public class CanalesDeAltaApiTests
{
    private const string ClaveApiDemo = "dev-api-key-megastore-2026";

    private readonly ApiFactory _factory;

    public CanalesDeAltaApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static object AltaApi(string referencia, string nombre = "Ana Gomez") => new
    {
        modalidadServicio = "Estandar",
        nombreDestinatario = nombre,
        calle = "18 de Julio",
        numero = "1234",
        ciudad = "Montevideo",
        bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } },
        referenciaExterna = referencia
    };

    private HttpClient ClienteApi(string clave = ClaveApiDemo)
    {
        var cliente = _factory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Api-Key", clave);
        return cliente;
    }

    private async Task<int> ContarEnviosConPrefijoAsync(string prefijo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Envios.CountAsync(e => e.ReferenciaExterna != null && e.ReferenciaExterna.StartsWith(prefijo));
    }

    [FactPostgres]
    public async Task Api_ReintentoConLaMismaReferencia_DevuelveElMismoEnvioSinDuplicar()
    {
        var cliente = ClienteApi();

        var primera = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-REINTENTO-1"));
        var segunda = await cliente.PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-REINTENTO-1"));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var a = await primera.Content.ReadFromJsonAsync<JsonElement>();
        var b = await segunda.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("codigoSeguimiento").GetString(), b.GetProperty("codigoSeguimiento").GetString());
        Assert.True(b.GetProperty("duplicado").GetBoolean());
        Assert.Equal(1, await ContarEnviosConPrefijoAsync("IT-REINTENTO-1"));
    }

    [FactPostgres]
    public async Task Api_SolicitudesSimultaneasConLaMismaReferencia_NuncaDuplicanNiDanError500()
    {
        var cliente = ClienteApi();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => cliente.PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-CARRERA-1"))));

        Assert.All(respuestas, r => Assert.True(
            r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Respuesta inesperada: {(int)r.StatusCode}"));
        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, await ContarEnviosConPrefijoAsync("IT-CARRERA-1"));
    }

    [FactPostgres]
    public async Task Api_ClaveInvalidaOAusente_Da401YNoCreaNada()
    {
        var conClaveMala = await ClienteApi("clave-mala").PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-SINCLAVE-1"));
        var sinClave = await _factory.CreateClient().PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-SINCLAVE-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, conClaveMala.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinClave.StatusCode);
        Assert.Equal(0, await ContarEnviosConPrefijoAsync("IT-SINCLAVE-1"));
    }

    [FactPostgres]
    public async Task Api_NombreDemasiadoLargo_Da400EnVezDeError500()
    {
        var respuesta = await ClienteApi().PostAsJsonAsync("/api/integracion/v1/envios", AltaApi("IT-LARGO-1", new string('x', 250)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [FactPostgres]
    public async Task Portal_ElComercioDelCuerpoSeIgnora_SeUsaElDeLaSesion()
    {
        var comercioAjeno = Guid.NewGuid();
        var cuerpo = new
        {
            comercioId = comercioAjeno,
            modalidadServicio = "Estandar",
            nombreDestinatario = "Ana",
            calle = "Calle",
            numero = "1",
            ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        };

        var respuesta = await _factory.CreateClient().PostAsJsonAsync("/api/envios", cuerpo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var id = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = _factory.Services.CreateScope();
        var envio = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Envios.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.NotEqual(comercioAjeno, envio.ComercioOperadorId);
        Assert.Equal(DatabaseSeeder.DefaultComercioOperadorId, envio.ComercioOperadorId);
    }

    [FactPostgres]
    public async Task ListadoDeEnvios_NoIncluyeEnviosDeOtroOperador()
    {
        var ajeno = new UltimaMilla.Domain.Entities.Envios.Envio(
            Guid.NewGuid(), Guid.NewGuid(), "Calle ajena 1", "Cliente ajeno", "TRK-OTROOPER01", Guid.NewGuid());
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Envios.Add(ajeno);
            await db.SaveChangesAsync();
        }

        var listado = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/envios");

        Assert.DoesNotContain(listado.EnumerateArray(), e => e.GetProperty("id").GetGuid() == ajeno.Id);
        Assert.True(listado.GetArrayLength() > 0);
    }

    private static MultipartFormDataContent Archivo(byte[] contenido)
    {
        var form = new MultipartFormDataContent();
        var parte = new ByteArrayContent(contenido);
        form.Add(parte, "archivo", "lote.csv");
        return form;
    }

    private const string Encabezado = "referencia,modalidad,destinatario,calle,numero,ciudad,peso,alto,ancho,profundo\n";

    [FactPostgres]
    public async Task Importacion_MismoArchivoDosVeces_NoDuplicaYInformaPorFila()
    {
        var csv = Encabezado +
                  "IT-CSV-1,Estandar,Ana,Calle,1,Montevideo,2,10,10,10\n" +
                  "IT-CSV-2,Estandar,Luis,Calle,2,Montevideo,2,10,10,10\n" +
                  "IT-CSV-3,Estandar,Eva,Calle,3,Montevideo,dos,10,10,10\n";
        var bytes = Encoding.UTF8.GetBytes(csv);
        var cliente = _factory.CreateClient();

        var primera = await (await cliente.PostAsync("/api/comercio/envios/importacion", Archivo(bytes))).Content.ReadFromJsonAsync<JsonElement>();
        var segunda = await (await cliente.PostAsync("/api/comercio/envios/importacion", Archivo(bytes))).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, primera.GetProperty("creados").GetInt32());
        Assert.Equal(1, primera.GetProperty("conError").GetInt32());
        Assert.True(segunda.GetProperty("reimportacion").GetBoolean());
        Assert.Equal(0, segunda.GetProperty("creados").GetInt32());
        Assert.Equal(2, segunda.GetProperty("duplicados").GetInt32());
        Assert.Equal(2, await ContarEnviosConPrefijoAsync("IT-CSV-"));
    }

    [FactPostgres]
    public async Task Importacion_ArchivoEnLatin1_ConservaLasTildes()
    {
        var csv = Encabezado + "IT-LATIN-1,Estandar,José Ñandú,Avenida Italia,1,Montevideo,2,10,10,10\n";
        var bytes = Encoding.Latin1.GetBytes(csv);

        var respuesta = await _factory.CreateClient().PostAsync("/api/comercio/envios/importacion", Archivo(bytes));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var envio = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Envios.AsNoTracking()
            .SingleAsync(e => e.ReferenciaExterna == "IT-LATIN-1");
        Assert.Equal("José Ñandú", envio.NombreDestinatario);
    }

    [FactPostgres]
    public async Task Importacion_SinArchivoOSinColumnas_Da400()
    {
        var cliente = _factory.CreateClient();

        var sinColumnas = await cliente.PostAsync("/api/comercio/envios/importacion", Archivo(Encoding.UTF8.GetBytes("referencia,peso\nA,1\n")));
        var vacio = await cliente.PostAsync("/api/comercio/envios/importacion", Archivo(Array.Empty<byte>()));

        Assert.Equal(HttpStatusCode.BadRequest, sinColumnas.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
    }
}
