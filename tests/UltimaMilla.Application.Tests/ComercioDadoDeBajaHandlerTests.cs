using UltimaMilla.Application.Commands.AltaEnvio;
using UltimaMilla.Application.Commands.Comercio.ConfigurarAvisos;
using UltimaMilla.Application.Commands.Comercio.ConfirmarDevolucion;
using UltimaMilla.Application.Commands.Comercio.ImportarEnvios;
using UltimaMilla.Application.Commands.Comercio.RegenerarClaveApi;
using UltimaMilla.Application.Commands.Comercio.ReintentarAviso;
using UltimaMilla.Application.Commands.Identidad.IniciarSesion;
using UltimaMilla.Application.Commands.Mensajeria;
using UltimaMilla.Application.Commands.Mensajeria.GestionarWebhooks;
using UltimaMilla.Application.Commands.Operador.GestionComercios;
using UltimaMilla.Application.Commands.Retiros.GestionRetiros;
using UltimaMilla.Application.Commands.Retiros.SolicitarRetiro;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Comercio.GenerarEtiquetasEnvios;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Enums;
using Xunit;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Application.Tests;

public class ComercioDadoDeBajaHandlerTests
{
    private sealed class FakeRetiroRepositoryLocal : IRetiroRepository
    {
        public List<Retiro> Retiros { get; } = new();

        public Task AddAsync(Retiro retiro, CancellationToken cancellationToken = default)
        {
            Retiros.Add(retiro);
            return Task.CompletedTask;
        }

        public Task<Retiro?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.FirstOrDefault(r => r.Id == id));

        public Task<List<Retiro>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
        {
            var set = ids.ToHashSet();
            return Task.FromResult(Retiros.Where(r => set.Contains(r.Id)).ToList());
        }

