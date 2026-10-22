using UltimaMilla.Application.Commands.Operador.ActualizarMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.CambiarEstadoMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.CrearMotivoNoEntrega;
using UltimaMilla.Application.Commands.Operador.PublicarReglaOperativa;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Queries.Operador;
using UltimaMilla.Application.Tests.Fakes;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Tests;

public class OperadorConfiguracionHandlerTests
{
    private readonly FakeReglaOperativaRepository _reglas = new();
    private readonly FakeMotivoNoEntregaRepository _motivos = new();
    private readonly FakeCurrentTenant _tenant = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUnitOfWork _uow = new();

    private PublicarReglaOperativaCommandHandler Publicar() => new(_reglas, _tenant, _clock, _uow);
    private CrearMotivoNoEntregaCommandHandler Crear() => new(_motivos, _tenant, _uow);
    private ActualizarMotivoNoEntregaCommandHandler Actualizar() => new(_motivos, _tenant, _uow);
    private CambiarEstadoMotivoNoEntregaCommandHandler CambiarEstado() => new(_motivos, _tenant, _uow);

    private static PublicarReglaOperativaCommand Regla(
        int intentos = 3, string? tipo = "Firma", string? politica = "Devolver al comercio",
        TimeSpan? entreIntentos = null, TimeSpan? estandar = null, TimeSpan? urgente = null, int paradas = 50) =>
        new(intentos, entreIntentos ?? TimeSpan.FromHours(4), tipo, politica,
            estandar ?? TimeSpan.FromHours(48), urgente ?? TimeSpan.FromHours(12), paradas);

    private MotivoNoEntrega AgregarMotivo(string codigo = "DIRECCION_ERRONEA", Guid? operadorId = null, bool interno = false)
    {
        var motivo = new MotivoNoEntrega(Guid.NewGuid(), operadorId ?? _tenant.OperadorId, codigo, "Descripción", interno);
        _motivos.Motivos.Add(motivo);
        return motivo;
    }

    // ---------- UC-04 ----------

