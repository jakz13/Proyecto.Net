using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Mocks;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias para MockEnviosApi validando la simulación en memoria de UC-25 (importación masiva y reimportación idempotente).
/// </summary>
public class MockEnviosApiTests
{
    private const string CsvValido = """
    referencia,modalidad,destinatario,calle,numero,ciudad,peso,alto,ancho,profundo
    REF-MOCK-101,Estandar,Martín Silva,18 de Julio,1420,Montevideo,2.5,10,15,20
    REF-MOCK-102,Estandar,Valeria Rossi,Av. Rivera,3200,Montevideo,1.2,5,10,15
    """;

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConCsvValido_ProcesaRegistrosYCalculaTarifas()
    {
        // Arrange
        var api = new MockEnviosApi();
        var bytes = System.Text.Encoding.UTF8.GetBytes(CsvValido);
        var claveUnica = "lote-test-" + Guid.NewGuid();

        // Act
        var resultado = await api.ImportarEnviosPorArchivoAsync(bytes, "lote.csv", claveUnica);

        // Assert
        Assert.NotNull(resultado);
        Assert.False(resultado.Reimportacion);
        Assert.Equal(2, resultado.TotalRegistros);
        Assert.Equal(2, resultado.Creados);
        Assert.Equal(0, resultado.Duplicados);
        Assert.Equal(0, resultado.ConError);
        Assert.All(resultado.Filas, f =>
        {
            Assert.Equal(ResultadoFilaImportacionDto.Creado, f.Estado);
            Assert.NotNull(f.CodigoSeguimiento);
            Assert.True(f.Tarifa > 0);
        });
    }

    [Fact]
    public async Task ImportarEnviosPorArchivo_ReimportandoMismoLote_IdentificaIdempotenciaYNoDuplica()
    {
        // Arrange
        var api = new MockEnviosApi();
        var bytes = System.Text.Encoding.UTF8.GetBytes(CsvValido);
        var clave = "lote-idempotente-" + Guid.NewGuid();

        // Primera importación
        var primerResultado = await api.ImportarEnviosPorArchivoAsync(bytes, "lote.csv", clave);
        Assert.False(primerResultado.Reimportacion);
        Assert.Equal(2, primerResultado.Creados);

        // Act: Reimportar el mismo lote con la misma clave
        var reimportacionResultado = await api.ImportarEnviosPorArchivoAsync(bytes, "lote.csv", clave);

        // Assert
        Assert.NotNull(reimportacionResultado);
        Assert.True(reimportacionResultado.Reimportacion);
        Assert.Equal(0, reimportacionResultado.Creados);
        Assert.Equal(2, reimportacionResultado.Duplicados);
        Assert.All(reimportacionResultado.Filas, f => Assert.Equal(ResultadoFilaImportacionDto.Duplicado, f.Estado));
    }

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConColumnasFaltantes_LanzaApiExcepcionBadRequest()
    {
        // Arrange
        var api = new MockEnviosApi();
        var csvInvalido = "referencia,destinatario\nREF-1,Juan";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csvInvalido);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApiExcepcion>(() =>
            api.ImportarEnviosPorArchivoAsync(bytes, "invalido.csv"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, ex.CodigoEstado);
        Assert.Contains("Faltan columnas obligatorias", ex.Message);
    }
}
