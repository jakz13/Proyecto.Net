using System.Net;
using UltimaMilla.Frontend.Contracts.Enums;
using UltimaMilla.Frontend.Contracts.Modelos.Envios;
using UltimaMilla.Frontend.Infrastructure.Http;
using UltimaMilla.Frontend.Tests.Helpers;
using Xunit;

namespace UltimaMilla.Frontend.Tests;

/// <summary>
/// Pruebas unitarias para HttpEnviosApi.
/// </summary>
public class HttpEnviosApiTests
{
    private const string UrlBase = "http://localhost:5000";

    [Fact]
    public async Task ListarEnvios_ConRespuestaExitosa_AplicaFiltrosYPaginacion()
    {
        // Arrange
        var envio1Id = Guid.NewGuid();
        var envio2Id = Guid.NewGuid();

        var jsonRespuesta = $$"""
        [
            {
                "id": "{{envio1Id}}",
                "comercioId": "11111111-1111-1111-1111-111111111111",
                "direccionDestino": "Av. Brasil 2345",
                "nombreDestinatario": "Carlos Gómez",
                "fechaCreacion": "2026-10-22T09:00:00Z",
                "estado": "Entregado"
            },
            {
                "id": "{{envio2Id}}",
                "comercioId": "11111111-1111-1111-1111-111111111111",
                "direccionDestino": "Rambla Gandhi 456",
                "nombreDestinatario": "Ana Rodríguez",
                "fechaCreacion": "2026-10-22T10:00:00Z",
                "estado": "EnTransito"
            }
        ]
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var filtro = new FiltroEnviosDto
        {
            Pagina = 1,
            TamanoPagina = 10,
            TerminoBusqueda = "Carlos"
        };

        // Act
        var resultado = await api.ListarEnviosAsync(filtro);

        // Assert
        Assert.NotNull(resultado);
        Assert.Single(resultado.Elementos);
        Assert.Equal("Carlos Gómez", resultado.Elementos[0].NombreDestinatario);
        Assert.Equal(EstadoEnvioDto.Entregado, resultado.Elementos[0].Estado);
    }

    [Fact]
    public async Task ObtenerPorId_ConEnvioExistente_RetornaEnvioMapeado()
    {
        // Arrange
        var envioId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        {
            "envioId": "{{envioId}}",
            "codigoSeguimiento": "UY-9999",
            "estadoActual": 5,
            "modalidadServicio": "Estandar",
            "tarifa": 150.0,
            "nombreDestinatario": "Lucía Fernández",
            "direccionDestino": "Bulevar Artigas 1200",
            "hojaDeRutaId": "aaaaaaaa-1111-2222-3333-444444444444",
            "fechaAlta": "2026-10-22T08:00:00Z",
            "bultos": [
                {
                    "bultoId": "11111111-2222-3333-4444-555555555555",
                    "identificacion": "BLT-1",
                    "pesoKg": 4.5,
                    "estadoBulto": "Entregado"
                }
            ],
            "eventos": []
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        // Act
        var resultado = await api.ObtenerPorIdAsync(envioId);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(envioId, resultado.Id);
        Assert.Equal("UY-9999", resultado.CodigoSeguimiento);
        Assert.Equal(EstadoEnvioDto.Entregado, resultado.Estado);
        Assert.Equal("Lucía Fernández", resultado.NombreDestinatario);
        Assert.Equal(4.5m, resultado.PesoKg);
    }

    [Fact]
    public async Task ObtenerPorId_CuandoNoExiste_RetornaNull()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.NotFound);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        // Act
        var resultado = await api.ObtenerPorIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(resultado);
    }

    [Fact]
    public async Task DeclararEnvioExtraviado_RetornaResultadoExitoso()
    {
        // Arrange
        var clienteHttp = new HttpClient(ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK)) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var dto = new DeclararExtraviadoDto
        {
            EnvioId = Guid.NewGuid(),
            CodigoSeguimiento = "UY-EXTRAVIADO",
            MotivoExtravio = "No apareció en rendición de la ruta",
            DespachadorUsuario = "despachador1"
        };

        // Act
        var resultado = await api.DeclararEnvioExtraviadoAsync(dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Exito);
        Assert.Equal(EstadoEnvioDto.Extraviado, resultado.NuevoEstado);
    }
}