        public Task<Retiro?> GetByCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.FirstOrDefault(r => string.Equals(r.CodigoSeguimientoPublico, codigoSeguimiento, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> ExisteCodigoSeguimientoAsync(string codigoSeguimiento, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.Any(r => string.Equals(r.CodigoSeguimientoPublico, codigoSeguimiento, StringComparison.OrdinalIgnoreCase)));

        public Task<Retiro?> GetByReferenciaExternaAsync(Guid comercioOperadorId, string referenciaExterna, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.FirstOrDefault(r => r.ComercioOperadorId == comercioOperadorId && r.ReferenciaExterna == referenciaExterna));

        public Task<(List<Retiro> Items, int Total)> BuscarPorComercioAsync(
            Guid operadorId,
            Guid comercioOperadorId,
            EstadoRetiro? estado,
            int pagina,
            int tamanoPagina,
            CancellationToken cancellationToken = default)
        {
            var q = Retiros.Where(r => r.OperadorId == operadorId && r.ComercioOperadorId == comercioOperadorId && (!estado.HasValue || r.EstadoActual == estado.Value)).ToList();
            return Task.FromResult((q.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList(), q.Count));
        }

        public Task<List<Retiro>> ListarPorOperadorAsync(Guid operadorId, EstadoRetiro? estado, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.Where(r => r.OperadorId == operadorId && (!estado.HasValue || r.EstadoActual == estado.Value)).ToList());

        public Task<IReadOnlyDictionary<EstadoRetiro, int>> ContarPorEstadoAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<EstadoRetiro, int>>(new Dictionary<EstadoRetiro, int>());

        public Task<List<Retiro>> ListarParaBajaComercioAsync(Guid operadorId, Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Retiros.Where(r =>
                r.OperadorId == operadorId
                && r.ComercioOperadorId == comercioOperadorId
                && r.EstadoActual is EstadoRetiro.Solicitado or EstadoRetiro.AsignadoARuta).ToList());

        public Task UpdateAsync(Retiro retiro, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeWebhookSenderLocal : IWebhookSender
    {
        public Task<ResultadoWebhook> EnviarAsync(
            string urlWebhook,
            string payloadJson,
            string firmaHmac,
            Guid avisoId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResultadoWebhook(true, 200, "OK"));
    }

    private sealed class FakeAvisoRepoLocal : IAvisoComercioRepository
    {
        public List<SuscripcionAviso> Suscripciones { get; } = new();
        public List<AvisoComercio> Avisos { get; } = new();

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.Where(s => s.ComercioOperadorId == comercioOperadorId && s.Activa).ToList());

        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default)
        {
            Avisos.Add(aviso);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos.Take(maximo).ToList());

        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.Where(s => !comercioOperadorId.HasValue || comercioOperadorId.Value == Guid.Empty || s.ComercioOperadorId == comercioOperadorId.Value).ToList());

        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Suscripciones.FirstOrDefault(s => s.Id == suscripcionId));

        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default)
        {
            Suscripciones.Add(suscripcion);
            return Task.CompletedTask;
        }

        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos.Where(a => string.IsNullOrWhiteSpace(estado) || a.Estado == estado).Take(limite).ToList());

        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos.FirstOrDefault(a => a.Id == avisoId));
    }

    private sealed class FakeNotifierLocal : ITableroRealtimeNotifier
    {
        public List<Guid> ComerciosNotificados { get; } = new();
        public Task NotificarOperadoresDelComercioAsync(Guid comercioId, CancellationToken ct = default)
        {
            ComerciosNotificados.Add(comercioId);
            return Task.CompletedTask;
        }
        public Task NotificarCambioRutaAsync(Guid operadorId, Guid hojaDeRutaId, string estado, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotificarHitoParadaAsync(Guid operadorId, Guid hojaDeRutaId, Guid paradaId, string estadoParada, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static Envio CrearEnvio(Guid operadorId, Guid comercioOperadorId, string codigo)
    {
        var envio = new Envio(
            Guid.NewGuid(),
            comercioOperadorId,
            Guid.NewGuid(),
            "Estandar",
            codigo,
            direccionEntregaId: Guid.NewGuid(),
            direccionDestino: "Av. Rivera 1000",
            nombreDestinatario: "Cliente Prueba",
            operadorId: operadorId);
        envio.AgregarBulto($"{codigo}-B1", 1m, 10m, 10m, 10m);
        return envio;
    }

    private static Retiro CrearRetiro(Guid operadorId, Guid comercioOperadorId, string codigo)
    {
        var retiro = new Retiro(
            Guid.NewGuid(),
            operadorId,
            comercioOperadorId,
            codigo,
            "Juan Pérez",
            Guid.NewGuid(),
            "Bvar. España 2100",
            "Cambio de talle");
        retiro.AgregarBulto("Caja", 1m, 10m, 10m, 10m);
        return retiro;
    }

    [Fact]
    public async Task CambiarEstado_DarDeBaja_CancelaOperacionesPreviasASalidaYMantieneEnTransitoYOtrosComercios()
    {
        var operadorId = Guid.NewGuid();
        var otroOperadorId = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var usuarioOperadorId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercioId, operadorId, "huella-1");
        var otroComercioVinculo = new ComercioOperador(Guid.NewGuid(), Guid.NewGuid(), operadorId, "huella-2");
        var otroOperadorVinculo = new ComercioOperador(Guid.NewGuid(), comercioId, otroOperadorId, "huella-3");

        var enviosRepo = new FakeEnvioRepository();
        var retirosRepo = new FakeRetiroRepositoryLocal();
        var rutasRepo = new FakeRutaRepository();
        var enlacesRepo = new FakeComercioOperadorRepository();
        enlacesRepo.Relaciones.AddRange([vinculo, otroComercioVinculo, otroOperadorVinculo]);
        var tenant = new FakeCurrentTenant { OperadorId = operadorId, UsuarioId = usuarioOperadorId };
        var uow = new FakeUnitOfWork();
        var notifier = new FakeNotifierLocal();

        var envioAdmitido = CrearEnvio(operadorId, vinculo.Id, "ENV-ADM");

        var envioEnDeposito = CrearEnvio(operadorId, vinculo.Id, "ENV-DEP");
        envioEnDeposito.RecepcionarEnDeposito(["ENV-DEP-B1"], null, null, usuarioOperadorId);

        var rutaExclusiva = new HojaDeRuta(Guid.NewGuid(), operadorId, DateOnly.FromDateTime(DateTime.UtcNow), "Liberada");
        var envioEnRutaExclusiva = CrearEnvio(operadorId, vinculo.Id, "ENV-RUT1");
        envioEnRutaExclusiva.RecepcionarEnDeposito(["ENV-RUT1-B1"], null, null, usuarioOperadorId);
        envioEnRutaExclusiva.AsignarRuta(rutaExclusiva.Id);
        rutaExclusiva.AgregarParada(Guid.NewGuid(), envioEnRutaExclusiva.Id, 1, envioEnRutaExclusiva.DireccionEntregaId!.Value);
        rutaExclusiva.RegistrarAsignacion(envioEnRutaExclusiva.Id, usuarioOperadorId);

        var retiroEnRutaExclusiva = CrearRetiro(operadorId, vinculo.Id, "RET-RUT1");
        retiroEnRutaExclusiva.AsignarARuta(rutaExclusiva.Id, usuarioOperadorId);
        rutaExclusiva.AgregarParadaRetiro(retiroEnRutaExclusiva.Id, 2, retiroEnRutaExclusiva.DireccionRetiroId);

        var rutaCompartida = new HojaDeRuta(Guid.NewGuid(), operadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnCarga");
        var envioEnRutaCompartida = CrearEnvio(operadorId, vinculo.Id, "ENV-RUT2");
        envioEnRutaCompartida.RecepcionarEnDeposito(["ENV-RUT2-B1"], null, null, usuarioOperadorId);
        envioEnRutaCompartida.AsignarRuta(rutaCompartida.Id);
        rutaCompartida.AgregarParada(Guid.NewGuid(), envioEnRutaCompartida.Id, 1, envioEnRutaCompartida.DireccionEntregaId!.Value);
        rutaCompartida.RegistrarAsignacion(envioEnRutaCompartida.Id, usuarioOperadorId);

        var envioOtroComercio = CrearEnvio(operadorId, otroComercioVinculo.Id, "ENV-OTRO");
        envioOtroComercio.RecepcionarEnDeposito(["ENV-OTRO-B1"], null, null, usuarioOperadorId);
        envioOtroComercio.AsignarRuta(rutaCompartida.Id);
        rutaCompartida.AgregarParada(Guid.NewGuid(), envioOtroComercio.Id, 2, envioOtroComercio.DireccionEntregaId!.Value);
        rutaCompartida.RegistrarAsignacion(envioOtroComercio.Id, usuarioOperadorId);

        var rutaDespachada = new HojaDeRuta(Guid.NewGuid(), operadorId, DateOnly.FromDateTime(DateTime.UtcNow), "EnTransito");
        var envioEnTransito = CrearEnvio(operadorId, vinculo.Id, "ENV-CALL");
        envioEnTransito.RecepcionarEnDeposito(["ENV-CALL-B1"], null, null, usuarioOperadorId);
        envioEnTransito.AsignarRuta(rutaDespachada.Id);
        envioEnTransito.DespacharEnRuta(rutaDespachada.Id, usuarioOperadorId);

        var envioOtroOperador = CrearEnvio(otroOperadorId, otroOperadorVinculo.Id, "ENV-OP2");
        var retiroSolicitado = CrearRetiro(operadorId, vinculo.Id, "RET-SOL");

        enviosRepo.Envios.AddRange([envioAdmitido, envioEnDeposito, envioEnRutaExclusiva, envioEnRutaCompartida, envioOtroComercio, envioEnTransito, envioOtroOperador]);
        retirosRepo.Retiros.AddRange([retiroSolicitado, retiroEnRutaExclusiva]);
        rutasRepo.Rutas.AddRange([rutaExclusiva, rutaCompartida, rutaDespachada]);

        var procesador = new BajaComercioProcesador(enviosRepo, retirosRepo, rutasRepo);
        var handler = new CambiarEstadoComercioCommandHandler(enlacesRepo, tenant, uow, notifier, procesador);

        // Act 1: dar de baja
        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, Activo: false), CancellationToken.None);

        // Act 2: idempotencia al volver a invocar la baja
        var eventosAntes = envioAdmitido.Eventos.Count;
        await handler.Handle(new CambiarEstadoComercioCommand(comercioId, Activo: false), CancellationToken.None);

        // Assert
        Assert.False(vinculo.EstaActivo);
        Assert.Equal(EstadoEnvio.Cancelado, envioAdmitido.EstadoActual);
        Assert.Equal(EstadoEnvio.Cancelado, envioEnDeposito.EstadoActual);
        Assert.Equal(EstadoEnvio.Cancelado, envioEnRutaExclusiva.EstadoActual);
        Assert.Null(envioEnRutaExclusiva.HojaDeRutaId);
        Assert.Equal(EstadoEnvio.Cancelado, envioEnRutaCompartida.EstadoActual);
        Assert.Null(envioEnRutaCompartida.HojaDeRutaId);
        Assert.Equal(EstadoRetiro.Cancelado, retiroSolicitado.EstadoActual);
        Assert.Equal(EstadoRetiro.Cancelado, retiroEnRutaExclusiva.EstadoActual);
        Assert.Equal("Cancelada", rutaExclusiva.Estado);
        Assert.False(rutaExclusiva.TieneParadasVigentes);

        // La ruta compartida sigue viva con el envío del otro comercio
        Assert.Equal("EnCarga", rutaCompartida.Estado);
        Assert.True(rutaCompartida.TieneParadasVigentes);
        Assert.Equal(EstadoEnvio.AsignadoARuta, envioOtroComercio.EstadoActual);

        // Lo que ya salió a la calle o pertenece a otro operador no se toca
        Assert.Equal(EstadoEnvio.EnTransito, envioEnTransito.EstadoActual);
        Assert.Equal(EstadoEnvio.Admitido, envioOtroOperador.EstadoActual);

        // Idempotencia y notificación en tiempo real
        Assert.Equal(eventosAntes, envioAdmitido.Eventos.Count);
        Assert.Equal(2, notifier.ComerciosNotificados.Count(id => id == comercioId));
    }

    [Fact]
    public async Task ActualizarComercio_DesactivarVinculo_EjecutaCancelacionYNotifica()
    {
        var operadorId = Guid.NewGuid();
        var comercio = new ComercioEntidad(Guid.NewGuid(), "Comercio Test S.A.", "Test", "219000110019", "c@test.uy", "099111222", "Dir 1");
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercio.Id, operadorId, "huella-a");
        var enviosRepo = new FakeEnvioRepository();
        var envio = CrearEnvio(operadorId, vinculo.Id, "ENV-ACT");
        enviosRepo.Envios.Add(envio);

        var comerciosRepo = new FakeComercioRepository();
        comerciosRepo.Comercios.Add(comercio);
        var enlacesRepo = new FakeComercioOperadorRepository();
        enlacesRepo.Relaciones.Add(vinculo);
        var notifier = new FakeNotifierLocal();
        var procesador = new BajaComercioProcesador(enviosRepo, new FakeRetiroRepositoryLocal(), new FakeRutaRepository());
        var handler = new ActualizarComercioCommandHandler(
            comerciosRepo, enlacesRepo, new FakeCurrentTenant { OperadorId = operadorId, UsuarioId = Guid.NewGuid() }, new FakeUnitOfWork(), procesador, notifier);

        var res = await handler.Handle(
            new ActualizarComercioCommand(comercio.Id, "Comercio Test S.A.", "Test", "c@test.uy", "099111222", "Dir 1", Activo: false, comercio.SelloModificacion.ToString()),
            CancellationToken.None);

        Assert.False(res.Activo);
        Assert.Equal(EstadoEnvio.Cancelado, envio.EstadoActual);
        Assert.Contains(comercio.Id, notifier.ComerciosNotificados);
    }

    [Fact]
    public async Task IniciarSesion_ComercioDadoDeBajaEnUnicoOperador_PermiteIngresoSoloLectura()
    {
        var operadorId = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var perfil = new Perfil(Guid.NewGuid(), "Comercio", "Comercio");
        var hasher = new FakePasswordHasherService();
        var usuario = new Usuario(Guid.NewGuid(), "Comercio", "comercio@baja.uy", hasher.HashearPassword("Pass123!"), perfil.Id, operadorId, comercioId);
        var vinculoSuspendido = new ComercioOperador(Guid.NewGuid(), comercioId, operadorId, "huella");
        vinculoSuspendido.Suspender();

        var usuariosRepo = new FakeUsuarioRepository();
        usuariosRepo.Usuarios.Add(usuario);
        usuariosRepo.Perfiles.Add(perfil);
        var linksRepo = new FakeComercioOperadorRepository();
        linksRepo.Relaciones.Add(vinculoSuspendido);
        var jwt = new FakeTokenJwtService();

        var handler = new IniciarSesionCommandHandler(usuariosRepo, hasher, jwt, new FakeClock(), new FakeUnitOfWork(), linksRepo);
        var res = await handler.Handle(new IniciarSesionCommand("comercio@baja.uy", "Pass123!"), CancellationToken.None);

        Assert.Equal(vinculoSuspendido.Id, jwt.UltimoComercioOperadorId);
        Assert.Equal(operadorId, res.OperadorId);
    }

    [Fact]
    public async Task IniciarSesion_ComercioConOperadorDefaultDadoDeBajaYOrtoActivo_EligeOperadorActivo()
    {
        var operadorBajaId = Guid.NewGuid();
        var operadorActivoId = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var perfil = new Perfil(Guid.NewGuid(), "Comercio", "Comercio");
        var hasher = new FakePasswordHasherService();
        var usuario = new Usuario(Guid.NewGuid(), "Comercio Multi", "multi@baja.uy", hasher.HashearPassword("Pass123!"), perfil.Id, operadorBajaId, comercioId);

        var linkBaja = new ComercioOperador(Guid.NewGuid(), comercioId, operadorBajaId, "h1");
        linkBaja.Suspender();
        var linkActivo = new ComercioOperador(Guid.NewGuid(), comercioId, operadorActivoId, "h2");

        var usuariosRepo = new FakeUsuarioRepository();
        usuariosRepo.Usuarios.Add(usuario);
        usuariosRepo.Perfiles.Add(perfil);
        var linksRepo = new FakeComercioOperadorRepository();
        linksRepo.Relaciones.AddRange([linkBaja, linkActivo]);
        var jwt = new FakeTokenJwtService();

        var handler = new IniciarSesionCommandHandler(usuariosRepo, hasher, jwt, new FakeClock(), new FakeUnitOfWork(), linksRepo);
        var res = await handler.Handle(new IniciarSesionCommand("multi@baja.uy", "Pass123!"), CancellationToken.None);

        Assert.Equal(linkActivo.Id, jwt.UltimoComercioOperadorId);
        Assert.Equal(operadorActivoId, res.OperadorId);
    }

    [Fact]
    public async Task ComercioSuspendido_BloqueaTodosLosHandlersDeEscrituraYAlReactivarVuelveAPermitirlos()
    {
        var operadorId = Guid.NewGuid();
        var comercioId = Guid.NewGuid();
        var vinculo = new ComercioOperador(Guid.NewGuid(), comercioId, operadorId, ComercioOperador.CalcularHuellaClaveApi("clave-123"));
        vinculo.Suspender();

        var linksRepo = new FakeComercioOperadorRepository();
        linksRepo.Relaciones.Add(vinculo);

        var tenant = new FakeCurrentTenant
        {
            OperadorId = operadorId,
            ComercioOperadorId = vinculo.Id,
            UsuarioId = Guid.NewGuid()
        };
        var uow = new FakeUnitOfWork();
        var clock = new FakeClock();
        var enviosRepo = new FakeEnvioRepository();
        var retirosRepo = new FakeRetiroRepositoryLocal();
        var suscripcionesRepo = new FakeSuscripcionAvisoRepository();
        var avisosConsultaRepo = new FakeAvisoComercioConsultaRepository(suscripcionesRepo);
        var avisoRepoWorker = new FakeAvisoRepoLocal();
        var devolucionesRepo = new FakeDevolucionRepository();
        var deliveredSender = new FakeWebhookSenderLocal();
        var entregador = new AvisoComercioEntregador(avisoRepoWorker, deliveredSender, uow, clock);

        var altaProcesador = new AltaEnvioProcesador(enviosRepo, new FakeDestinatarioRepository(), new FakeDireccionRepository(), new FakeCuadroTarifarioRepository(), clock, new FakeOutboxStorage());
        var altaHandler = new AltaEnvioCommandHandler(altaProcesador, tenant, uow, linksRepo);
        var importarHandler = new ImportarEnviosCommandHandler(altaProcesador, new FakeLoteImportacionRepository(), tenant, clock, uow, linksRepo);
        var regenerarHandler = new RegenerarClaveApiCommandHandler(linksRepo, tenant, uow);
        var retiroProcesador = new SolicitarRetiroProcesador(retirosRepo, new FakeDireccionRepository(), new FakeCuadroTarifarioRepository(), clock);
        var solicitarRetiroHandler = new SolicitarRetiroCommandHandler(retiroProcesador, tenant, uow, linksRepo);
        var cancelarRetiroHandler = new CancelarRetiroCommandHandler(retirosRepo, uow, tenant, linksRepo);
        var confirmarDevHandler = new ConfirmarDevolucionCommandHandler(enviosRepo, devolucionesRepo, new FakeOutboxStorage(), uow, tenant, clock, linksRepo);
        var configAvisosHandler = new ConfigurarAvisosCommandHandler(suscripcionesRepo, tenant, uow, linksRepo);
        var probarWebhookHandler = new ProbarWebhookCommandHandler(suscripcionesRepo, tenant, linksRepo);
        var reintentarAvisoHandler = new ReintentarAvisoCommandHandler(avisosConsultaRepo, suscripcionesRepo, tenant, entregador, linksRepo);
        var crearSubHandler = new CrearSuscripcionAvisoCommandHandler(avisoRepoWorker, linksRepo, tenant, uow);
        var desactivarSubHandler = new DesactivarSuscripcionAvisoCommandHandler(avisoRepoWorker, uow, linksRepo, tenant);
        var reenviarManualHandler = new ReenviarAvisoManualCommandHandler(avisoRepoWorker, entregador, tenant, linksRepo);
        var etiquetasHandler = new GenerarEtiquetasEnviosQueryHandler(enviosRepo, tenant, null, linksRepo);

        var altaCmd = new AltaEnvioCommand(
            vinculo.Id,
            "Estandar",
            new DestinatarioAltaDto("A", Email: "a@b.c", Telefono: "099"),
            new DireccionAltaDto("Calle", "1", "Mvd"),
            [new BultoAltaDto(1, 10, 10, 10)]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            altaHandler.Handle(altaCmd, CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            importarHandler.Handle(new ImportarEnviosCommand("lote-1", [new FilaImportacion(2, altaCmd)]), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            regenerarHandler.Handle(new RegenerarClaveApiCommand(), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            solicitarRetiroHandler.Handle(new SolicitarRetiroCommand(vinculo.Id, new ContactoRetiroDto("Juan"), new DireccionAltaDto("Calle 1", "1", "Mvd"), [new BultoRetiroAltaDto("Caja", 1, 10, 10, 10)], "Cambio"), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            cancelarRetiroHandler.Handle(new CancelarRetiroCommand(Guid.NewGuid(), "Cancelar"), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            confirmarDevHandler.Handle(new ConfirmarDevolucionCommand(Guid.NewGuid()), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            configAvisosHandler.Handle(new ConfigurarAvisosCommand("https://ejemplo.uy/hook", ["Entregado"]), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            probarWebhookHandler.Handle(new ProbarWebhookCommand("https://ejemplo.uy/hook"), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reintentarAvisoHandler.Handle(new ReintentarAvisoCommand(Guid.NewGuid()), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            crearSubHandler.Handle(new CrearSuscripcionAvisoCommand(vinculo.Id, "EnvioEntregado", "https://ejemplo.uy/hook", "clave12345"), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            desactivarSubHandler.Handle(new DesactivarSuscripcionAvisoCommand(Guid.NewGuid(), vinculo.Id), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            reenviarManualHandler.Handle(new ReenviarAvisoManualCommand(Guid.NewGuid()), CancellationToken.None));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            etiquetasHandler.Handle(new GenerarEtiquetasEnviosQuery([Guid.NewGuid()]), CancellationToken.None));

        // Reactivar vuelve a permitir operaciones
        var estadoHandler = new CambiarEstadoComercioCommandHandler(linksRepo, tenant, uow);
        await estadoHandler.Handle(new CambiarEstadoComercioCommand(comercioId, Activo: true), CancellationToken.None);

        var clave = await regenerarHandler.Handle(new RegenerarClaveApiCommand(), CancellationToken.None);
        Assert.StartsWith("um_", clave.ClaveApi);
    }
}
