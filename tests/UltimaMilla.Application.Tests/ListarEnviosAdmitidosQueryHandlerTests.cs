using UltimaMilla.Application.Queries.Deposito.ListarEnviosAdmitidos;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

public class ListarEnviosAdmitidosQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();
    private int _secuencia;

    private ListarEnviosAdmitidosQueryHandler CrearHandler() => new(_envios, _tenant);

    private Envio CrearEnvio(
        string destinatario = "Juan Pérez",
        Guid? operadorId = null,
        bool recepcionarEnDeposito = false,
        int cantidadBultos = 1)
    {
        _secuencia++;
        var operador = operadorId ?? _tenant.OperadorId;
        var envio = new Envio(
            Guid.NewGuid(),
            _tenant.ComercioOperadorId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            $"TRK-TEST{_secuencia:0000}",
            Guid.NewGuid(),
            null,
            null,
            $"Calle Falsa {_secuencia}",
            destinatario,
            operador);

        for (int i = 1; i <= cantidadBultos; i++)
        {
            envio.AgregarBulto($"TRK-TEST{_secuencia:0000}-B{i:00}", 1.5m * i, 10m, 15m, 20m);
        }

        if (recepcionarEnDeposito)
        {
            var codigos = envio.Bultos.Select(b => b.Identificacion).ToList();
            envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());
        }

        _envios.Envios.Add(envio);
        return envio;
    }

    [Fact]
    public async Task Handle_DevuelveSoloEnviosAdmitidosDelOperadorActual()
    {
        // Arrange: 2 admitidos del operador, 1 en depósito del operador, 1 admitido de otro operador
        var admitido1 = CrearEnvio("Cliente 1", cantidadBultos: 2);
        var admitido2 = CrearEnvio("Cliente 2", cantidadBultos: 1);
        CrearEnvio("Cliente En Deposito", recepcionarEnDeposito: true);
        CrearEnvio("Cliente Otro Operador", operadorId: Guid.NewGuid());

        var handler = CrearHandler();

        // Act
        var resultado = await handler.Handle(new ListarEnviosAdmitidosQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, e => e.EnvioId == admitido1.Id);
        Assert.Contains(resultado, e => e.EnvioId == admitido2.Id);

        var dto1 = resultado.First(e => e.EnvioId == admitido1.Id);
        Assert.Equal(admitido1.CodigoSeguimientoPublico, dto1.CodigoSeguimiento);
        Assert.Equal(2, dto1.CantidadBultos);
        Assert.Equal(2, dto1.Bultos.Count);
        Assert.Equal("TRK-TEST0001-B01", dto1.Bultos[0].Identificacion);
    }

    [Fact]
    public async Task Handle_SinEnviosAdmitidos_DevuelveColeccionVacia()
    {
        // Arrange: sólo envíos en depósito
        CrearEnvio(recepcionarEnDeposito: true);

        var handler = CrearHandler();

        // Act
        var resultado = await handler.Handle(new ListarEnviosAdmitidosQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Empty(resultado);
    }

    [Fact]
    public async Task Handle_RequestNulo_LanzaArgumentNullException()
    {
        var handler = CrearHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.Handle(null!, CancellationToken.None));
    }

    [Fact]
    public void Constructor_DependenciasNulas_LanzaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ListarEnviosAdmitidosQueryHandler(null!, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ListarEnviosAdmitidosQueryHandler(_envios, null!));
    }

    [Fact]
    public async Task Handle_MapeaCorrectamenteDimensionesYPesosDeBultos()
    {
        // Arrange
        var envio = CrearEnvio("Carlos Ruiz", cantidadBultos: 1);
        var handler = CrearHandler();

        // Act
        var resultado = await handler.Handle(new ListarEnviosAdmitidosQuery(), CancellationToken.None);

        // Assert
        var item = Assert.Single(resultado);
        Assert.Equal("Carlos Ruiz", item.NombreDestinatario);
        Assert.Equal(envio.PesoTotal, item.PesoTotal);

        var bulto = Assert.Single(item.Bultos);
        Assert.Equal(1.5m, bulto.Peso);
        Assert.Equal(10m, bulto.Alto);
        Assert.Equal(15m, bulto.Ancho);
        Assert.Equal(20m, bulto.Profundo);
        Assert.Equal("Ingresado", bulto.EstadoBulto);
    }
}
