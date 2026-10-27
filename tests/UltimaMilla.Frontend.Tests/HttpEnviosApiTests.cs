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

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConArchivoValido_RetornaResultadoExitoso()
    {
        // Arrange
        var loteId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        {
            "loteId": "{{loteId}}",
            "claveIdempotencia": "sha256:abc123def456",
            "reimportacion": false,
            "totalRegistros": 2,
            "creados": 2,
            "duplicados": 0,
            "conError": 0,
            "filas": [
                {
                    "fila": 2,
                    "estado": "Creado",
                    "codigoSeguimiento": "ENV-2026-0001",
                    "tarifa": 170.0,
                    "error": null
                },
                {
                    "fila": 3,
                    "estado": "Creado",
                    "codigoSeguimiento": "ENV-2026-0002",
                    "tarifa": 210.5,
                    "error": null
                }
            ]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var bytesArchivo = System.Text.Encoding.UTF8.GetBytes("referencia,modalidad,destinatario,calle,numero,ciudad,peso,alto,ancho,profundo\nREF-1,Estandar,Juan,18 de Julio,1000,Montevideo,2.0,10,10,10\nREF-2,Estandar,Ana,Colonia,2000,Montevideo,3.5,15,15,15");

        // Act
        var resultado = await api.ImportarEnviosPorArchivoAsync(bytesArchivo, "envios.csv");

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(loteId, resultado.LoteId);
        Assert.False(resultado.Reimportacion);
        Assert.Equal(2, resultado.TotalRegistros);
        Assert.Equal(2, resultado.Creados);
        Assert.Equal(0, resultado.Duplicados);
        Assert.Equal(2, resultado.Filas.Count);
        Assert.Equal("ENV-2026-0001", resultado.Filas[0].CodigoSeguimiento);
        Assert.Equal(170.0m, resultado.Filas[0].Tarifa);
    }

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConReimportacion_ReconoceDuplicados()
    {
        // Arrange
        var loteId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        {
            "loteId": "{{loteId}}",
            "claveIdempotencia": "sha256:abc123def456",
            "reimportacion": true,
            "totalRegistros": 2,
            "creados": 0,
            "duplicados": 2,
            "conError": 0,
            "filas": [
                {
                    "fila": 2,
                    "estado": "Duplicado",
                    "codigoSeguimiento": "ENV-2026-0001",
                    "tarifa": 170.0,
                    "error": null
                },
                {
                    "fila": 3,
                    "estado": "Duplicado",
                    "codigoSeguimiento": "ENV-2026-0002",
                    "tarifa": 210.5,
                    "error": null
                }
            ]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var bytesArchivo = System.Text.Encoding.UTF8.GetBytes("contenido");

        // Act
        var resultado = await api.ImportarEnviosPorArchivoAsync(bytesArchivo, "envios.csv", "clave-lote-1");

        // Assert
        Assert.NotNull(resultado);
        Assert.True(resultado.Reimportacion);
        Assert.Equal(0, resultado.Creados);
        Assert.Equal(2, resultado.Duplicados);
        Assert.All(resultado.Filas, f => Assert.Equal(ResultadoFilaImportacionDto.Duplicado, f.Estado));
    }

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConArchivoVacio_LanzaArgumentException()
    {
        // Arrange
        var clienteHttp = new HttpClient(ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.OK)) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            api.ImportarEnviosPorArchivoAsync(Array.Empty<byte>(), "vacio.csv"));
    }

    [Fact]
    public async Task ImportarEnviosPorArchivo_ConConflictoConcurrencia_LanzaApiExcepcion409()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.Conflict, "{\"error\":\"Conflicto de concurrencia\"}");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var bytes = System.Text.Encoding.UTF8.GetBytes("test");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UltimaMilla.Frontend.Infrastructure.Excepciones.ApiExcepcion>(() =>
            api.ImportarEnviosPorArchivoAsync(bytes, "envios.csv"));

        Assert.Equal(HttpStatusCode.Conflict, ex.CodigoEstado);
    }

    [Fact]
    public async Task CrearEnvio_ConRespuestaExitosa_MapeaCamposTarifaYCodigo()
    {
        // Arrange
        var nuevoId = Guid.NewGuid();
        var jsonRespuesta = $$"""
        {
            "id": "{{nuevoId}}",
            "codigoSeguimiento": "TRK-2026-0099",
            "estado": "Admitido",
            "tarifa": 235.50,
            "bultos": ["TRK-2026-0099-B01", "TRK-2026-0099-B02"]
        }
        """;

        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.Created, jsonRespuesta);
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var dto = new CrearEnvioDto
        {
            NombreDestinatario = "Gonzalo Méndez",
            DireccionDestino = "Sarandí 450",
            ModalidadServicio = "Estandar",
            Bultos = new List<CrearBultoItemDto>
            {
                new CrearBultoItemDto { Peso = 1.2m, Alto = 10m, Ancho = 10m, Profundo = 10m },
                new CrearBultoItemDto { Peso = 1.8m, Alto = 15m, Ancho = 15m, Profundo = 15m }
            }
        };

        // Act
        var resultado = await api.CrearEnvioAsync(dto);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(nuevoId, resultado.Id);
        Assert.Equal("TRK-2026-0099", resultado.CodigoSeguimiento);
        Assert.Equal(EstadoEnvioDto.Admitido, resultado.Estado);
        Assert.Equal(235.50m, resultado.Tarifa);
        Assert.Equal(2, resultado.BultosIdentificaciones?.Count);
    }

    [Fact]
    public async Task CrearEnvio_ConErrorEnServidor_LanzaApiExcepcion()
    {
        // Arrange
        var manejador = ManejadorMensajesHttpMock.ConRespuesta(HttpStatusCode.BadRequest, "{\"error\":\"El operador no tiene cuadro tarifario\"}");
        var clienteHttp = new HttpClient(manejador) { BaseAddress = new Uri(UrlBase) };
        var api = new HttpEnviosApi(clienteHttp);

        var dto = new CrearEnvioDto
        {
            NombreDestinatario = "Test Error",
            DireccionDestino = "Dirección inválida"
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UltimaMilla.Frontend.Infrastructure.Excepciones.ApiExcepcion>(() =>
            api.CrearEnvioAsync(dto));

        Assert.Equal(HttpStatusCode.BadRequest, ex.CodigoEstado);
    }
}
