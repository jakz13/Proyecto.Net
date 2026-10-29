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

    [Fact]
    public async Task CrearEnvioAsync_ConDatosValidos_CalculaTarifaGeneraCodigoYDevuelveEstadoAdmitido()
    {
        // Arrange
        var api = new MockEnviosApi();
        var nuevoEnvio = new CrearEnvioDto
        {
            NombreDestinatario = "Lucía Rodríguez",
            DireccionDestino = "18 de Julio 1234, Montevideo",
            ModalidadServicio = "Estandar",
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 2.0m, Alto = 10m, Ancho = 10m, Profundo = 10m }
            }
        };

        // Act
        var resultado = await api.CrearEnvioAsync(nuevoEnvio);

        // Assert
        Assert.NotNull(resultado);
        Assert.StartsWith("TRK-2026-", resultado.CodigoSeguimiento);
        Assert.Equal(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto.Admitido, resultado.Estado);
        Assert.NotNull(resultado.Tarifa);
        Assert.True(resultado.Tarifa > 0m);
        Assert.Single(resultado.BultosIdentificaciones!);
        Assert.Contains("-B01", resultado.BultosIdentificaciones![0]);
    }

    [Fact]
    public async Task CrearEnvioAsync_ConModalidadExpressYVariosBultos_AplicaRecargosYGeneraIdentificadores()
    {
        // Arrange
        var api = new MockEnviosApi();
        var envioEstandar = new CrearEnvioDto
        {
            NombreDestinatario = "Cliente Base",
            DireccionDestino = "Canelones 890",
            ModalidadServicio = "Estandar",
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 1.0m, Alto = 10m, Ancho = 10m, Profundo = 10m }
            }
        };

        var envioExpressMultiBulto = new CrearEnvioDto
        {
            NombreDestinatario = "Cliente Prioritario",
            DireccionDestino = "Rambla Gandhi 456",
            ModalidadServicio = "Express",
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 1.5m, Alto = 15m, Ancho = 15m, Profundo = 15m },
                new CrearBultoItemDto { Peso = 2.5m, Alto = 20m, Ancho = 20m, Profundo = 20m }
            }
        };

        // Act
        var resEstandar = await api.CrearEnvioAsync(envioEstandar);
        var resExpress = await api.CrearEnvioAsync(envioExpressMultiBulto);

        // Assert
        Assert.True(resExpress.Tarifa > resEstandar.Tarifa, "La tarifa Express multi-bulto debe ser mayor a la Estándar de 1 bulto.");
        Assert.Equal(2, resExpress.BultosIdentificaciones!.Count);
        Assert.Equal("Express", resExpress.ModalidadServicio);
    }

    [Fact]
    public async Task CrearEnvioPorApiIntegracion_ConCredencialValida_CalculaTarifaAdmiteEnvioYGeneraSeguimiento()
    {
        // Arrange
        var api = new MockEnviosApi();
        var claveVigente = await api.ObtenerClaveApiActivaAsync();
        var solicitud = new SolicitudAltaEnvioApiDto
        {
            ClaveApi = claveVigente,
            ModalidadServicio = "Estandar",
            NombreDestinatario = "Sofía Bentancur",
            Calle = "Av. Brasil",
            Numero = "2850",
            Ciudad = "Montevideo",
            ReferenciaExterna = "REF-API-" + Guid.NewGuid(),
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 2.0m, Alto = 10m, Ancho = 15m, Profundo = 20m }
            }
        };

        // Act
        var respuesta = await api.CrearEnvioPorApiIntegracionAsync(solicitud);

        // Assert
        Assert.NotNull(respuesta);
        Assert.NotEqual(Guid.Empty, respuesta.Id);
        Assert.StartsWith("TRK-2026-", respuesta.CodigoSeguimiento);
        Assert.Equal("Admitido", respuesta.Estado);
        Assert.True(respuesta.Tarifa > 0m);
        Assert.False(respuesta.Duplicado);
        Assert.Single(respuesta.Bultos);

        // Verificar postcondición: el envío queda admitido en el listado general igual que por los otros canales
        var envioEnPlataforma = await api.ObtenerPorIdAsync(respuesta.Id);
        Assert.NotNull(envioEnPlataforma);
        Assert.Equal(UltimaMilla.Frontend.Contracts.Enums.EstadoEnvioDto.Admitido, envioEnPlataforma!.Estado);
    }

    [Fact]
    public async Task CrearEnvioPorApiIntegracion_ConMismaReferenciaExterna_EsIdempotenteYNoDuplica()
    {
        // Arrange
        var api = new MockEnviosApi();
        var claveVigente = await api.ObtenerClaveApiActivaAsync();
        var referenciaCompartida = "ORD-ERP-" + Guid.NewGuid();

        var solicitud = new SolicitudAltaEnvioApiDto
        {
            ClaveApi = claveVigente,
            ModalidadServicio = "Express",
            NombreDestinatario = "Nicolás Pereira",
            Calle = "Bvar. España",
            Numero = "2140",
            Ciudad = "Montevideo",
            ReferenciaExterna = referenciaCompartida,
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 1.5m, Alto = 10m, Ancho = 10m, Profundo = 10m }
            }
        };

        // Act
        var primera = await api.CrearEnvioPorApiIntegracionAsync(solicitud);
        var reintento = await api.CrearEnvioPorApiIntegracionAsync(solicitud);

        // Assert
        Assert.False(primera.Duplicado);
        Assert.True(reintento.Duplicado);
        Assert.Equal(primera.Id, reintento.Id);
        Assert.Equal(primera.CodigoSeguimiento, reintento.CodigoSeguimiento);
        Assert.Equal(primera.Tarifa, reintento.Tarifa);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("um_clave_inexistente_12345")]
    public async Task CrearEnvioPorApiIntegracion_ConCredencialInvalida_LanzaApiExcepcionUnauthorized(string claveInvalida)
    {
        // Arrange
        var api = new MockEnviosApi();
        var solicitud = new SolicitudAltaEnvioApiDto
        {
            ClaveApi = claveInvalida,
            NombreDestinatario = "Cliente Prueba",
            Calle = "Colonia",
            Numero = "1000",
            Ciudad = "Montevideo"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ApiExcepcion>(() =>
            api.CrearEnvioPorApiIntegracionAsync(solicitud));

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, ex.CodigoEstado);
    }

    [Fact]
    public async Task RegenerarClaveApi_InvalidaClaveAnteriorYGeneraNuevaCredencialVigente()
    {
        // Arrange
        var api = new MockEnviosApi();
        var claveAnterior = await api.ObtenerClaveApiActivaAsync();

        // Act
        var nuevaCredencial = await api.RegenerarClaveApiAsync();

        // Assert
        Assert.NotNull(nuevaCredencial);
        Assert.StartsWith("um_", nuevaCredencial.ClaveApi);
        Assert.NotEqual(claveAnterior, nuevaCredencial.ClaveApi);

        // Invocar con la clave anterior debe fallar con 401
        var solicitudClaveVieja = new SolicitudAltaEnvioApiDto
        {
            ClaveApi = claveAnterior,
            NombreDestinatario = "Test Rotación",
            Calle = "18 de Julio",
            Numero = "1200",
            Ciudad = "Montevideo"
        };

        var ex = await Assert.ThrowsAsync<ApiExcepcion>(() =>
            api.CrearEnvioPorApiIntegracionAsync(solicitudClaveVieja));
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, ex.CodigoEstado);
    }

    [Fact]
    public async Task CrearEnvioPorApiIntegracion_ConDatosInvalidosONulos_LanzaExcepcionTipada()
    {
        // Arrange
        var api = new MockEnviosApi();
        var clave = await api.ObtenerClaveApiActivaAsync();

        // 1. Solicitud nula
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            api.CrearEnvioPorApiIntegracionAsync(null!));

        // 2. Peso fuera de rango (<= 0)
        var solicitudPesoCero = new SolicitudAltaEnvioApiDto
        {
            ClaveApi = clave,
            NombreDestinatario = "Cliente",
            Calle = "Rivera",
            Numero = "2000",
            Ciudad = "Montevideo",
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 0m, Alto = 10m, Ancho = 10m, Profundo = 10m }
            }
        };

        var exPeso = await Assert.ThrowsAsync<ApiExcepcion>(() =>
            api.CrearEnvioPorApiIntegracionAsync(solicitudPesoCero));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exPeso.CodigoEstado);
    }
}
