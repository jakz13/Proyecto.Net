using UltimaMilla.Application.Commands.Operador.GestionComercios;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Las sesiones abiertas de un comercio se enteran en vivo cuando un operador lo vincula (mismo RUT) o suspende/reactiva su vínculo.
/// El aviso es complementario: si falla, el cambio ya guardado no se deshace.
/// </summary>
public class AvisoOperadoresDelComercioTests
{
    private readonly FakeComercioRepository _comercios = new();
    private readonly FakeComercioOperadorRepository _vinculos = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly NotificadorDePrueba _notificador = new();

    private sealed class NotificadorDePrueba : ITableroRealtimeNotifier
    {
        public bool Falla { get; set; }
        public List<Guid> ComerciosAvisados { get; } = new();

        public Task NotificarOperadoresDelComercioAsync(Guid comercioId, CancellationToken ct = default)
        {
            if (Falla)
                throw new InvalidOperationException("Hub caído");

            ComerciosAvisados.Add(comercioId);
            return Task.CompletedTask;
        }

        public Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default) => Task.CompletedTask;
    }

    private CrearComercioCommandHandler Crear() => new(_comercios, _vinculos, _tenant, _uow, _notificador);

    private static CrearComercioCommand Comando(string rut) =>
        new("Empresa Global SRL", "Global", rut, "info@global.com", "29001122", "Calle 1");

    private ComercioEntidad ComercioExistente(string rut)
    {
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Empresa Global SRL", rut, "info@global.com", "29001122", "Global");
        _comercios.Comercios.Add(comercio);
        return comercio;
    }

    [Fact]
    public async Task VincularUnComercioQueYaExiste_AvisaASusSesiones()
    {
        var existente = ComercioExistente("123456789012");

        await Crear().Handle(Comando("123456789012"), default);

        Assert.Equal(new[] { existente.Id }, _notificador.ComerciosAvisados);
    }

    [Fact]
    public async Task UnComercioNuevo_NoTieneSesionesQueAvisar()
    {
        await Crear().Handle(Comando("999999999999"), default);

        Assert.Empty(_notificador.ComerciosAvisados);
    }

    [Fact]
    public async Task SiElAvisoFalla_ElAltaQuedaGuardada()
    {
        ComercioExistente("123456789012");
        _notificador.Falla = true;

        var resultado = await Crear().Handle(Comando("123456789012"), default);

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Single(_vinculos.Relaciones);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task SuspenderOReactivarElVinculo_AvisaAlComercio()
    {
        var comercioId = Guid.NewGuid();
        _vinculos.Relaciones.Add(new ComercioOperador(Guid.NewGuid(), comercioId, _tenant.OperadorId, "clave", "Activo"));
        var handler = new CambiarEstadoComercioCommandHandler(_vinculos, _tenant, _uow, _notificador);

        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, false), default);
        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, true), default);

        Assert.Equal(new[] { comercioId, comercioId }, _notificador.ComerciosAvisados);
    }

    [Fact]
    public async Task SiElAvisoFallaAlSuspender_ElCambioQuedaGuardado()
    {
        var comercioId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercioId, _tenant.OperadorId, "clave", "Activo");
        _vinculos.Relaciones.Add(vinculo);
        _notificador.Falla = true;

        await new CambiarEstadoComercioCommandHandler(_vinculos, _tenant, _uow, _notificador)
            .Handle(new CambiarEstadoComercioCommand(comercioId, false), default);

        Assert.False(vinculo.EstaActivo);
        Assert.Equal(1, _uow.Confirmaciones);
    }
}
