using UltimaMilla.Application.Commands.Mensajeria.DescartarAviso;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Application.Queries.Mensajeria.ListarAvisosEnColaDeFallidos;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Seguimiento;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

/// <summary>
/// Letra 6.9: los avisos a comercios tienen reintentos con espera creciente y cola de fallidos. Un aviso cuyos reintentos se agotaron
/// (por ejemplo, el comercio responde 500 cuatro veces) aparece en la cola de fallidos del administrador, que puede reenviarlo o descartarlo.
/// </summary>
public class ColaDeFallidosDeAvisosTests
{
    private readonly FakeRepoAvisos _repo = new();
    private readonly FakeUnitOfWork _uow = new();

    private AvisoComercio Aviso(string estado)
    {
        var aviso = new AvisoComercio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{\"evento\":\"EnvioPublicado\"}", "firma");
        switch (estado)
        {
            case "Agotado": aviso.MarcarAgotado("HTTP 500 Internal Server Error"); break;
            case "Fallido": aviso.MarcarFallido("HTTP 502 Bad Gateway"); break;
            case "Reintentando": aviso.RegistrarFallo("HTTP 500 Internal Server Error", DateTime.UtcNow.AddMinutes(5)); break;
            case "Entregado": aviso.MarcarEntregado("HTTP 200 OK"); break;
        }

        _repo.Avisos.Add(aviso);
        return aviso;
    }

    private Task<IReadOnlyList<AvisoEnColaDeFallidosDto>> Listar(Guid? id = null) =>
        new ListarAvisosEnColaDeFallidosQueryHandler(_repo).Handle(new ListarAvisosEnColaDeFallidosQuery(id), CancellationToken.None);

    [Fact]
    public async Task LaCola_IncluyeLosAvisosAgotadosYFallidos_PeroNoLosQueSiguenReintentandoNiLosEntregados()
    {
        var agotado = Aviso("Agotado");
        var fallido = Aviso("Fallido");
        Aviso("Reintentando");
        Aviso("Entregado");
        Aviso("Pendiente");

        var cola = await Listar();

        Assert.Equal(new[] { agotado.Id, fallido.Id }.OrderBy(i => i), cola.Select(a => a.Id).OrderBy(i => i));
        Assert.All(cola, a => Assert.Contains("HTTP 5", a.UltimaRespuestaHttp));
    }

    [Fact]
    public async Task LaCola_VaciaSiNoHayAvisosFallidos()
    {
        Aviso("Entregado");
        Aviso("Reintentando");

        Assert.Empty(await Listar());
    }

    [Fact]
    public async Task PorId_DevuelveElAvisoSoloSiSigueEsperandoUnaDecision()
    {
        var agotado = Aviso("Agotado");
        var entregado = Aviso("Entregado");

        Assert.Equal(agotado.Id, Assert.Single(await Listar(agotado.Id)).Id);
        Assert.Empty(await Listar(entregado.Id));
        Assert.Empty(await Listar(Guid.NewGuid()));
    }

    [Fact]
    public async Task Descartar_SacaElAvisoDeLaColaYLoConservaConSuMotivo()
    {
        var agotado = Aviso("Agotado");
        var handler = new DescartarAvisoCommandHandler(_repo, _uow);

        await handler.Handle(new DescartarAvisoCommand(agotado.Id, "El comercio dio de baja la integración"), CancellationToken.None);

        Assert.Equal("Descartado", agotado.Estado);
        Assert.Contains("dio de baja la integración", agotado.UltimaRespuestaHttp);
        Assert.Equal(1, _uow.Confirmaciones);
        Assert.Empty(await Listar());
        Assert.Contains(_repo.Avisos, a => a.Id == agotado.Id); // no se borra: queda para auditoría
    }

    [Fact]
    public async Task Descartar_SinMotivo_NoCambiaNada()
    {
        var agotado = Aviso("Agotado");
        var handler = new DescartarAvisoCommandHandler(_repo, _uow);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(new DescartarAvisoCommand(agotado.Id, " "), CancellationToken.None));

        Assert.Equal("Agotado", agotado.Estado);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Descartar_UnAvisoQueTodaviaSeReintenta_NoEstaPermitido()
    {
        var reintentando = Aviso("Reintentando");
        var handler = new DescartarAvisoCommandHandler(_repo, _uow);

        await Assert.ThrowsAsync<DomainValidationException>(() => handler.Handle(new DescartarAvisoCommand(reintentando.Id, "motivo"), CancellationToken.None));

        Assert.Equal("Reintentando", reintentando.Estado);
    }

    [Fact]
    public async Task Descartar_UnAvisoInexistenteOAjeno_EsNoEncontrado()
    {
        var handler = new DescartarAvisoCommandHandler(_repo, _uow);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DescartarAvisoCommand(Guid.NewGuid(), "motivo"), CancellationToken.None));
    }

    private sealed class FakeRepoAvisos : IAvisoComercioRepository
    {
        public List<AvisoComercio> Avisos { get; } = new();

        public Task<List<AvisoComercio>> ListarHistorialAvisosAsync(Guid? comercioOperadorId = null, string? estado = null, int limite = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos
                .Where(a => string.IsNullOrWhiteSpace(estado) || a.Estado == estado)
                .OrderByDescending(a => a.Timestamp)
                .Take(limite)
                .ToList());

        public Task<AvisoComercio?> GetAvisoByIdAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Avisos.FirstOrDefault(a => a.Id == avisoId));

        public Task<List<SuscripcionAviso>> ListarSuscripcionesActivasAsync(Guid comercioOperadorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SuscripcionAviso?> GetSuscripcionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(AvisoComercio aviso, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<AvisoComercio>> ListarPendientesDeEntregaAsync(DateTime ahoraUtc, int maximo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<SuscripcionAviso>> ListarSuscripcionesAsync(Guid? comercioOperadorId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SuscripcionAviso?> GetSuscripcionParaEdicionAsync(Guid suscripcionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddSuscripcionAsync(SuscripcionAviso suscripcion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
