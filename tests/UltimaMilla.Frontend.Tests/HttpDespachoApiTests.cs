using System.Net;
using UltimaMilla.Frontend.Contracts.Modelos.Despacho;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Frontend.Tests.Helpers;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias para HttpDespachoApi (UC-09).
/// </summary>
public class HttpDespachoApiTests
{
    private const string UrlBase = "http://localhost:5000";

    [Fact]
    public async Task ListarRutasPendientesDespacho_ConRespuestaExitosa_MapeaCamposCorrectamente()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        [
            {
                "hojaDeRutaId": "{{rutaId}}",
                "numeroRuta": "HDR-2026-0001",
                "fecha": "2026-10-22",
                "estado": "EnCarga",
                "repartidorId": "66666666-6666-6666-6666-666666666666",
                "repartidorDocumento": "CI 4.888.777-1",
                "vehiculoId": "55555555-5555-5555-5555-555555555555",
                "vehiculoPatente": "STX-1234",
                "vehiculoTipo": "Furgoneta Utilitaria",
                "capacidadPesoKg": 1000.0,
                "capacidadVolumenM3": 10.0,
                "pesoTotalKg": 350.5,
                "volumenTotalM3": 3.8,
                "cantidadParadas": 5,
                "selloModificacion": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
            }
        ]
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpDespachoApi(clienteHttp);

        // Act
        var resultado = await api.ListarRutasPendientesDespachoAsync();

        // Assert
        Assert.NotNull(resultado);
        Assert.Single(resultado);
        var ruta = resultado[0];
        Assert.Equal(rutaId, ruta.Id);
        Assert.Equal("HDR-2026-0001", ruta.NumeroRuta);
        Assert.Equal("EnCarga", ruta.Estado);
        Assert.Equal("STX-1234", ruta.VehiculoMatricula);
        Assert.Equal(5, ruta.CantidadParadas);
        Assert.True(ruta.PorcentajeOcupacionPeso > 30);
    }

    [Fact]
    public async Task ObtenerControlDespacho_ConHojaDeRutaValida_CalculaCargadosYDiscrepantes()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var bulto1Id = Guid.NewGuid();
        var bulto2Id = Guid.NewGuid();

        var jsonRespuesta = $$"""
        {
            "hojaDeRutaId": "{{rutaId}}",
            "numeroRuta": "HDR-2026-0002",
            "fecha": "2026-10-22",
            "estadoRuta": "EnCarga",
            "vehiculoPatente": "STX-5678",
            "capacidadPesoKg": 800.0,
            "capacidadVolumenM3": 8.0,
            "tieneRutaPendienteDeRendir": false,
            "puedeDespachar": true,
            "bultos": [
                {
                    "bultoId": "{{bulto1Id}}",
                    "envioId": "11111111-0000-0000-0000-000000000001",
                    "codigoSeguimiento": "UY-001",
                    "codigoBulto": "BLT-001",
                    "destinatario": "Juan Pérez",
                    "direccion": "18 de Julio 1234",
                    "pesoKg": 10.0,
                    "volumenM3": 0.1,
                    "cargadoEfectivo": true
                },
                {
                    "bultoId": "{{bulto2Id}}",
                    "envioId": "11111111-0000-0000-0000-000000000002",
                    "codigoSeguimiento": "UY-002",
                    "codigoBulto": "BLT-002",
                    "destinatario": "María Gómez",
                    "direccion": "Colonia 567",
                    "pesoKg": 15.0,
                    "volumenM3": 0.2,
                    "cargadoEfectivo": false,
                    "motivoNoCarga": "Bulto dañado en depósito"
                }
            ]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpDespachoApi(clienteHttp);

        // Act
        var resultado = await api.ObtenerControlDespachoAsync(rutaId);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.TotalBultos);
        Assert.Equal(1, resultado.BultosCargados);
        Assert.Equal(1, resultado.BultosDiscrepantes);
        Assert.False(resultado.TieneRutaPendienteDeRendir);
    }

    [Fact]
    public async Task ObtenerControlDespacho_CuandoRutaNoExiste_RetornaNull()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.NotFound);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpDespachoApi(clienteHttp);

        // Act
        var resultado = await api.ObtenerControlDespachoAsync(Guid.NewGuid());

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConfirmarDespacho_ConRespuestaExitosa_RetornaResultadoDespachoCompleto()
    {
        // Arrange
        var rutaId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        {
            "hojaDeRutaId": "{{rutaId}}",
            "numeroRuta": "HDR-2026-0001",
            "estado": "Despachada",
            "enviosDespachados": 4,
            "enviosDevueltosADeposito": 1,
            "fechaDespacho": "2026-10-22T10:00:00Z",
            "selloModificacion": "99999999-8888-7777-6666-555555555555"
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpDespachoApi(clienteHttp);

        var dto = new ConfirmarDespachoDto
        {
            HojaDeRutaId = rutaId,
            ObservacionesSalida = "Salida autorizada por operario"
        };

        // Act
        var resultado = await api.ConfirmarDespachoAsync(dto, "operario1");

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exitoso);
        Assert.Equal(4, resultado.EnviosEnTransitoCount);
        Assert.Equal(1, resultado.EnviosReintegradosDepositoCount);
        Assert.Equal(rutaId, resultado.HojaDeRutaId);
    }

    [Fact]
    public async Task ConfirmarDespacho_CuandoHayConflictoDeConcurrencia_LanzaApiExcepcionConflicto()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.Conflict, "La ruta ya fue despachada");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpDespachoApi(clienteHttp);

        var dto = new ConfirmarDespachoDto { HojaDeRutaId = Guid.NewGuid() };

        // Act & Assert
        var excepcion = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ConfirmarDespachoAsync(dto, "operario1"));
        Assert.True(excepcion.EsConflictoConcurrencia);
        Assert.Equal(HttpStatusCode.Conflict, excepcion.CodigoEstado);
    }
}
