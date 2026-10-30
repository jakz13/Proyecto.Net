using UltimaMilla.Application.Queries.Deposito.ListarEnviosEnDeposito;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Tests;

public class ListarEnviosEnDepositoQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();
    private int _secuencia;

    private ListarEnviosEnDepositoQueryHandler CrearHandler() => new(_envios, _tenant);

    private Envio CrearEnvioEnDeposito(
        string destinatario = "Juan Pérez",
        Guid? operadorId = null,
        int cantidadBultos = 2,
        bool clasificarTodos = false)
    {
        _secuencia++;
        var operador = operadorId ?? _tenant.OperadorId;
        var envio = new Envio(
            Guid.NewGuid(),
            _tenant.ComercioOperadorId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "Estandar",
            $"TRK-DEP{_secuencia:0000}",
            Guid.NewGuid(),
            null,
            null,
            $"Av. Italia {_secuencia}",
            destinatario,
            operador);

        var codigos = new List<string>();
        for (int i = 1; i <= cantidadBultos; i++)
        {
            var codigo = $"TRK-DEP{_secuencia:0000}-B{i:00}";
            envio.AgregarBulto(codigo, 2.0m, 15m, 15m, 15m);
            codigos.Add(codigo);
        }

        // Recepcionar en depósito para cumplir precondición de UC-15
        envio.RecepcionarEnDeposito(codigos, null, null, Guid.NewGuid());

        if (clasificarTodos)
        {
            var clasificaciones = codigos.Select(c => new ClasificacionBulto(c, PrioridadBulto.Normal, null, "Estándar")).ToList();
            envio.ClasificarBultos(clasificaciones, Guid.NewGuid());
        }

        _envios.Envios.Add(envio);
        return envio;
    }

    [Fact]
    public async Task Handle_DevuelveSoloEnviosEnDepositoDelOperadorActual()
    {
        // Arrange
        var envio1 = CrearEnvioEnDeposito("Cliente 1", cantidadBultos: 2);
        var envio2 = CrearEnvioEnDeposito("Cliente 2", cantidadBultos: 1, clasificarTodos: true);

        // Envío de otro operador (aislamiento multitenancy)
        CrearEnvioEnDeposito("Cliente Otro Operador", operadorId: Guid.NewGuid());

        // Envío en estado Admitido (no recepcionado aún en depósito)
        var admitido = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-ADMITIDO", operadorId: _tenant.OperadorId);
        admitido.AgregarBulto("BLT-ADMITIDO-1", 1m);
        _envios.Envios.Add(admitido);

        var handler = CrearHandler();

        // Act
        var resultado = await handler.Handle(new ListarEnviosEnDepositoQuery(), CancellationToken.None);

        // Assert
        Assert.NotNull(resultado);
        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, e => e.EnvioId == envio1.Id);
        Assert.Contains(resultado, e => e.EnvioId == envio2.Id);

        var dto1 = resultado.First(e => e.EnvioId == envio1.Id);
        Assert.False(dto1.EstaClasificado);
        Assert.Equal(0, dto1.CantidadClasificados);
        Assert.Equal(2, dto1.CantidadBultos);

        var dto2 = resultado.First(e => e.EnvioId == envio2.Id);
        Assert.True(dto2.EstaClasificado);
        Assert.Equal(1, dto2.CantidadClasificados);
        Assert.Equal(1, dto2.CantidadBultos);
    }

    [Fact]
    public async Task Handle_CalculaCorrectamenteCantidadClasificadosParciales()
    {
        // Arrange
        var envio = CrearEnvioEnDeposito("Cliente Parcial", cantidadBultos: 2);
        // Clasificar solo el primer bulto
        var clasificacionParcial = new List<ClasificacionBulto>
        {
            new($"TRK-DEP{_secuencia:0000}-B01", PrioridadBulto.Alta, Guid.NewGuid(), "Frágil")
        };
        envio.ClasificarBultos(clasificacionParcial, Guid.NewGuid());

        var handler = CrearHandler();

        // Act
        var resultado = await handler.Handle(new ListarEnviosEnDepositoQuery(), CancellationToken.None);

        // Assert
        var item = Assert.Single(resultado);
        Assert.Equal(2, item.CantidadBultos);
        Assert.Equal(1, item.CantidadClasificados);
        Assert.False(item.EstaClasificado);

        var bulto1 = item.Bultos.First(b => b.Identificacion.EndsWith("B01"));
        Assert.True(bulto1.EstaClasificado);
        Assert.Equal("Alta", bulto1.Prioridad);
        Assert.Equal("Frágil", bulto1.TipoClasificacion);

        var bulto2 = item.Bultos.First(b => b.Identificacion.EndsWith("B02"));
        Assert.False(bulto2.EstaClasificado);
        Assert.Null(bulto2.Prioridad);
    }

    [Fact]
    public async Task Handle_SinEnviosEnDeposito_DevuelveColeccionVacia()
    {
        var handler = CrearHandler();

        var resultado = await handler.Handle(new ListarEnviosEnDepositoQuery(), CancellationToken.None);

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
        Assert.Throws<ArgumentNullException>(() => new ListarEnviosEnDepositoQueryHandler(null!, _tenant));
        Assert.Throws<ArgumentNullException>(() => new ListarEnviosEnDepositoQueryHandler(_envios, null!));
    }
}