    [Fact]
    public async Task Publicar_PrimeraVersion_QuedaVigenteConVersionUno()
    {
        var dto = await Publicar().Handle(Regla(), default);

        Assert.Equal(1, dto.Version);
        Assert.Equal(_clock.UtcNow, dto.VigenteDesde);
        Assert.Null(dto.VigenteHasta);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task Publicar_ConVersionPrevia_ArchivaLaAnteriorYPublicaLaNueva()
    {
        await Publicar().Handle(Regla(), default);
        _clock.UtcNow = _clock.UtcNow.AddDays(1);

        var dto = await Publicar().Handle(Regla(intentos: 5), default);

        var anterior = _reglas.Reglas.Single(r => r.Version == 1);
        Assert.Equal(_clock.UtcNow, anterior.VigenteHasta);
        Assert.Equal(2, dto.Version);
        Assert.Equal(5, dto.MaxIntentosEntrega);
        Assert.Single(_reglas.Reglas, r => r.VigenteHasta == null);
    }

    [Theory]
    [InlineData("firma", "Firma")]
    [InlineData(" COMPLETA ", "Completa")]
    public async Task Publicar_TipoPruebaSeNormalizaAlValorCanonico(string entrada, string esperado)
    {
        var dto = await Publicar().Handle(Regla(tipo: entrada), default);
        Assert.Equal(esperado, dto.TipoPruebaEntregaExigida);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Publicar_MaxIntentosNoPositivo_LanzaDomainValidation(int intentos) =>
        await AssertRechazaSinArchivar(Regla(intentos: intentos));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Publicar_MaxParadasNoPositivo_LanzaDomainValidation(int paradas) =>
        await AssertRechazaSinArchivar(Regla(paradas: paradas));

    [Fact]
    public async Task Publicar_PlazoEntreIntentosNegativo_LanzaDomainValidation() =>
        await AssertRechazaSinArchivar(Regla(entreIntentos: TimeSpan.FromHours(-1)));

    [Fact]
    public async Task Publicar_PlazoEstandarNegativo_LanzaDomainValidation() =>
        await AssertRechazaSinArchivar(Regla(estandar: TimeSpan.FromMinutes(-1)));

    [Fact]
    public async Task Publicar_PlazoUrgenteNegativo_LanzaDomainValidation() =>
        await AssertRechazaSinArchivar(Regla(urgente: TimeSpan.FromMinutes(-1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Huella")]
    public async Task Publicar_TipoPruebaVacioONoPermitido_LanzaDomainValidation(string? tipo) =>
        await AssertRechazaSinArchivar(Regla(tipo: tipo));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Publicar_PoliticaDevolucionVaciaONula_LanzaDomainValidation(string? politica) =>
        await AssertRechazaSinArchivar(Regla(politica: politica));

    [Fact]
    public async Task Publicar_ComandoNulo_LanzaArgumentNull() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Publicar().Handle(null!, default));

    [Fact]
    public async Task Publicar_NoTocaVersionesDeOtroOperador()
    {
        var otro = Guid.NewGuid();
        _reglas.Reglas.Add(new ReglaOperativa(Guid.NewGuid(), otro, 7, _clock.UtcNow.AddDays(-3), 3, TimeSpan.FromHours(1), "Firma", "x", TimeSpan.FromHours(1), TimeSpan.FromHours(1)));

        var dto = await Publicar().Handle(Regla(), default);

        Assert.Equal(1, dto.Version);
        Assert.Null(_reglas.Reglas.Single(r => r.OperadorId == otro).VigenteHasta);
    }

    private async Task AssertRechazaSinArchivar(PublicarReglaOperativaCommand comando)
    {
        var previa = await Publicar().Handle(Regla(), default);
        var confirmaciones = _uow.Confirmaciones;

        await Assert.ThrowsAsync<DomainValidationException>(() => Publicar().Handle(comando, default));

        Assert.Single(_reglas.Reglas);
        Assert.Null(_reglas.Reglas.Single(r => r.Id == previa.Id).VigenteHasta);
        Assert.Equal(confirmaciones, _uow.Confirmaciones);
    }

    [Fact]
    public async Task ObtenerVigente_SinReglas_DevuelveNull()
    {
        var h = new ObtenerReglaOperativaVigenteQueryHandler(_reglas, _tenant);
        Assert.Null(await h.Handle(new ObtenerReglaOperativaVigenteQuery(), default));
    }

    [Fact]
    public async Task Listar_DevuelveSoloLasDelOperadorMasRecientePrimero()
    {
        await Publicar().Handle(Regla(), default);
        await Publicar().Handle(Regla(), default);
        _reglas.Reglas.Add(new ReglaOperativa(Guid.NewGuid(), Guid.NewGuid(), 1, _clock.UtcNow, 3, TimeSpan.Zero, "Firma", "x", TimeSpan.Zero, TimeSpan.Zero));

        var lista = await new ListarReglasOperativasQueryHandler(_reglas, _tenant).Handle(new ListarReglasOperativasQuery(), default);

        Assert.Equal([2, 1], lista.Select(r => r.Version));
    }

    [Fact]
    public async Task ObtenerRegla_IdVacio_LanzaDomainValidation() =>
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new ObtenerReglaOperativaQueryHandler(_reglas, _tenant).Handle(new ObtenerReglaOperativaQuery(Guid.Empty), default));

    [Fact]
    public async Task ObtenerRegla_Inexistente_LanzaNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ObtenerReglaOperativaQueryHandler(_reglas, _tenant).Handle(new ObtenerReglaOperativaQuery(Guid.NewGuid()), default));

    [Fact]
    public async Task ObtenerRegla_DeOtroOperador_LanzaUnauthorized()
    {
        var ajena = new ReglaOperativa(Guid.NewGuid(), Guid.NewGuid(), 1, _clock.UtcNow, 3, TimeSpan.Zero, "Firma", "x", TimeSpan.Zero, TimeSpan.Zero);
        _reglas.Reglas.Add(ajena);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new ObtenerReglaOperativaQueryHandler(_reglas, _tenant).Handle(new ObtenerReglaOperativaQuery(ajena.Id), default));
    }

    // ---------- UC-05 ----------

    [Fact]
    public async Task CrearMotivo_Valido_QuedaActivoYConCodigoNormalizado()
    {
        var dto = await Crear().Handle(new CrearMotivoNoEntregaCommand(" direccion_erronea ", " Dirección errónea ", true), default);

        Assert.Equal("DIRECCION_ERRONEA", dto.Codigo);
        Assert.Equal("Dirección errónea", dto.Descripcion);
        Assert.True(dto.Activo);
        Assert.True(dto.EsInterno);
        Assert.Equal(_tenant.OperadorId, Assert.Single(_motivos.Motivos).OperadorId);
        Assert.Equal(1, _uow.Confirmaciones);
    }

    [Fact]
    public async Task CrearMotivo_CodigoDuplicadoEnElOperador_LanzaDomainValidation()
    {
        AgregarMotivo("CLIENTE_AUSENTE");

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Crear().Handle(new CrearMotivoNoEntregaCommand("cliente_ausente", "Otra descripción"), default));

        Assert.Single(_motivos.Motivos);
        Assert.Equal(0, _uow.Confirmaciones);
    }

