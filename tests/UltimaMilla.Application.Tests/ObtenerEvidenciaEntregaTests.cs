using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Envios.ObtenerEvidenciaEntrega;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ObtenerEvidenciaEntregaTests
{
    // 1x1 PNG real: lo que el handler debe devolver byte a byte
    private const string PngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeCurrentTenant _tenant = new();

    private ObtenerEvidenciaEntregaQueryHandler Handler() => new(_envios, _tenant);

    private static ReglaOperativa Regla(Guid operadorId) =>
        new(Guid.NewGuid(), operadorId, 1, DateTime.UtcNow.AddDays(-1), 3, TimeSpan.FromHours(24),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));

    private (Envio Envio, Guid IntentoId) EnvioEntregado(string firma, string? foto = null)
    {
        _tenant.ComercioOperadorId = null; // personal del operador
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-EVID001",
            Guid.NewGuid(), null, null, "Rivera 10, Montevideo", "Ana Gomez", _tenant.OperadorId);
        envio.AgregarBulto("TRK-EVID001-B01", 2m, 10m, 10m, 10m);
        envio.RecepcionarEnDeposito(new[] { "TRK-EVID001-B01" }, null, null, Guid.NewGuid());
        envio.AsignarRuta(Guid.NewGuid());
        envio.DespacharEnRuta(envio.HojaDeRutaId!.Value, Guid.NewGuid());
        var intento = envio.RegistrarEntregaExitosa(Guid.NewGuid(), Regla(_tenant.OperadorId), "Juan", "1", firma, foto, null, null, DateTime.UtcNow);
        _envios.Envios.Add(envio);
        return (envio, intento.Id);
    }

    [Fact]
    public async Task Firma_EnBase64Puro_SeDevuelveComoPng()
    {
        var (envio, intentoId) = EnvioEntregado(PngBase64);

        var r = await Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Firma), default);

        Assert.Equal("image/png", r.TipoContenido);
        Assert.Equal(Convert.FromBase64String(PngBase64), r.Contenido);
    }

    [Fact]
    public async Task Foto_ConDataUri_RespetaElTipoDeclarado()
    {
        var (envio, intentoId) = EnvioEntregado(PngBase64, "data:image/jpeg;base64," + PngBase64);

        var r = await Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Foto), default);

        Assert.Equal("image/jpeg", r.TipoContenido);
    }

    [Fact]
    public async Task DataUriConTipoNoImagen_NoSeSirve()
    {
        // Defensa contra servir HTML/SVG almacenado como "firma" (XSS almacenado)
        var (envio, intentoId) = EnvioEntregado("data:text/html;base64," + PngBase64);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Firma), default));
    }

    [Fact]
    public async Task EvidenciaInexistenteOBase64Invalido_EsNoEncontrada()
    {
        var (envio, intentoId) = EnvioEntregado("esto-no-es-base64!!");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Firma), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Foto), default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, Guid.NewGuid(), TipoEvidenciaEntrega.Firma), default));
    }

    [Fact]
    public async Task OtroOperador_EsDenegado_YOtroComercio_NoSeEntera()
    {
        var (envio, intentoId) = EnvioEntregado(PngBase64);

        _tenant.ComercioOperadorId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Firma), default));

        _tenant.ComercioOperadorId = envio.ComercioOperadorId; // el dueño sí la ve
        Assert.NotEmpty((await Handler().Handle(new ObtenerEvidenciaEntregaQuery(envio.Id, intentoId, TipoEvidenciaEntrega.Firma), default)).Contenido);

        var ajeno = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", "TRK-EVID002",
            Guid.NewGuid(), null, null, "Rivera 10, Montevideo", "Ana Gomez", Guid.NewGuid());
        _envios.Envios.Add(ajeno);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(ajeno.Id, Guid.NewGuid(), TipoEvidenciaEntrega.Firma), default));
    }

    [Fact]
    public async Task IdentificadoresVacios_SonInvalidos()
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Handler().Handle(new ObtenerEvidenciaEntregaQuery(Guid.Empty, Guid.NewGuid(), TipoEvidenciaEntrega.Firma), default));
    }
}
