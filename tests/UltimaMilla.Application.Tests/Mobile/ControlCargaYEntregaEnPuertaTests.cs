using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.IniciarRecorrido;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Events;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;
using Xunit;

namespace UltimaMilla.Application.Tests.Mobile;

public class ControlCargaYEntregaEnPuertaTests
{
    private static readonly Guid OperadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtroOperadorId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid ComercioId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UsuarioRepartidorId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid HojaDeRutaId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid Parada1Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000001");
    private static readonly Guid Envio1Id = Guid.Parse("cccccccc-1111-2222-3333-000000000001");
    private static readonly Guid Direccion1Id = Guid.Parse("eeeeeeee-0001-0000-0000-000000000001");

    private static (
        HojaDeRuta Ruta,
        Envio Envio,
        FakeRutaRepository RutaRepo,
        FakeEnvioRepository EnvioRepo,
        FakeOutboxStorage Outbox,
        FakeUnitOfWork Uow,
        FakeCacheService Cache,
        FakeClock Clock,
        FakeCurrentTenant Tenant) CrearEscenario(string estadoRuta = "Despachada", Guid? tenantOperadorId = null)
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), estadoRuta);
        ruta.AgregarParada(Parada1Id, Envio1Id, 1, Direccion1Id, null, "Pendiente");

        var envio = new Envio(
            Envio1Id,
            ComercioId,
            "Av. 18 de Julio 1450, Centro, Montevideo",
            "Martín Rodríguez",
            "ENV-2026-00108",
            OperadorId);
        envio.AgregarBulto("BLT-001-A", 1.2m);
        envio.RecepcionarEnDeposito(new[] { "BLT-001-A" }, null, null, UsuarioRepartidorId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BLT-001-A", PrioridadBulto.Normal) }, UsuarioRepartidorId);
        envio.AsignarRuta(HojaDeRutaId);
        envio.DespacharEnRuta(HojaDeRutaId, UsuarioRepartidorId, "Despachado en ruta");

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var envioRepo = new FakeEnvioRepository();
        envioRepo.Envios.Add(envio);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var cache = new FakeCacheService();
        var clock = new FakeClock();
        var tenant = new FakeCurrentTenant
        {
            OperadorId = tenantOperadorId ?? OperadorId,
            UsuarioId = UsuarioRepartidorId
        };

        return (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant);
    }

    // =========================================================================
    // UC-18 / UC-19: CONTROL DE CARGA E INICIO DE RECORRIDO
    // =========================================================================

    [Fact]
    public async Task IniciarRecorrido_RutaDespachada_PasaAEnTransitoYEmiteOutboxMessage()
    {
        // Arrange
        var (ruta, _, rutaRepo, _, outbox, uow, _, clock, tenant) = CrearEscenario(estadoRuta: "Despachada");
        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var command = new IniciarRecorridoCommand(HojaDeRutaId, "IDEMP-INICIO-001");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EnTransito", result.Estado);
        Assert.False(result.YaIniciada);
        Assert.Equal("EnTransito", ruta.Estado);
        Assert.True(uow.Confirmaciones > 0);

        // Outbox integration event
        Assert.Single(outbox.Mensajes);
        var mensaje = outbox.Mensajes.First();
        Assert.Equal(EventosIntegracion.Tipos.RutaIniciada, mensaje.TipoEvento);
        Assert.Equal(nameof(HojaDeRuta), mensaje.AgregadoOrigen);
        Assert.Equal(HojaDeRutaId, mensaje.AgregadoId);
    }

    [Fact]
    public async Task IniciarRecorrido_RutaYaEnTransito_EsIdempotente()
    {
        // Arrange
        var (ruta, _, rutaRepo, _, outbox, uow, _, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var command = new IniciarRecorridoCommand(HojaDeRutaId, "IDEMP-INICIO-002");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EnTransito", result.Estado);
        Assert.True(result.YaIniciada);
        Assert.Empty(outbox.Mensajes);
    }

    [Fact]
    public async Task IniciarRecorrido_RutaEnBorrador_LanzaDomainValidationException()
    {
        // Arrange
        var (ruta, _, rutaRepo, _, outbox, uow, _, clock, tenant) = CrearEscenario(estadoRuta: "Borrador");
        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var command = new IniciarRecorridoCommand(HojaDeRutaId, "IDEMP-INICIO-003");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("Despachada", ex.Message);
    }

    [Fact]
    public async Task IniciarRecorrido_RutaInexistente_LanzaNotFoundException()
    {
        // Arrange
        var (_, _, rutaRepo, _, outbox, uow, _, clock, tenant) = CrearEscenario();
        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var command = new IniciarRecorridoCommand(Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task IniciarRecorrido_OperadorAjeno_LanzaUnauthorizedAccessException()
    {
        // Arrange (Anti-IDOR)
        var (_, _, rutaRepo, _, outbox, uow, _, clock, tenant) = CrearEscenario(tenantOperadorId: OtroOperadorId);
        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var command = new IniciarRecorridoCommand(HojaDeRutaId);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    // =========================================================================
    // UC-20: RESTRICCIÓN DE ENTREGA EN PUERTA SEGÚN ESTADO DE HOJA DE RUTA
    // =========================================================================

    [Fact]
    public async Task ConfirmarEntrega_RutaEnLiberada_LanzaDomainValidationException()
    {
        // Arrange
        var (_, _, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "Liberada");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-001",
            "Martín Rodríguez",
            "4.123.456-7",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.Contains("no se encuentra en tránsito", ex.Message);
        Assert.Equal(0, uow.Confirmaciones);
    }

    [Fact]
    public async Task ConfirmarEntrega_RutaEnTransito_RegistraEntregaExitosamente()
    {
        // Arrange
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-002",
            "Martín Rodríguez",
            "4.123.456-7",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Entregado", result.EstadoEnvio);
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal("Entregada", ruta.Paradas.First(p => p.Id == Parada1Id).Estado);
        Assert.True(uow.Confirmaciones > 0);
        Assert.Single(outbox.Mensajes.Where(m => m.TipoEvento == "EnvioEntregadoIntegrationEvent"));
    }

    [Fact]
    public async Task ConfirmarEntrega_ParadaYaEntregada_RespondeDeFormaIdempotente()
    {
        // Arrange
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-003",
            "Martín Rodríguez",
            "4.123.456-7",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Primera entrega
        await handler.Handle(command, CancellationToken.None);

        // Act - Reejecución con misma o distinta clave
        var resultIdempotente = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(resultIdempotente);
        Assert.True(resultIdempotente.Idempotente);
        Assert.Contains("idempotente", resultIdempotente.Mensaje);
    }

    private sealed class FakeCacheService : IDistributedCacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