    [Fact]
    public async Task CrearMotivo_MismoCodigoEnOtroOperador_Permitido()
    {
        AgregarMotivo("CLIENTE_AUSENTE", Guid.NewGuid());

        await Crear().Handle(new CrearMotivoNoEntregaCommand("CLIENTE_AUSENTE", "Cliente ausente"), default);

        Assert.Equal(2, _motivos.Motivos.Count);
    }

    [Theory]
    [InlineData(null, "desc")]
    [InlineData("", "desc")]
    [InlineData("   ", "desc")]
    [InlineData("COD", null)]
    [InlineData("COD", "")]
    [InlineData("COD", "   ")]
    public async Task CrearMotivo_CodigoODescripcionVacios_LanzaDomainValidation(string? codigo, string? descripcion)
    {
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Crear().Handle(new CrearMotivoNoEntregaCommand(codigo, descripcion), default));

        Assert.Empty(_motivos.Motivos);
    }

    [Fact]
    public async Task CrearMotivo_ComandoNulo_LanzaArgumentNull() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Crear().Handle(null!, default));

    [Fact]
    public async Task ActualizarMotivo_Valido_CambiaDescripcion()
    {
        var motivo = AgregarMotivo();

        var dto = await Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(motivo.Id, "  Nueva  "), default);

        Assert.Equal("Nueva", dto.Descripcion);
        Assert.Equal("DIRECCION_ERRONEA", dto.Codigo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ActualizarMotivo_DescripcionVacia_LanzaDomainValidation(string? descripcion)
    {
        var motivo = AgregarMotivo();

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(motivo.Id, descripcion), default));

        Assert.Equal("Descripción", motivo.Descripcion);
    }

    [Fact]
    public async Task ActualizarMotivo_IdVacio_LanzaDomainValidation() =>
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(Guid.Empty, "x"), default));

    [Fact]
    public async Task ActualizarMotivo_Inexistente_LanzaNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(Guid.NewGuid(), "x"), default));

    [Fact]
    public async Task ActualizarMotivo_DeOtroOperador_LanzaUnauthorized()
    {
        var ajeno = AgregarMotivo("AJENO", Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(ajeno.Id, "x"), default));

        Assert.Equal("Descripción", ajeno.Descripcion);
    }

    [Fact]
    public async Task CambiarEstado_DesactivarYReactivar_ConservaElMotivo()
    {
        var motivo = AgregarMotivo();

        var desactivado = await CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(motivo.Id, false), default);
        Assert.False(desactivado.Activo);

        var activado = await CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(motivo.Id, true), default);
        Assert.True(activado.Activo);
        Assert.Single(_motivos.Motivos);
    }

    [Fact]
    public async Task CambiarEstado_Idempotente_NoFalla()
    {
        var motivo = AgregarMotivo();

        await CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(motivo.Id, false), default);
        var segunda = await CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(motivo.Id, false), default);

        Assert.False(segunda.Activo);
    }

    [Fact]
    public async Task CambiarEstado_IdVacio_LanzaDomainValidation() =>
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(Guid.Empty, true), default));

    [Fact]
    public async Task CambiarEstado_Inexistente_LanzaNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(Guid.NewGuid(), true), default));

    [Fact]
    public async Task CambiarEstado_DeOtroOperador_LanzaUnauthorized()
    {
        var ajeno = AgregarMotivo("AJENO", Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CambiarEstado().Handle(new CambiarEstadoMotivoNoEntregaCommand(ajeno.Id, false), default));

        Assert.True(ajeno.Activo);
    }

    [Fact]
    public async Task ListarMotivos_DevuelveSoloLosDelOperadorIncluyendoInactivos()
    {
        AgregarMotivo("A").Desactivar();
        AgregarMotivo("B");
        AgregarMotivo("C", Guid.NewGuid());

        var lista = await new ListarMotivosNoEntregaQueryHandler(_motivos, _tenant).Handle(new ListarMotivosNoEntregaQuery(), default);

        Assert.Equal(["A", "B"], lista.Select(m => m.Codigo));
    }

    [Fact]
    public async Task ListarMotivos_ConFiltroActivos_FiltraCorrectamente()
    {
        AgregarMotivo("ACTIVO");
        AgregarMotivo("INACTIVO").Desactivar();

        var soloActivos = await new ListarMotivosNoEntregaQueryHandler(_motivos, _tenant).Handle(new ListarMotivosNoEntregaQuery(SoloActivos: true), default);
        var soloInactivos = await new ListarMotivosNoEntregaQueryHandler(_motivos, _tenant).Handle(new ListarMotivosNoEntregaQuery(SoloActivos: false), default);

        Assert.Equal(["ACTIVO"], soloActivos.Select(m => m.Codigo));
        Assert.Equal(["INACTIVO"], soloInactivos.Select(m => m.Codigo));
    }

    [Fact]
    public async Task ListarMotivos_ConFiltroBusqueda_FiltraPorCodigoODescripcion()
    {
        AgregarMotivo("DIRECCION_ERRONEA");
        AgregarMotivo("CLIENTE_AUSENTE");

        var porCodigo = await new ListarMotivosNoEntregaQueryHandler(_motivos, _tenant).Handle(new ListarMotivosNoEntregaQuery(Busqueda: "direccion"), default);
        Assert.Equal(["DIRECCION_ERRONEA"], porCodigo.Select(m => m.Codigo));
    }

    [Fact]
    public async Task ObtenerResumen_CalculaMetricasCorrectas()
    {
        AgregarMotivo("ACTIVO_MOVIL");
        AgregarMotivo("INTERNO", interno: true);
        AgregarMotivo("INACTIVO").Desactivar();

        var resumen = await new ObtenerResumenMotivosNoEntregaQueryHandler(_motivos, _tenant, _clock).Handle(new ObtenerResumenMotivosNoEntregaQuery(), default);

        Assert.Equal(3, resumen.TotalMotivos);
        Assert.Equal(1, resumen.MotivosActivosParaMovil);
        Assert.Equal(1, resumen.MotivosInactivos);
        Assert.Equal(_clock.UtcNow, resumen.UltimaSincronizacionApp);
    }

    [Fact]
    public async Task ListarParaRepartidor_ExcluyeInactivosEInternos()
    {
        AgregarMotivo("INACTIVO").Desactivar();
        AgregarMotivo("INTERNO", interno: true);
        AgregarMotivo("VISIBLE");

        var lista = await new ListarMotivosParaRepartidorQueryHandler(_motivos, _tenant).Handle(new ListarMotivosParaRepartidorQuery(), default);

        Assert.Equal("VISIBLE", Assert.Single(lista).Codigo);
    }

    [Fact]
    public async Task ObtenerMotivo_IdVacioInexistenteOAjeno_LanzaExcepcionTipada()
    {
        var h = new ObtenerMotivoNoEntregaQueryHandler(_motivos, _tenant);
        var ajeno = AgregarMotivo("AJENO", Guid.NewGuid());

        await Assert.ThrowsAsync<DomainValidationException>(() => h.Handle(new ObtenerMotivoNoEntregaQuery(Guid.Empty), default));
        await Assert.ThrowsAsync<NotFoundException>(() => h.Handle(new ObtenerMotivoNoEntregaQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => h.Handle(new ObtenerMotivoNoEntregaQuery(ajeno.Id), default));
    }

    [Fact]
    public async Task Publicar_TipoPruebaCompuesto_NormalizaACompleta()
    {
        var dto = await Publicar().Handle(Regla(tipo: "Firma digital y Foto del paquete en puerta"), default);
        Assert.Equal("Completa", dto.TipoPruebaEntregaExigida);
    }

    [Fact]
    public async Task ActualizarMotivo_ConCambioDeEstado_ActualizaDescripcionYActivo()
    {
        var m = AgregarMotivo("ORIGINAL");
        var dto = await Actualizar().Handle(new ActualizarMotivoNoEntregaCommand(m.Id, "Nueva descripción", Activo: false), default);

        Assert.Equal("Nueva descripción", dto.Descripcion);
        Assert.False(dto.Activo);
        Assert.False(m.Activo);
    }

    // ---------- Simulación (UC-04) ----------

    [Fact]
    public async Task Simular_SinReglaVigente_DevuelveNoExitoso()
    {
        var h = new SimularReglaOperativaQueryHandler(_reglas, _tenant, _clock);
        var res = await h.Handle(new SimularReglaOperativaQuery("Estándar", _clock.UtcNow, 0, null), default);

        Assert.False(res.Exitoso);
        Assert.Contains("No hay versión", res.AccionSugerida);
    }

    [Fact]
    public async Task Simular_PrimerIntento_HabilitadoParaEntregaDentroDeSla()
    {
        await Publicar().Handle(Regla(intentos: 3, estandar: TimeSpan.FromHours(48)), default);
        var h = new SimularReglaOperativaQueryHandler(_reglas, _tenant, _clock);

        var res = await h.Handle(new SimularReglaOperativaQuery("Estándar", _clock.UtcNow.AddHours(-10), 0, null), default);

        Assert.True(res.Exitoso);
        Assert.True(res.PuedeReintentarAhora);
        Assert.False(res.SuperaMaximoIntentos);
        Assert.False(res.SlaVencido);
        Assert.Equal(3, res.IntentosRestantes);
    }

    [Fact]
    public async Task Simular_IntentoFallidoReciente_EnEsperaReglamentaria()
    {
        await Publicar().Handle(Regla(intentos: 3, entreIntentos: TimeSpan.FromHours(24)), default);
        var h = new SimularReglaOperativaQueryHandler(_reglas, _tenant, _clock);

        var res = await h.Handle(new SimularReglaOperativaQuery("Estándar", _clock.UtcNow.AddHours(-10), 1, _clock.UtcNow.AddHours(-4)), default);

        Assert.True(res.Exitoso);
        Assert.False(res.PuedeReintentarAhora);
        Assert.Equal(2, res.IntentosRestantes);
        Assert.NotNull(res.ProximaFechaReintentoPermitida);
    }

    [Fact]
    public async Task Simular_AlcanzoMaximoIntentos_ActivaPoliticaDevolucion()
    {
        await Publicar().Handle(Regla(intentos: 3), default);
        var h = new SimularReglaOperativaQueryHandler(_reglas, _tenant, _clock);

        var res = await h.Handle(new SimularReglaOperativaQuery("Estándar", _clock.UtcNow.AddHours(-40), 3, _clock.UtcNow.AddHours(-10)), default);

        Assert.True(res.Exitoso);
        Assert.True(res.SuperaMaximoIntentos);
        Assert.False(res.PuedeReintentarAhora);
        Assert.Equal(0, res.IntentosRestantes);
        Assert.Contains("política de devolución", res.AccionSugerida);
    }

    [Fact]
    public async Task Simular_ModalidadUrgente_AplicaPlazoUrgente()
    {
        await Publicar().Handle(Regla(estandar: TimeSpan.FromHours(48), urgente: TimeSpan.FromHours(12)), default);
        var h = new SimularReglaOperativaQueryHandler(_reglas, _tenant, _clock);

        var fechaDespacho = _clock.UtcNow.AddHours(-15);
        var res = await h.Handle(new SimularReglaOperativaQuery("Express Mismo Día", fechaDespacho, 0, null), default);

        Assert.True(res.SlaVencido);
        Assert.Equal(fechaDespacho.AddHours(12), res.FechaLimiteSla);
    }
}