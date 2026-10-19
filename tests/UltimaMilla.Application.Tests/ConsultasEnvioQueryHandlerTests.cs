using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Envios.ObtenerDetalleEnvio;
using UltimaMilla.Application.Queries.Seguimiento.ConsultarSeguimientoPublico;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class ConsultasEnvioQueryHandlerTests
{
    private readonly FakeEnvioRepository _envios = new();
    private readonly FakeFranjaHorariaRepository _franjas = new();
    private readonly FakeCurrentTenant _tenant = new();

    private ConsultarSeguimientoPublicoQueryHandler HandlerSeguimiento() => new(_envios, _franjas);

    private ObtenerDetalleEnvioQueryHandler HandlerDetalle() => new(_envios, _tenant);

    private Envio CrearEnvio(Guid? operadorId = null, Guid? franja = null, string codigo = "TRK-SEGUIM001")
    {
        var envio = new Envio(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Estandar", codigo,
            Guid.NewGuid(), franja, null, "Rivera 10, Montevideo", "Ana Gomez", operadorId ?? _tenant.OperadorId);
        envio.AgregarBulto($"{codigo}-B01", 2m, 10m, 10m, 10m);
        _envios.Envios.Add(envio);
        return envio;
    }

    private static Envio LlevarAEnTransito(Envio envio)
    {
        var bulto = envio.Bultos.Single().Identificacion;
        envio.RecepcionarEnDeposito(new[] { bulto }, null, null, Guid.NewGuid());
        envio.AsignarRuta(Guid.NewGuid());
        envio.DespacharEnRuta(envio.HojaDeRutaId!.Value, Guid.NewGuid());
        return envio;
    }

    private static ReglaOperativa Regla(Guid operadorId) =>
        new(Guid.NewGuid(), operadorId, 1, DateTime.UtcNow.AddDays(-1), 3, TimeSpan.FromHours(24),
            "Firma", "Devolución", TimeSpan.FromHours(48), TimeSpan.FromHours(24));

    // ---- UC-33: seguimiento público ----

    [Fact]
    public async Task Seguimiento_EnvioExistente_DevuelveEstadoYLineaDeTiempo()
    {
        var envio = LlevarAEnTransito(CrearEnvio());

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK-SEGUIM001"), default);

        Assert.NotNull(resultado);
        Assert.Equal("TRK-SEGUIM001", resultado!.CodigoSeguimiento);
        Assert.Equal(EstadoEnvio.EnTransito, resultado.EstadoActual);
        Assert.False(resultado.PermiteReprogramacion);
        Assert.Equal(new[] { EstadoEnvio.Admitido, EstadoEnvio.EnDeposito, EstadoEnvio.AsignadoARuta, EstadoEnvio.EnTransito },
            resultado.HistorialEventos.Select(h => h.Estado));
        Assert.Equal("El envío salió a reparto", resultado.HistorialEventos.Last().DescripcionPublica);
        Assert.Equal(envio.Eventos.Max(e => e.Timestamp), resultado.FechaUltimaActualizacion);
    }

    [Fact]
    public async Task Seguimiento_NoExponeDatosPersonalesNiInternos()
    {
        var envio = CrearEnvio();
        envio.RecepcionarEnDeposito(new[] { "TRK-SEGUIM001-B01" }, null, "Observación interna reservada", Guid.NewGuid());

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("trk-seguim001"), default);

        var serializado = System.Text.Json.JsonSerializer.Serialize(resultado);
        Assert.DoesNotContain("Ana Gomez", serializado);
        Assert.DoesNotContain("Rivera", serializado);
        Assert.DoesNotContain(envio.Id.ToString(), serializado);
        Assert.DoesNotContain("Observación interna", serializado);
        Assert.DoesNotContain("Backoffice", serializado);
    }

    [Fact]
    public async Task Seguimiento_OcultaEventosQueNoCambianElEstado()
    {
        var envio = CrearEnvio();
        envio.RecepcionarEnDeposito(new[] { "TRK-SEGUIM001-B01" }, null, null, Guid.NewGuid());
        envio.ClasificarBultos(new[] { new ClasificacionBulto("TRK-SEGUIM001-B01", PrioridadBulto.Alta) }, Guid.NewGuid());

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK-SEGUIM001"), default);

        Assert.Equal(2, resultado!.HistorialEventos.Count);
        Assert.DoesNotContain(resultado.HistorialEventos, h => h.DescripcionPublica.Contains("lasific"));
    }

    [Fact]
    public async Task Seguimiento_CodigoConEspaciosYMinusculas_SeNormaliza()
    {
        CrearEnvio();

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("  trk-seguim001 "), default);

        Assert.NotNull(resultado);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("TRK-NO-EXISTE")]
    public async Task Seguimiento_CodigoInvalidoOInexistente_DevuelveNull(string? codigo)
    {
        CrearEnvio();

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery(codigo!), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Seguimiento_CodigoDemasiadoLargo_DevuelveNullSinConsultar()
    {
        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery(new string('A', 101)), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Seguimiento_CodigoConCaracteresEspeciales_DevuelveNull()
    {
        CrearEnvio();

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK' OR '1'='1"), default);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task Seguimiento_EnvioReprogramado_PermiteReprogramacion()
    {
        var envio = LlevarAEnTransito(CrearEnvio());
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), _tenant.OperadorId, "AUSENTE", "Ausente");
        envio.RegistrarIntentoFallido(Guid.NewGuid(), motivo, Regla(_tenant.OperadorId), "Nadie atendió", null, null, null, DateTime.UtcNow);

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK-SEGUIM001"), default);

        Assert.Equal(EstadoEnvio.Reprogramado, resultado!.EstadoActual);
        Assert.True(resultado.PermiteReprogramacion);
        Assert.Contains(resultado.HistorialEventos, h => h.DescripcionPublica == "Entrega reprogramada para un nuevo intento");
        Assert.DoesNotContain("Nadie atendió", System.Text.Json.JsonSerializer.Serialize(resultado));
    }

    [Fact]
    public async Task Seguimiento_ConFranjaComprometida_InformaLaVentanaEstimada()
    {
        var franja = new FranjaHoraria(Guid.NewGuid(), Guid.NewGuid(), DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(12, 30));
        _franjas.Franjas.Add(franja);
        CrearEnvio(franja: franja.Id);

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK-SEGUIM001"), default);

        Assert.Equal("09:00-12:30", resultado!.VentanaHorariaEstimada);
    }

    [Fact]
    public async Task Seguimiento_SinFranjaOFranjaInexistente_NoInformaVentana()
    {
        CrearEnvio(franja: Guid.NewGuid());

        var resultado = await HandlerSeguimiento().Handle(new ConsultarSeguimientoPublicoQuery("TRK-SEGUIM001"), default);

        Assert.Null(resultado!.VentanaHorariaEstimada);
    }

    [Fact]
    public async Task Seguimiento_ConsultaNula_LanzaArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerSeguimiento().Handle(null!, default));
    }

    // ---- Detalle para el Backoffice ----

    [Fact]
    public async Task Detalle_IncluyeEventosIntentosYPruebaDeEntrega()
    {
        var envio = LlevarAEnTransito(CrearEnvio());
        envio.RegistrarEntregaExitosa(Guid.NewGuid(), Regla(_tenant.OperadorId), "Juan Pérez", "1.234.567-8", "firma-base64", null, -34.9, -56.1, DateTime.UtcNow);

        var detalle = await HandlerDetalle().Handle(new ObtenerDetalleEnvioQuery(envio.Id), default);

        Assert.Equal(envio.Id, detalle.EnvioId);
        Assert.Equal(EstadoEnvio.Entregado, detalle.EstadoActual);
        Assert.Equal("Ana Gomez", detalle.NombreDestinatario);
        Assert.Equal(5, detalle.Eventos.Count);
        Assert.Equal("EntregaExitosa", detalle.Eventos.Last().TipoEvento);
        var intento = Assert.Single(detalle.Intentos);
        Assert.Equal("Exitoso", intento.Resultado);
        Assert.Equal("Juan Pérez", intento.Prueba!.NombreReceptor);
        Assert.Equal("1.234.567-8", intento.Prueba.DocumentoReceptor);
        Assert.True(intento.Prueba.TieneFirma);
        Assert.False(intento.Prueba.TieneFoto);
        Assert.Single(detalle.Bultos);
    }

    [Fact]
    public async Task Detalle_NoDevuelveElContenidoDeLaFirmaNiDeLaFoto()
    {
        var envio = LlevarAEnTransito(CrearEnvio());
        envio.RegistrarEntregaExitosa(Guid.NewGuid(), Regla(_tenant.OperadorId), "Juan", "1", "FIRMA-SECRETA-BASE64", "FOTO-BASE64", null, null, DateTime.UtcNow);

        var detalle = await HandlerDetalle().Handle(new ObtenerDetalleEnvioQuery(envio.Id), default);

        var serializado = System.Text.Json.JsonSerializer.Serialize(detalle);
        Assert.DoesNotContain("FIRMA-SECRETA-BASE64", serializado);
        Assert.DoesNotContain("FOTO-BASE64", serializado);
    }

    [Fact]
    public async Task Detalle_EnvioDeOtroOperador_LanzaUnauthorizedAccess()
    {
        var envio = CrearEnvio(operadorId: Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => HandlerDetalle().Handle(new ObtenerDetalleEnvioQuery(envio.Id), default));
    }

    [Fact]
    public async Task Detalle_EnvioInexistenteOIdVacio_LanzaExcepcionesTipadas()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => HandlerDetalle().Handle(new ObtenerDetalleEnvioQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<DomainValidationException>(() => HandlerDetalle().Handle(new ObtenerDetalleEnvioQuery(Guid.Empty), default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => HandlerDetalle().Handle(null!, default));
    }
}
