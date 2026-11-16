using UltimaMilla.Application.Commands.Incidencias.AbrirIncidencia;
using UltimaMilla.Application.Commands.Incidencias.CerrarIncidencia;
using UltimaMilla.Application.Commands.Incidencias.RegistrarSeguimientoIncidencia;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Incidencias;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class IncidenciasHandlerTests
{
    private readonly FakeOutboxStorage _outbox = new();
    private readonly FakeIncidenciaRepository _incidencias = new();
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeRutaRepository _rutas = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _uow = new();

    private AbrirIncidenciaCommandHandler Abrir() => new(_incidencias, _envios, _rutas, _tenant, _clock, _uow, _outbox);
    private RegistrarSeguimientoIncidenciaCommandHandler Seguimiento() => new(_incidencias, _tenant, _clock, _uow, _outbox);
    private CerrarIncidenciaCommandHandler Cerrar() => new(_incidencias, _tenant, _clock, _uow, _outbox);

    private Incidencia CrearIncidencia(Guid? operadorId = null)
    {
        var incidencia = new Incidencia(Guid.NewGuid(), operadorId ?? _tenant.OperadorId, null, "AveriaVehiculo", "Se pinchó una rueda");
        _incidencias.Incidencias.Add(incidencia);
        return incidencia;
    }

    [Fact]
    public async Task Abrir_SinEnvio_RegistraIncidenciaAbiertaDelOperador()
    {
        var resultado = await Abrir().Handle(new AbrirIncidenciaCommand("AveriaVehiculo", "Se pinchó una rueda", "Alta"), default);

        var guardada = Assert.Single(_incidencias.Incidencias);
        Assert.Equal(_tenant.OperadorId, guardada.OperadorId);
        Assert.Equal("Abierta", resultado.Estado);
        Assert.Equal("Alta", resultado.Severidad);
        Assert.Equal("IncidenciaAbiertaIntegrationEvent", Assert.Single(_outbox.Mensajes).TipoEvento);
        Assert.Equal(_tenant.UsuarioId, guardada.CreadaPorUsuarioId);
    }

    [Fact]
    public async Task Abrir_ConEnvioDeOtroOperador_LanzaUnauthorizedYNoPersiste()
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-AJENO", operadorId: Guid.NewGuid());
        _envios.Envios.Add(envio);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Abrir().Handle(new AbrirIncidenciaCommand("Reclamo", "Reclamo del destinatario", null, envio.Id), default));

        Assert.Empty(_incidencias.Incidencias);
    }

    [Fact]
    public async Task Abrir_ConEnvioInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Abrir().Handle(new AbrirIncidenciaCommand("Reclamo", "Reclamo", null, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Abrir_ConTipoODescripcionDemasiadoLargos_LanzaDomainValidationEnVezDeFallarAlGuardar()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Abrir().Handle(new AbrirIncidenciaCommand(new string('t', 51), "Algo pasó"), default));
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Abrir().Handle(new AbrirIncidenciaCommand("Otro", new string('d', 1001)), default));
        Assert.Empty(_incidencias.Incidencias);
    }

    [Fact]
    public async Task Abrir_ConSeveridadInvalida_LanzaDomainValidation()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Abrir().Handle(new AbrirIncidenciaCommand("Otro", "Algo pasó", "Urgentisima"), default));
    }

    [Fact]
    public async Task Seguimiento_TomarACargo_AsignaResponsableYPasaAEnSeguimiento()
    {
        var incidencia = CrearIncidencia();

        var resultado = await Seguimiento().Handle(
            new RegistrarSeguimientoIncidenciaCommand(incidencia.Id, "Llamé al taller", TomarACargo: true), default);

        Assert.Equal("EnSeguimiento", resultado.Estado);
        Assert.Equal(_tenant.UsuarioId, incidencia.ResponsableUsuarioId);
        Assert.Equal(2, incidencia.Notas.Count);
    }

    [Fact]
    public async Task Seguimiento_SinComentarioNiToma_LanzaDomainValidation()
    {
        var incidencia = CrearIncidencia();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Seguimiento().Handle(new RegistrarSeguimientoIncidenciaCommand(incidencia.Id, "  "), default));
    }

    [Fact]
    public async Task Seguimiento_DeOtroOperador_LanzaUnauthorized()
    {
        var incidencia = CrearIncidencia(Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Seguimiento().Handle(new RegistrarSeguimientoIncidenciaCommand(incidencia.Id, "Nota"), default));
    }

    [Fact]
    public async Task Cerrar_ConResolucion_CierraYRegistraFecha()
    {
        var incidencia = CrearIncidencia();

        var resultado = await Cerrar().Handle(new CerrarIncidenciaCommand(incidencia.Id, "Se cambió la rueda"), default);

        Assert.Equal("Cerrada", resultado.Estado);
        Assert.Equal(_clock.UtcNow, incidencia.FechaCierre);
        Assert.Equal("Se cambió la rueda", incidencia.Resolucion);
    }

    [Fact]
    public async Task Cerrar_SinResolucion_LanzaDomainValidation()
    {
        var incidencia = CrearIncidencia();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Cerrar().Handle(new CerrarIncidenciaCommand(incidencia.Id, null), default));

        Assert.Equal("Abierta", incidencia.Estado);
    }

    [Fact]
    public async Task Cerrar_IncidenciaYaCerrada_LanzaDomainValidation()
    {
        var incidencia = CrearIncidencia();
        await Cerrar().Handle(new CerrarIncidenciaCommand(incidencia.Id, "Resuelta"), default);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Cerrar().Handle(new CerrarIncidenciaCommand(incidencia.Id, "Otra vez"), default));
    }

    [Fact]
    public async Task Listar_DevuelveSoloLasDelOperadorYFiltraPorEstado()
    {
        var abierta = CrearIncidencia();
        var cerrada = CrearIncidencia();
        cerrada.Cerrar(Guid.NewGuid(), "Listo");
        CrearIncidencia(Guid.NewGuid());

        var handler = new ListarIncidenciasQueryHandler(_incidencias, _tenant);
        var todas = await handler.Handle(new ListarIncidenciasQuery(), default);
        var soloAbiertas = await handler.Handle(new ListarIncidenciasQuery("Abierta"), default);

        Assert.Equal(2, todas.Total);
        var item = Assert.Single(soloAbiertas.Items);
        Assert.Equal(abierta.Id, item.Id);
    }

    [Fact]
    public async Task Obtener_DeOtroOperador_LanzaUnauthorized()
    {
        var incidencia = CrearIncidencia(Guid.NewGuid());
        var handler = new ObtenerIncidenciaQueryHandler(_incidencias, _tenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new ObtenerIncidenciaQuery(incidencia.Id), default));
    }

    [Fact]
    public async Task ListarConflictos_RequestNulo_LanzaArgumentNullException()
    {
        var handler = new ListarConflictosSincronizacionQueryHandler(new FakeConflictoSincronizacionRepository(), _tenant);
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, default));
    }

    [Fact]
    public async Task ListarConflictos_SinOperadorAutenticado_LanzaUnauthorized()
    {
        var tenantVacio = new FakeCurrentTenant { OperadorId = Guid.Empty, IsAuthenticated = false };
        var handler = new ListarConflictosSincronizacionQueryHandler(new FakeConflictoSincronizacionRepository(), tenantVacio);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new ListarConflictosSincronizacionQuery(), default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    public async Task ListarConflictos_MaximoFueraDeRango_LanzaDomainValidation(int maximo)
    {
        var handler = new ListarConflictosSincronizacionQueryHandler(new FakeConflictoSincronizacionRepository(), _tenant);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(new ListarConflictosSincronizacionQuery(maximo), default));
    }

    [Fact]
    public async Task ListarConflictos_ListaSoloLosDelOperadorActualYRespetaTopeYOrden()
    {
        var otroOperador = Guid.NewGuid();
        var repo = new FakeConflictoSincronizacionRepository();
        repo.Agregar(_tenant.OperadorId, new ConflictoSincronizacionDto(
            Guid.NewGuid(), _clock.UtcNow.AddMinutes(-30), "Envio", Guid.NewGuid(), "ENV-1",
            "Entregado", "Cancelado", "PrevaleceEntregaFisicaConEvidencia", "Prevalece evidencia."));
        repo.Agregar(_tenant.OperadorId, new ConflictoSincronizacionDto(
            Guid.NewGuid(), _clock.UtcNow.AddMinutes(-5), "Envio", Guid.NewGuid(), "ENV-2",
            "NoEntregado", "EnDeposito", "PrevaleceEstadoServidor", "Prevalece servidor."));
        repo.Agregar(otroOperador, new ConflictoSincronizacionDto(
            Guid.NewGuid(), _clock.UtcNow, "Envio", Guid.NewGuid(), "ENV-AJENO",
            "Entregado", "EnDeposito", "PrevaleceEntregaFisicaConEvidencia", "No visible."));

        var handler = new ListarConflictosSincronizacionQueryHandler(repo, _tenant);
        var todos = await handler.Handle(new ListarConflictosSincronizacionQuery(), default);
        var acotados = await handler.Handle(new ListarConflictosSincronizacionQuery(1), default);

        Assert.Equal(2, todos.Count);
        Assert.Equal("ENV-2", todos[0].CodigoSeguimiento);
        Assert.Equal("ENV-1", todos[1].CodigoSeguimiento);
        Assert.DoesNotContain(todos, c => c.CodigoSeguimiento == "ENV-AJENO");

        var unico = Assert.Single(acotados);
        Assert.Equal("ENV-2", unico.CodigoSeguimiento);
    }

    private sealed class FakeConflictoSincronizacionRepository : IConflictoSincronizacionRepository
    {
        private readonly List<(Guid OperadorId, ConflictoSincronizacionDto Dto)> _items = new();

        public void Agregar(Guid operadorId, ConflictoSincronizacionDto dto) => _items.Add((operadorId, dto));

        public Task<IReadOnlyList<ConflictoSincronizacionDto>> ListarRecientesPorOperadorAsync(
            Guid operadorId, int maximo, CancellationToken cancellationToken = default)
        {
            var filtrados = _items
                .Where(x => x.OperadorId == operadorId)
                .OrderByDescending(x => x.Dto.Timestamp)
                .Take(maximo)
                .Select(x => x.Dto)
                .ToList();
            return Task.FromResult<IReadOnlyList<ConflictoSincronizacionDto>>(filtrados);
        }
    }
}
