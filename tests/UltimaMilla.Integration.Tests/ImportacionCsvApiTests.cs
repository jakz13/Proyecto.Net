using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Importación masiva de envíos por CSV con una sesión real de comercio: archivo con filas válidas y con errores,
/// reimportación idempotente y los rechazos de archivo vacío o con columnas faltantes.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class ImportacionCsvApiTests
{
    private const string Encabezado = "referencia,modalidad,destinatario,calle,numero,ciudad,peso,alto,ancho,profundo,documento,email,telefono";

    private readonly ApiFactory _factory;

    public ImportacionCsvApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ComercioAsync()
    {
        var cliente = _factory.CreateClient();
        var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = "comercio@gmail.com", password = "Comercio.2026!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        return cliente;
    }

    private static Task<HttpResponseMessage> SubirAsync(HttpClient cliente, string contenido, string nombre = "envios.csv")
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(contenido));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        formulario.Add(archivo, "archivo", nombre);
        return cliente.PostAsync("/api/comercio/envios/importacion", formulario);
    }

    [FactPostgres]
    public async Task ElArchivoConFilasValidasYConError_ReportaCadaRegistro_YReimportarNoDuplica()
    {
        var comercio = await ComercioAsync();
        var lote = Guid.NewGuid().ToString("N")[..8];
        var csv = string.Join("\n", new[]
        {
            Encabezado,
            $"CSV-{lote}-1,Estandar,Ana Gomez,Av. Italia,100,Montevideo,2.5,10,20,30,1234567-8,ana@correo.com,099111222",
            $"CSV-{lote}-2,Estandar,Luis Perez,Rivera,200,Montevideo,1,10,10,10,,,",
            $"CSV-{lote}-3,Estandar,Sin Peso,Rivera,300,Montevideo,pesado,10,10,10,,,"
        });

        var primera = await SubirAsync(comercio, csv);
        Assert.True(primera.StatusCode == HttpStatusCode.OK, await primera.Content.ReadAsStringAsync());
        var resultado = await primera.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(resultado.GetProperty("reimportacion").GetBoolean());
        Assert.Equal(3, resultado.GetProperty("totalRegistros").GetInt32());
        Assert.Equal(2, resultado.GetProperty("creados").GetInt32());
        Assert.Equal(1, resultado.GetProperty("conError").GetInt32());
        Assert.Equal(3, resultado.GetProperty("filas").GetArrayLength());

        // Mismo archivo: se reconoce como el mismo lote y no se crean envíos de nuevo
        var segunda = await SubirAsync(comercio, csv);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var reimportado = await segunda.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(reimportado.GetProperty("reimportacion").GetBoolean());
        Assert.Equal(0, reimportado.GetProperty("creados").GetInt32());

        // Otro archivo con una referencia ya importada: esa fila es un duplicado, no un envío nuevo
        var conRepetida = await SubirAsync(comercio, string.Join("\n", new[]
        {
            Encabezado,
            $"CSV-{lote}-1,Estandar,Ana Gomez,Av. Italia,100,Montevideo,2.5,10,20,30,,,",
            $"CSV-{lote}-4,Estandar,Marta Diaz,Rivera,400,Montevideo,3,10,10,10,,,"
        }));
        var tercera = await conRepetida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, tercera.GetProperty("creados").GetInt32());
        Assert.Equal(1, tercera.GetProperty("duplicados").GetInt32());
    }

    [FactPostgres]
    public async Task ElSeparadorPuntoYComaYLosArchivosInvalidos_SeManejanConMensajesClaros()
    {
        var comercio = await ComercioAsync();
        var lote = Guid.NewGuid().ToString("N")[..8];

        var conPuntoYComa = await SubirAsync(comercio, string.Join("\n", new[]
        {
            Encabezado.Replace(',', ';'),
            $"CSV-{lote}-PC;Estandar;Rosa Lima;Av. Italia;5;Montevideo;1,5;10;10;10;;;"
        }));
        var resultado = await conPuntoYComa.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, resultado.GetProperty("creados").GetInt32());

        var sinColumnas = await SubirAsync(comercio, "referencia,destinatario\nX,Y");
        Assert.Equal(HttpStatusCode.BadRequest, sinColumnas.StatusCode);
        Assert.Contains("Faltan columnas", await sinColumnas.Content.ReadAsStringAsync());

        var vacio = await SubirAsync(comercio, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
    }

    [FactPostgres]
    public async Task ElPersonalDelOperador_NoPuedeImportarEnviosDeUnComercio()
    {
        var admin = _factory.CreateClient();
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@gmail.com", password = "Admin.2026!" });
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await SubirAsync(admin, Encabezado)).StatusCode);
    }
}
