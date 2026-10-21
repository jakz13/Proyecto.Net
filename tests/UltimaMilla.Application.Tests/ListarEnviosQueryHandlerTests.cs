using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Tests;

public class ListarEnviosQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();

    private Envio Crear(Guid operadorId, Guid comercioId, string codigo)
    {
        var envio = new Envio(Guid.NewGuid(), comercioId, "Calle 1", "Ana", codigo, operadorId);
        _envios.Envios.Add(envio);
        return envio;
    }

    [Fact]
    public async Task NoDevuelveEnviosDeOtrosOperadores()
    {
        var propio = Crear(_tenant.OperadorId, _tenant.ComercioOperadorId!.Value, "TRK-PROPIO01");
        Crear(Guid.NewGuid(), Guid.NewGuid(), "TRK-AJENO001");

        var resultado = await new ListarEnviosQueryHandler(_envios, _tenant).Handle(new ListarEnviosQuery(), default);

        Assert.Equal(propio.Id, Assert.Single(resultado).Id);
    }

    [Fact]
    public async Task UsuarioDeComercio_SoloVeLosEnviosDeSuComercio()
    {
        var propio = Crear(_tenant.OperadorId, _tenant.ComercioOperadorId!.Value, "TRK-PROPIO01");
        Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-OTROCOM01");

        var resultado = await new ListarEnviosQueryHandler(_envios, _tenant).Handle(new ListarEnviosQuery(), default);

        Assert.Equal(propio.Id, Assert.Single(resultado).Id);
    }

    [Fact]
    public async Task UsuarioDeOperador_VeLosEnviosDeTodosLosComerciosDeSuOperador()
    {
        _tenant.ComercioOperadorId = null;
        Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-COMERCIO1");
        Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-COMERCIO2");

        var resultado = await new ListarEnviosQueryHandler(_envios, _tenant).Handle(new ListarEnviosQuery(), default);

        Assert.Equal(2, resultado.Count);
    }
}
