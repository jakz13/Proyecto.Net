using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.ConfirmarCarga;
using UltimaMilla.Application.Commands.Mobile.CargaVehiculo.IniciarRecorrido;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarEntregaExitosa;
using UltimaMilla.Application.Commands.Mobile.Entrega.RegistrarIntentoFallido;
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
    // CONTROL DE CARGA E INICIO DE RECORRIDO
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
    // RESTRICCIÓN DE ENTREGA EN PUERTA SEGÚN ESTADO DE HOJA DE RUTA
    // =========================================================================

    [Fact]
    public async Task ConfirmarEntrega_RutaEnBorrador_LanzaDomainValidationException()
    {
        // Arrange
        var (_, _, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "Borrador");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-001",
            "Martín Rodríguez",
            "41234567",
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
            "41234567",
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
            "41234567",
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

    [Fact]
    public async Task ConfirmarEntrega_ConflictoConServidor_PrevaleceEntregaFisicaConEvidencia()
    {
        // Arrange: el envio fue retirado en servidor a NoEntregado mientras el repartidor estuvo offline
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var motivoInterno = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "REASIG_SISTEMA", "Reasignado por despachador", esInterno: true);
        envio.RegistrarRetiroEnCalle(motivoInterno, Guid.NewGuid(), "Retiro concurrente");

        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-CONFLICTO-001",
            "Martín Rodríguez",
            "41234567",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: "foto_base_64",
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ConflictoResuelto);
        Assert.Equal("Entregado", result.EstadoEnvio);
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Single(envioRepo.Conflictos);
        Assert.Equal("PrevaleceEntregaFisicaConEvidencia", envioRepo.Conflictos[0].PoliticaAplicada);
    }

    [Fact]
    public async Task IntentoFallido_ConflictoConServidor_PrevaleceEstadoServidor()
    {
        // Arrange: el envio ya fue cancelado/reprogramado en servidor
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var motivoInterno = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "REASIG_SISTEMA", "Reasignado por despachador", esInterno: true);
        envio.RegistrarRetiroEnCalle(motivoInterno, Guid.NewGuid(), "Retiro concurrente");

        var motivoCliente = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "CLI_AUSENTE", "Cliente ausente", esInterno: false);
        envioRepo.Motivos.Add(motivoCliente);

        var handler = new RegistrarIntentoFallidoCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarIntentoFallidoCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-FALLO-CONFLICTO-001",
            MotivoNoEntregaId: motivoCliente.Id,
            MotivoCodigo: "CLI_AUSENTE",
            Observaciones: "No atiende timbre",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ConflictoResuelto);
        Assert.Equal(EstadoEnvio.NoEntregado, envio.EstadoActual);
        Assert.Single(envioRepo.Conflictos);
        Assert.Equal("PrevaleceEstadoServidor", envioRepo.Conflictos[0].PoliticaAplicada);
    }

    [Fact]
    public async Task ConfirmarEntrega_RutaDespachadaConDosParadas_AutoIniciaRecorridoYRegistraEntrega()
    {
        // Arrange: la ruta sigue en Despachada porque IniciarRecorrido offline no se procesó antes
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "Despachada");
        ruta.AgregarParada(Guid.NewGuid(), Guid.NewGuid(), 2, Direccion1Id, null, "Pendiente");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ENTREGA-DESPACHADA-001",
            "Martín Rodríguez",
            "41234567",
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
        Assert.Equal("EnTransito", ruta.Estado);
    }

    [Fact]
    public async Task IntentoFallido_RutaDespachadaConDosParadas_AutoIniciaRecorridoYRegistraFallo()
    {
        // Arrange: la ruta sigue en Despachada cuando llega un intento fallido offline
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "Despachada");
        ruta.AgregarParada(Guid.NewGuid(), Guid.NewGuid(), 2, Direccion1Id, null, "Pendiente");
        var motivoCliente = new MotivoNoEntrega(Guid.NewGuid(), OperadorId, "CLI_AUSENTE", "Cliente ausente", esInterno: false);
        envioRepo.Motivos.Add(motivoCliente);
        var handler = new RegistrarIntentoFallidoCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarIntentoFallidoCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-FALLO-DESPACHADA-001",
            MotivoNoEntregaId: motivoCliente.Id,
            MotivoCodigo: "CLI_AUSENTE",
            Observaciones: "No atiende timbre",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EnTransito", ruta.Estado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")] // 7 dígitos
    [InlineData("123456789")] // 9 dígitos
    [InlineData("ABCDEFGH")] // letras
    [InlineData("1234-567")] // 7 dígitos con guión
    public async Task ConfirmarEntrega_DocumentoInvalido_LanzaDomainValidationException(string? docInvalido)
    {
        // Arrange
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-DOC-INVALIDO-001",
            "Juan Pérez",
            docInvalido!,
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: null,
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(command, CancellationToken.None));
        Assert.True(ex.Message.Contains("requerido") || ex.Message.Contains("8 dígitos numéricos"));
    }

    [Fact]
    public async Task ConfirmarEntrega_DocumentoValido8Digitos_ProcesaExitosamente()
    {
        // Arrange
        var (ruta, envio, rutaRepo, envioRepo, outbox, uow, cache, clock, tenant) = CrearEscenario(estadoRuta: "EnTransito");
        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);

        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-DOC-VALIDO-001",
            "Juan Pérez",
            "41234567",
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
    }

    [Fact]
    public async Task IniciarRecorrido_RutaLiberadaConSoloRetiros_PasaDirectamenteAEnTransito()
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParadaRetiro(Guid.NewGuid(), 1, Direccion1Id);
        ruta.Liberar();

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock();
        var tenant = new FakeCurrentTenant { OperadorId = OperadorId, UsuarioId = UsuarioRepartidorId };

        var handler = new IniciarRecorridoCommandHandler(rutaRepo, uow, outbox, tenant, clock);
        var result = await handler.Handle(new IniciarRecorridoCommand(HojaDeRutaId, "IDEMP-SOLO-RETIROS"), CancellationToken.None);

        Assert.Equal("EnTransito", result.Estado);
        Assert.Equal("EnTransito", ruta.Estado);
    }

    [Fact]
    public async Task ConfirmarCarga_RutaLiberadaConSoloRetirosSinBultos_PasaAEnTransitoSinRequerirEscaneo()
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParadaRetiro(Guid.NewGuid(), 1, Direccion1Id);
        ruta.Liberar();

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var envioRepo = new FakeEnvioRepository();
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var tenant = new FakeCurrentTenant { OperadorId = OperadorId, UsuarioId = UsuarioRepartidorId };

        var handler = new ConfirmarCargaCommandHandler(rutaRepo, envioRepo, uow, tenant, outbox);
        var result = await handler.Handle(
            new ConfirmarCargaCommand(HojaDeRutaId, new List<string>(), "IDEMP-CARGA-SOLO-RETIROS", null, true),
            CancellationToken.None);

        Assert.Equal("EnTransito", result.EstadoRuta);
        Assert.Equal("EnTransito", ruta.Estado);
        Assert.Equal("Esta ruta contiene únicamente retiros en domicilio y no requiere carga en depósito.", result.Mensaje);
    }

    [Fact]
    public async Task ConfirmarCarga_RutaMixtaEnviosYRetiros_SoloEvaluaEnviosYConservaParadasDeRetiro()
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Parada1Id, Envio1Id, 1, Direccion1Id, null, "Pendiente");
        ruta.RegistrarAsignacion(Envio1Id, UsuarioRepartidorId);
        var retiroId = Guid.NewGuid();
        var paradaRetiro = ruta.AgregarParadaRetiro(retiroId, 2, Direccion1Id);
        ruta.Liberar();

        var envio = new Envio(
            Envio1Id,
            ComercioId,
            "Av. 18 de Julio 1450, Centro, Montevideo",
            "Martín Rodríguez",
            "ENV-2026-00108",
            OperadorId);
        envio.AgregarBulto("BLT-MIX-001", 1.2m);
        envio.RecepcionarEnDeposito(new[] { "BLT-MIX-001" }, null, null, UsuarioRepartidorId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BLT-MIX-001", PrioridadBulto.Normal) }, UsuarioRepartidorId);
        envio.AsignarRuta(HojaDeRutaId);

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var envioRepo = new FakeEnvioRepository();
        envioRepo.Envios.Add(envio);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var tenant = new FakeCurrentTenant { OperadorId = OperadorId, UsuarioId = UsuarioRepartidorId };

        var handler = new ConfirmarCargaCommandHandler(rutaRepo, envioRepo, uow, tenant, outbox);
        var result = await handler.Handle(
            new ConfirmarCargaCommand(HojaDeRutaId, new List<string> { "BLT-MIX-001" }, "IDEMP-CARGA-MIXTA", null, true),
            CancellationToken.None);

        Assert.Equal("EnTransito", result.EstadoRuta);
        Assert.Equal(1, result.TotalBultosCargados);
        Assert.Equal(0, result.TotalBultosFaltantes);
        Assert.Equal("Pendiente", paradaRetiro.Estado);
        Assert.True(paradaRetiro.EsRetiro);
    }

    [Fact]
    public async Task RegistrarEntregaExitosa_EnvioEnAsignadoARuta_PromueveAEnTransitoYCompletaEntrega()
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "Liberada");
        ruta.AgregarParada(Parada1Id, Envio1Id, 1, Direccion1Id, null, "Pendiente");

        var envio = new Envio(
            Envio1Id,
            ComercioId,
            "Av. 18 de Julio 1450, Centro, Montevideo",
            "Martín Rodríguez",
            "ENV-2026-00999",
            OperadorId);
        envio.AgregarBulto("BLT-999-A", 1.2m);
        envio.RecepcionarEnDeposito(new[] { "BLT-999-A" }, null, null, UsuarioRepartidorId);
        envio.ClasificarBultos(new[] { new ClasificacionBulto("BLT-999-A", PrioridadBulto.Normal) }, UsuarioRepartidorId);
        envio.AsignarRuta(HojaDeRutaId);

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var envioRepo = new FakeEnvioRepository();
        envioRepo.Envios.Add(envio);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var cache = new FakeCacheService();
        var clock = new FakeClock();
        var tenant = new FakeCurrentTenant { OperadorId = OperadorId, UsuarioId = UsuarioRepartidorId };

        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);
        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-ASIGNADO-RUTA-001",
            "Martín Rodríguez",
            "41234567",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: "foto_base_64",
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Entregado", result.EstadoEnvio);
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal("Finalizada", ruta.Estado);
    }

    [Fact]
    public async Task RegistrarEntregaExitosa_EnvioRetiradoDeRutaEnDepositoYParadaCancelada_PrevaleceEntregaFisicaYRegistraConflicto()
    {
        var ruta = new HojaDeRuta(HojaDeRutaId, OperadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnCarga");
        ruta.AgregarParada(Parada1Id, Envio1Id, 1, Direccion1Id, null, "Pendiente");
        ruta.RegistrarAsignacion(Envio1Id, UsuarioRepartidorId);

        var envio = new Envio(
            Envio1Id,
            ComercioId,
            "Av. 18 de Julio 1450, Centro, Montevideo",
            "Martín Rodríguez",
            "TRK-36B87B024C",
            OperadorId);
        envio.AgregarBulto("TRK-36B87B024C-B01", 1.2m);
        envio.AgregarBulto("TRK-36B87B024C-B02", 1.4m);
        envio.RecepcionarEnDeposito(new[] { "TRK-36B87B024C-B01", "TRK-36B87B024C-B02" }, null, null, UsuarioRepartidorId);
        envio.ClasificarBultos(new[]
        {
            new ClasificacionBulto("TRK-36B87B024C-B01", PrioridadBulto.Normal),
            new ClasificacionBulto("TRK-36B87B024C-B02", PrioridadBulto.Normal)
        }, UsuarioRepartidorId);
        envio.AsignarRuta(HojaDeRutaId);

        // El despachador retira/cancela el envío de la ruta mientras el repartidor estaba offline y lo entregó con evidencia
        ruta.RetirarEnvio(Envio1Id);
        envio.RetirarDeRuta("AveriaVehiculo · Despachador: Despachador Turno Mañana", UsuarioRepartidorId);

        Assert.Equal(EstadoEnvio.EnDeposito, envio.EstadoActual);
        Assert.Null(envio.HojaDeRutaId);
        Assert.Equal("Cancelada", ruta.Paradas.Single().Estado);

        var rutaRepo = new FakeRutaRepository();
        rutaRepo.Rutas.Add(ruta);
        var envioRepo = new FakeEnvioRepository();
        envioRepo.Envios.Add(envio);
        var outbox = new FakeOutboxStorage();
        var uow = new FakeUnitOfWork();
        var cache = new FakeCacheService();
        var clock = new FakeClock();
        var tenant = new FakeCurrentTenant { OperadorId = OperadorId, UsuarioId = UsuarioRepartidorId };

        var handler = new RegistrarEntregaExitosaCommandHandler(envioRepo, rutaRepo, uow, outbox, cache, tenant, clock);
        var command = new RegistrarEntregaExitosaCommand(
            HojaDeRutaId,
            Parada1Id,
            Envio1Id,
            "IDEMP-CONFLICTO-RETIRO-001",
            "Martín Rodríguez",
            "41234567",
            FirmaDigitalBase64: "firma_base_64",
            FotoEvidenciaBase64: "foto_base_64",
            Latitud: -34.9058,
            Longitud: -56.1885,
            FechaHoraDispositivoUtc: clock.UtcNow);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Entregado", result.EstadoEnvio);
        Assert.Equal(EstadoEnvio.Entregado, envio.EstadoActual);
        Assert.Equal(HojaDeRutaId, envio.HojaDeRutaId);
        Assert.Equal("Entregada", ruta.Paradas.Single().Estado);
        var conflicto = Assert.Single(envioRepo.Conflictos);
        Assert.Equal(Envio1Id, conflicto.EntidadId);
        Assert.Equal("EnDeposito", conflicto.EstadoServidor);
        Assert.Equal("Entregado", conflicto.EstadoDispositivo);
        Assert.Equal("PrevaleceEntregaFisicaConEvidencia", conflicto.PoliticaAplicada);
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
