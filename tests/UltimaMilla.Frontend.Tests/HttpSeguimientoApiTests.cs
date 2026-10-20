using System.Net;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Frontend.Tests.Helpers;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias exhaustivas para HttpSeguimientoApi (UC-33).
/// Valida anti-enumeración, límite de tasa (429), resiliencia y serialización.
/// </summary>
public class HttpSeguimientoApiTests
{
    private const string UrlBase = "http://localhost:5000";

    [Fact]
    public async Task ConsultarSeguimiento_ConCodigoValidoYRespuestaExitosa_RetornaDtoCompleto()
    {
        // Arrange
        const string codigo = "UY-2026-0001";
        const string respuestaJson = """
        {
            "codigoSeguimiento": "UY-2026-0001",
            "estadoActual": 5,
            "ventanaHorariaEstimada": "09:00-13:00",
            "fechaUltimaActualizacion": "2026-10-22T14:30:00Z",
            "permiteReprogramacion": false,
            "historialEventos": [
                {
                    "fecha": "2026-10-22T08:00:00Z",
                    "estado": 4,
                    "descripcionPublica": "El envío salió a reparto"
                },
                {
                    "fecha": "2026-10-22T14:30:00Z",
                    "estado": 5,
                    "descripcionPublica": "Envío entregado"
                }
            ]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, respuestaJson);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.ConsultarSeguimientoAsync(codigo);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal("UY-2026-0001", resultado.CodigoSeguimiento);
        Assert.Equal(EstadoEnvioDto.Entregado, resultado.EstadoActual);
        Assert.Equal("09:00-13:00", resultado.VentanaHorariaEstimada);
        Assert.False(resultado.PermiteReprogramacion);
        Assert.Equal(2, resultado.HistorialEventos.Count);
        Assert.Equal(EstadoEnvioDto.EnTransito, resultado.HistorialEventos[0].Estado);
        Assert.Equal("Envío entregado", resultado.HistorialEventos[1].DescripcionPublica);
    }

    [Fact]
    public async Task ConsultarSeguimiento_ConEnumsComoCadenasDeTexto_DeserializaCorrectamente()
    {
        // Arrange
        const string codigo = "UY-2026-0002";
        const string respuestaJson = """
        {
            "codigoSeguimiento": "UY-2026-0002",
            "estadoActual": "EnTransito",
            "ventanaHorariaEstimada": "14:00-18:00",
            "fechaUltimaActualizacion": "2026-10-22T11:00:00Z",
            "permiteReprogramacion": false,
            "historialEventos": [
                {
                    "fecha": "2026-10-22T11:00:00Z",
                    "estado": "EnTransito",
                    "descripcionPublica": "En camino"
                }
            ]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, respuestaJson);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.ConsultarSeguimientoAsync(codigo);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(EstadoEnvioDto.EnTransito, resultado.EstadoActual);
        Assert.Single(resultado.HistorialEventos);
        Assert.Equal(EstadoEnvioDto.EnTransito, resultado.HistorialEventos[0].Estado);
    }

    [Fact]
    public async Task ConsultarSeguimiento_CuandoApiRetorna404_RetornaNullPorReglaAntiEnumeracion()
    {
        // Arrange (UC-33: no debe arrojar error ni distinguir código mal formado de código inexistente)
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.NotFound, "{\"error\":\"No se encontró el envío\"}");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.ConsultarSeguimientoAsync("CODIGO-INEXISTENTE");

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarSeguimiento_CuandoApiRetorna429_LanzaApiExcepcionConEsLimiteDeTasa()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta((HttpStatusCode)429, "Too Many Requests");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act & Assert
        var excepcion = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ConsultarSeguimientoAsync("UY-RATE-LIMIT"));
        Assert.True(excepcion.EsLimiteDeTasa);
        Assert.Equal((HttpStatusCode)429, excepcion.CodigoEstado);
        Assert.Contains("Demasiadas consultas", excepcion.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ConsultarSeguimiento_ConCodigoNuloOVacio_RetornaNullInmediatamente(string? codigoInvalido)
    {
        // Arrange: Se pasa un manejador que fallaría si se emitiera una llamada HTTP
        var manejador = ManejadorMensajesHttpMock.ConExcepcion(new InvalidOperationException("No debió consultar HTTP"));
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.ConsultarSeguimientoAsync(codigoInvalido!);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarSeguimiento_ConCodigoQueSuperaLongitudSegura_RetornaNullSinLlamarHttp()
    {
        // Arrange
        var codigoLargo = new string('A', 150);
        var manejador = ManejadorMensajesHttpMock.ConExcepcion(new InvalidOperationException("No debió consultar HTTP"));
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.ConsultarSeguimientoAsync(codigoLargo);

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarSeguimiento_CuandoApiRetorna500_LanzaApiExcepcionConDetalle()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.InternalServerError, "Error interno del servidor");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act & Assert
        var excepcion = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ConsultarSeguimientoAsync("UY-2026-0001"));
        Assert.Equal(HttpStatusCode.InternalServerError, excepcion.CodigoEstado);
    }

    [Fact]
    public async Task ConsultarSeguimiento_CuandoFallaLaRed_LanzaApiExcepcionServicioNoDisponible()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConExcepcion(new HttpRequestException("Conexión rechazada"));
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act & Assert
        var excepcion = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ConsultarSeguimientoAsync("UY-2026-0001"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, excepcion.CodigoEstado);
    }

    [Fact]
    public async Task SolicitarReprogramacion_ConParametrosValidos_RetornaResultadoSeguro()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, "{}");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpSeguimientoApi(clienteHttp);

        // Act
        var resultado = await api.SolicitarReprogramacionAsync("UY-2026-0001", DateTime.UtcNow.AddDays(2));

        // Assert
        Assert.True(resultado);
    }
}
