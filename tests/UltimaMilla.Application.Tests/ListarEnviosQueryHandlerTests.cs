using UltimaMilla.Application.Queries.ListarEnvios;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Application.Tests;

public class ListarEnviosQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();

    private Envio Crear(Guid operadorId, Guid comercioId, string codigo, Guid? rutaId = null)
    {
        var envio = new Envio(Guid.NewGuid(), comercioId, "Calle 1", "Ana", codigo, operadorId);
        if (rutaId.HasValue)
        {
            envio.AgregarBulto("B-01", 1.0m);
            envio.RecepcionarEnDeposito(new[] { "B-01" }, null, null, Guid.NewGuid());
            envio.AsignarRuta(rutaId.Value);
        }
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

    [Fact]
    public async Task EnvioAsignadoARutaConRepartidor_ResuelveNumeroDeRutaYNombreDelChofer()
    {
        _tenant.ComercioOperadorId = null;
        var rutaId = Guid.NewGuid();
        var repartidorId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var ruta = new UltimaMilla.Domain.Entities.Planificacion.HojaDeRuta(
            rutaId,
            _tenant.OperadorId,
            new DateOnly(2026, 10, 22),
            "Despachada"
        );
        ruta.AsignarRecursos(repartidorId, Guid.NewGuid());

        var repartidor = new UltimaMilla.Domain.Entities.Operador.Repartidor(
            repartidorId,
            usuarioId,
            _tenant.OperadorId,
            "4.321.987-6",
            "+598 99 123 456"
        );

        var usuario = new UltimaMilla.Domain.Entities.Identidad.Usuario(
            usuarioId,
            "Martín Chofer",
            "chofer@gmail.com",
            "hash123",
            Guid.NewGuid()
        );

        var envio = Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-RUTA01", rutaId);

        var rutasRepo = new FakeRutaRepository();
        rutasRepo.Rutas.Add(ruta);

        var repartidoresRepo = new FakeRepartidorRepository();
        repartidoresRepo.Repartidores.Add(repartidor);

        var usuariosRepo = new FakeUsuarioRepository();
        usuariosRepo.Usuarios.Add(usuario);

        var handler = new ListarEnviosQueryHandler(_envios, _tenant, rutasRepo, repartidoresRepo, usuariosRepo);
        var resultado = await handler.Handle(new ListarEnviosQuery(), default);

        var dto = Assert.Single(resultado);
        Assert.Equal($"HR-20261022-{rutaId.ToString()[..4].ToUpperInvariant()}", dto.HojaDeRutaNumero);
        Assert.Equal("Martín Chofer", dto.RepartidorNombre);
    }

    [Fact]
    public async Task EnvioAsignadoARutaSinRepartidor_ResuelveNumeroRutaYRepartidorNulo()
    {
        _tenant.ComercioOperadorId = null;
        var rutaId = Guid.NewGuid();

        var ruta = new UltimaMilla.Domain.Entities.Planificacion.HojaDeRuta(
            rutaId,
            _tenant.OperadorId,
            new DateOnly(2026, 10, 22),
            "Borrador"
        );

        var envio = Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-RUTA02", rutaId);

        var rutasRepo = new FakeRutaRepository();
        rutasRepo.Rutas.Add(ruta);

        var handler = new ListarEnviosQueryHandler(_envios, _tenant, rutasRepo);
        var resultado = await handler.Handle(new ListarEnviosQuery(), default);

        var dto = Assert.Single(resultado);
        Assert.Equal($"HR-20261022-{rutaId.ToString()[..4].ToUpperInvariant()}", dto.HojaDeRutaNumero);
        Assert.Null(dto.RepartidorNombre);
    }

    [Fact]
    public async Task EnvioSinRutaAsignada_RetornaValoresNulosParaRutaYRepartidor()
    {
        _tenant.ComercioOperadorId = null;
        Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-SINRUTA");

        var handler = new ListarEnviosQueryHandler(_envios, _tenant, new FakeRutaRepository());
        var resultado = await handler.Handle(new ListarEnviosQuery(), default);

        var dto = Assert.Single(resultado);
        Assert.Null(dto.HojaDeRutaNumero);
        Assert.Null(dto.RepartidorNombre);
    }

    [Fact]
    public async Task EnvioConRutaInexistenteEnRepositorio_ManejaSafeFallbackSinExcepcion()
    {
        _tenant.ComercioOperadorId = null;
        var envio = Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-RUTA-FANTASMA", Guid.NewGuid());

        var handler = new ListarEnviosQueryHandler(_envios, _tenant, new FakeRutaRepository());
        var resultado = await handler.Handle(new ListarEnviosQuery(), default);

        var dto = Assert.Single(resultado);
        Assert.Null(dto.HojaDeRutaNumero);
        Assert.Null(dto.RepartidorNombre);
    }

    [Fact]
    public async Task RepartidorSinUsuarioEncontrado_UsaDocumentoComoFallback()
    {
        _tenant.ComercioOperadorId = null;
        var rutaId = Guid.NewGuid();
        var repartidorId = Guid.NewGuid();

        var ruta = new UltimaMilla.Domain.Entities.Planificacion.HojaDeRuta(
            rutaId,
            _tenant.OperadorId,
            new DateOnly(2026, 10, 22),
            "Despachada"
        );
        ruta.AsignarRecursos(repartidorId, Guid.NewGuid());

        var repartidor = new UltimaMilla.Domain.Entities.Operador.Repartidor(
            repartidorId,
            Guid.NewGuid(),
            _tenant.OperadorId,
            "1.234.567-8",
            "+598 99 999 999"
        );

        var envio = Crear(_tenant.OperadorId, Guid.NewGuid(), "TRK-FALLBACK-DOC", rutaId);

        var rutasRepo = new FakeRutaRepository();
        rutasRepo.Rutas.Add(ruta);

        var repartidoresRepo = new FakeRepartidorRepository();
        repartidoresRepo.Repartidores.Add(repartidor);

        var handler = new ListarEnviosQueryHandler(_envios, _tenant, rutasRepo, repartidoresRepo, new FakeUsuarioRepository());
        var resultado = await handler.Handle(new ListarEnviosQuery(), default);

        var dto = Assert.Single(resultado);
        Assert.Equal("Doc. 1.234.567-8", dto.RepartidorNombre);
    }
}
