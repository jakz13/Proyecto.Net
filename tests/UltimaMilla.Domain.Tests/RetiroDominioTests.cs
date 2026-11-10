using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Tests;

/// <summary>Retiro en domicilio (cambio de requerimientos 2.2): ciclo de vida, evidencia, conflictos offline y hojas de ruta mixtas.</summary>
public class RetiroDominioTests
{
    private static readonly Guid Operador = Guid.NewGuid();
    private static readonly Guid Comercio = Guid.NewGuid();

    private static Retiro NuevoRetiro(int bultos = 2)
    {
        var retiro = new Retiro(Guid.NewGuid(), Operador, Comercio, "RET-ABC12345", "Ana Pérez", Guid.NewGuid(), "Av. Italia 1234, Montevideo", "Cambio de talle");
        for (var i = 0; i < bultos; i++)
            retiro.AgregarBulto($"Prenda {i + 1}", 1.5m, 30, 40, 10);
        return retiro;
    }

    private static Retiro EnRuta(int bultos = 2)
    {
        var retiro = NuevoRetiro(bultos);
        retiro.AsignarARuta(Guid.NewGuid());
        return retiro;
    }

    private static string[] Codigos(Retiro retiro) => retiro.Bultos.Select(b => b.Identificacion).ToArray();

    private static void Retirar(Retiro retiro) =>
        retiro.RegistrarRetiro(Codigos(retiro), "foto", "firma", "Ana Pérez", "1.234.567-8", -34.9, -56.1, DateTime.UtcNow);

    // ---------- Alta ----------

    [Fact]
    public void AlSolicitarse_QuedaSolicitadoConSuEventoInicial()
    {
        var retiro = NuevoRetiro();

        Assert.Equal(EstadoRetiro.Solicitado, retiro.EstadoActual);
        var evento = Assert.Single(retiro.Eventos);
        Assert.Equal("SolicitudRetiro", evento.TipoEvento);
        Assert.Null(evento.EstadoAnterior);
        Assert.Equal(EstadoRetiro.Solicitado, evento.EstadoNuevo);
    }

    [Fact]
    public void LosBultos_SeIdentificanConElCodigoDelRetiro()
    {
        var retiro = NuevoRetiro(3);

        Assert.Equal(new[] { "RET-ABC12345-B01", "RET-ABC12345-B02", "RET-ABC12345-B03" }, Codigos(retiro));
        Assert.Equal(4.5m, retiro.PesoTotal);
    }

    [Theory]
    [InlineData("", "Av. Italia 1", "Motivo")]
    [InlineData("Ana", "", "Motivo")]
    [InlineData("Ana", "Av. Italia 1", "")]
    public void NoSePuedeSolicitarSinContactoDomicilioNiMotivo(string contacto, string direccion, string motivo)
    {
        Assert.Throws<DomainValidationException>(() =>
            new Retiro(Guid.NewGuid(), Operador, Comercio, "RET-X", contacto, Guid.NewGuid(), direccion, motivo));
    }

    [Fact]
    public void ElRetiroPerteneceAUnOperadorYAUnComercio()
    {
        Assert.Throws<DomainValidationException>(() =>
            new Retiro(Guid.NewGuid(), Guid.Empty, Comercio, "RET-X", "Ana", Guid.NewGuid(), "Calle 1", "Motivo"));
        Assert.Throws<DomainValidationException>(() =>
            new Retiro(Guid.NewGuid(), Operador, Guid.Empty, "RET-X", "Ana", Guid.NewGuid(), "Calle 1", "Motivo"));
    }

    [Fact]
    public void UnBultoSinPesoODimensionesNoEsValido()
    {
        var retiro = NuevoRetiro(0);

        Assert.Throws<DomainValidationException>(() => retiro.AgregarBulto("x", 0, 10, 10, 10));
        Assert.Throws<DomainValidationException>(() => retiro.AgregarBulto("x", 1, 0, 10, 10));
    }

    [Fact]
    public void LaTarifaNoPuedeSerNegativa()
    {
        var retiro = NuevoRetiro();

        Assert.Throws<DomainValidationException>(() => retiro.AplicarTarifa(-1, Guid.NewGuid()));
        retiro.AplicarTarifa(120m, Guid.NewGuid());
        Assert.Equal(120m, retiro.TarifaCalculada);
    }

    // ---------- Máquina de estados declarativa ----------

    [Fact]
    public void LaTablaDeTransiciones_DefineTodosLosEstadosYSoloDosSonTerminales()
    {
        foreach (var estado in Enum.GetValues<EstadoRetiro>())
            Assert.True(MaquinaEstadosRetiro.TablaTransiciones.ContainsKey(estado), $"Falta {estado} en la tabla");

        var terminales = Enum.GetValues<EstadoRetiro>().Where(MaquinaEstadosRetiro.EsEstadoTerminal).ToList();
        Assert.Equal(new[] { EstadoRetiro.EntregadoAlComercio, EstadoRetiro.Cancelado }, terminales.OrderBy(e => e));
    }

    [Theory]
    [InlineData(EstadoRetiro.Solicitado, EstadoRetiro.AsignadoARuta, true)]
    [InlineData(EstadoRetiro.AsignadoARuta, EstadoRetiro.Retirado, true)]
    [InlineData(EstadoRetiro.AsignadoARuta, EstadoRetiro.NoRetirado, true)]
    [InlineData(EstadoRetiro.NoRetirado, EstadoRetiro.Solicitado, true)]
    [InlineData(EstadoRetiro.Retirado, EstadoRetiro.EnDeposito, true)]
    [InlineData(EstadoRetiro.EnDeposito, EstadoRetiro.EntregadoAlComercio, true)]
    [InlineData(EstadoRetiro.Solicitado, EstadoRetiro.Retirado, false)]
    [InlineData(EstadoRetiro.Solicitado, EstadoRetiro.EntregadoAlComercio, false)]
    [InlineData(EstadoRetiro.Retirado, EstadoRetiro.Solicitado, false)]
    [InlineData(EstadoRetiro.Retirado, EstadoRetiro.Cancelado, false)]
    [InlineData(EstadoRetiro.EntregadoAlComercio, EstadoRetiro.Retirado, false)]
    [InlineData(EstadoRetiro.Cancelado, EstadoRetiro.Solicitado, false)]
    public void LasTransiciones_SeValidanContraLaTablaDeclarativa(EstadoRetiro origen, EstadoRetiro destino, bool valida)
    {
        Assert.Equal(valida, MaquinaEstadosRetiro.EsTransicionValida(origen, destino));
    }

    [Fact]
    public void UnEstadoSoloCambiaPorLosMetodosDeLaEntidad_YUnaTransicionInvalidaSeRechaza()
    {
        var retiro = NuevoRetiro();

        // Solicitado no puede ir directo a recibido en depósito
        Assert.Throws<DomainValidationException>(() => retiro.RecibirEnDeposito());
        Assert.Throws<DomainValidationException>(() => retiro.EntregarAlComercio("x"));
        Assert.Equal(EstadoRetiro.Solicitado, retiro.EstadoActual);
    }

    // ---------- Flujo completo e historial ----------

    [Fact]
    public void ElFlujoCompleto_DejaUnHistorialInmutableEnElOrdenEnQueOcurrio()
    {
        var retiro = EnRuta();
        Retirar(retiro);
        retiro.RecibirEnDeposito();
        retiro.EntregarAlComercio("Encargada");

        Assert.Equal(EstadoRetiro.EntregadoAlComercio, retiro.EstadoActual);
        Assert.Equal(
            new[] { "SolicitudRetiro", "AsignacionRuta", "RetiroRealizado", "RecepcionEnDeposito", "EntregaAlComercio" },
            retiro.Eventos.Select(e => e.TipoEvento));

        // Cada evento recuerda de qué estado venía
        Assert.Equal(EstadoRetiro.Retirado, retiro.Eventos.Single(e => e.TipoEvento == "RecepcionEnDeposito").EstadoAnterior);
    }

    [Fact]
    public void UnRetiroTerminadoNoAdmiteMasCambios()
    {
        var retiro = EnRuta();
        Retirar(retiro);
        retiro.RecibirEnDeposito();
        retiro.EntregarAlComercio(null);

        Assert.Throws<DomainValidationException>(() => retiro.Cancelar("tarde"));
        Assert.Throws<DomainValidationException>(() => retiro.RecibirEnDeposito());
    }

    // ---------- Asignación y cancelación ----------

    [Fact]
    public void SoloUnRetiroSolicitadoConBultosPuedeAsignarseAUnaRuta()
    {
        Assert.NotNull(NuevoRetiro(0).ObtenerMotivoNoAsignable());

        var retiro = NuevoRetiro();
        Assert.Null(retiro.ObtenerMotivoNoAsignable());
        retiro.AsignarARuta(Guid.NewGuid());

        Assert.NotNull(retiro.ObtenerMotivoNoAsignable());
        Assert.Throws<DomainValidationException>(() => retiro.AsignarARuta(Guid.NewGuid()));
    }

    [Fact]
    public void LiberarDeRuta_LoDejaNuevamenteSolicitadoYSinRuta()
    {
        var retiro = EnRuta();

        retiro.LiberarDeRuta("El despachador lo sacó");

        Assert.Equal(EstadoRetiro.Solicitado, retiro.EstadoActual);
        Assert.Null(retiro.HojaDeRutaId);
    }

    [Fact]
    public void Cancelar_ExigeMotivoYLimpiaLaRuta()
    {
        var retiro = NuevoRetiro();

        Assert.Throws<DomainValidationException>(() => retiro.Cancelar(" "));
        retiro.Cancelar("El comprador desistió");

        Assert.Equal(EstadoRetiro.Cancelado, retiro.EstadoActual);
    }

    // ---------- Evidencia del retiro (RF 40) ----------

    [Fact]
    public void ElRetiro_ExigeFotografiaFirmaYNombreDeQuienEntrega()
    {
        var sinFoto = EnRuta();
        Assert.Throws<DomainValidationException>(() => sinFoto.RegistrarRetiro(Codigos(sinFoto), null, "firma", "Ana", null, null, null, DateTime.UtcNow));

        var sinFirma = EnRuta();
        Assert.Throws<DomainValidationException>(() => sinFirma.RegistrarRetiro(Codigos(sinFirma), "foto", " ", "Ana", null, null, null, DateTime.UtcNow));

        var sinNombre = EnRuta();
        Assert.Throws<DomainValidationException>(() => sinNombre.RegistrarRetiro(Codigos(sinNombre), "foto", "firma", "", null, null, null, DateTime.UtcNow));
    }

    [Fact]
    public void ElRetiro_ExigeEscanearTodosLosBultosYNingunoAjeno()
    {
        var retiro = EnRuta(2);

        var faltante = Assert.Throws<DomainValidationException>(() =>
            retiro.RegistrarRetiro(new[] { Codigos(retiro)[0] }, "foto", "firma", "Ana", null, null, null, DateTime.UtcNow));
        Assert.Contains("B02", faltante.Message);

        var ajeno = Assert.Throws<DomainValidationException>(() =>
            retiro.RegistrarRetiro(Codigos(retiro).Append("OTRO-B01"), "foto", "firma", "Ana", null, null, null, DateTime.UtcNow));
        Assert.Contains("OTRO-B01", ajeno.Message);

        Assert.Equal(EstadoRetiro.AsignadoARuta, retiro.EstadoActual);
    }

    [Fact]
    public void ElRetiroRealizado_GuardaLaEvidenciaYLaPosicionDelDispositivo()
    {
        var retiro = EnRuta();
        var instante = new DateTime(2026, 11, 9, 14, 30, 0, DateTimeKind.Utc);

        retiro.RegistrarRetiro(Codigos(retiro), "foto", "firma", "Ana Pérez", "1.234.567-8", -34.9, -56.1, instante);

        Assert.Equal(EstadoRetiro.Retirado, retiro.EstadoActual);
        Assert.Equal("Ana Pérez", retiro.NombreEntrega);
        Assert.Equal(instante, retiro.FechaRetiro);
        Assert.Equal(-34.9, retiro.LatitudRetiro);
        var evento = retiro.Eventos.Last();
        Assert.Equal(instante, evento.Timestamp);
        Assert.Equal(-56.1, evento.Longitud);
    }

    [Fact]
    public void SoloUnRetiroAsignadoAUnaRutaPuedeRetirarse()
    {
        var retiro = NuevoRetiro();

        Assert.Throws<DomainValidationException>(() => Retirar(retiro));
    }

    [Fact]
    public void ElRetiroNoRealizado_RequiereMotivoYPuedeReprogramarse()
    {
        var retiro = EnRuta();

        Assert.Throws<DomainValidationException>(() => retiro.RegistrarNoRetiro("", null, null, null, DateTime.UtcNow));
        retiro.RegistrarNoRetiro("ausente", "No había nadie", -34.9, -56.1, DateTime.UtcNow);

        Assert.Equal(EstadoRetiro.NoRetirado, retiro.EstadoActual);
        Assert.Equal("AUSENTE", retiro.MotivoNoRetiroCodigo);

        retiro.ReprogramarVisita();
        Assert.Equal(EstadoRetiro.Solicitado, retiro.EstadoActual);
        Assert.Null(retiro.MotivoNoRetiroCodigo);
        Assert.Null(retiro.HojaDeRutaId);
    }

    // ---------- Política de conflicto offline (RF 24) ----------

    [Fact]
    public void ConflictoOffline_SiElServidorLoSacoDeLaRuta_PrevaleceElRetiroFisico()
    {
        var retiro = EnRuta();
        retiro.LiberarDeRuta("Sacado mientras el repartidor no tenía conexión");
        var rutaDelDispositivo = Guid.NewGuid();

        retiro.RegistrarRetiro(Codigos(retiro), "foto", "firma", "Ana", null, null, null, DateTime.UtcNow,
            resolverConflicto: true, hojaDeRutaId: rutaDelDispositivo);

        Assert.Equal(EstadoRetiro.Retirado, retiro.EstadoActual);
        Assert.Equal(rutaDelDispositivo, retiro.HojaDeRutaId);
        Assert.Contains("Conflicto de sincronización resuelto", retiro.Eventos.Last().Payload);
    }

    [Fact]
    public void ConflictoOffline_SiElComercioLoCancelo_TambienPrevaleceLoOcurridoEnElDomicilio()
    {
        var retiro = NuevoRetiro();
        retiro.Cancelar("El comercio lo canceló");

        retiro.RegistrarNoRetiro("AUSENTE", null, null, null, DateTime.UtcNow, resolverConflicto: true);

        Assert.Equal(EstadoRetiro.NoRetirado, retiro.EstadoActual);
    }

    [Fact]
    public void SinLaPoliticaDeConflicto_UnRetiroSolicitadoNoPuedeRetirarse()
    {
        var retiro = NuevoRetiro();

        Assert.Throws<DomainValidationException>(() =>
            retiro.RegistrarRetiro(Codigos(retiro), "foto", "firma", "Ana", null, null, null, DateTime.UtcNow, resolverConflicto: false));
    }

    [Fact]
    public void ElConflicto_NoPuedeReabrirUnEstadoTerminalDeVerdad()
    {
        var retiro = EnRuta();
        Retirar(retiro);
        retiro.RecibirEnDeposito();
        retiro.EntregarAlComercio(null);

        Assert.Throws<DomainValidationException>(() =>
            retiro.RegistrarRetiro(Codigos(retiro), "foto", "firma", "Ana", null, null, null, DateTime.UtcNow, resolverConflicto: true));
    }

    [Fact]
    public void LasTransicionesPorConflicto_SonUnaTablaDeclarativaAcotada()
    {
        // Solo desde "solicitado" y "cancelado" y solo hacia los resultados físicos
        Assert.Equal(new[] { EstadoRetiro.Solicitado, EstadoRetiro.Cancelado }, MaquinaEstadosRetiro.TablaTransicionesPorConflicto.Keys.OrderBy(e => e));
        Assert.All(MaquinaEstadosRetiro.TablaTransicionesPorConflicto.Values,
            destinos => Assert.Equal(new[] { EstadoRetiro.Retirado, EstadoRetiro.NoRetirado }, destinos.OrderBy(e => e)));
    }

    // ---------- Capacidad a lo largo del recorrido (RF 39) ----------

    [Fact]
    public void ConSoloEntregas_LaCargaMaximaEsLaDelInicio()
    {
        var paradas = new[] { new ParadaCarga(false, 4, 1), new ParadaCarga(false, 3, 1), new ParadaCarga(false, 2, 1) };

        var resultado = PerfilCargaRuta.Evaluar(paradas, capacidadPeso: 9, capacidadVolumen: 10);

        Assert.True(resultado.Cabe);
        Assert.Equal(9, resultado.PicoPeso);
    }

    [Fact]
    public void ConSoloEntregas_UnaCargaInicialMayorALaCapacidadNoCabe()
    {
        var paradas = new[] { new ParadaCarga(false, 6, 1), new ParadaCarga(false, 6, 1) };

        var resultado = PerfilCargaRuta.Evaluar(paradas, capacidadPeso: 10, capacidadVolumen: 10);

        Assert.False(resultado.Cabe);
        Assert.Equal(0, resultado.PosicionQueExcede);
    }

    [Fact]
    public void LosBultosRetirados_OcupanLugarDesdeLaParadaEnQueSeSuben()
    {
        // Sale con 8 kg; entrega 6 (queda en 2); retira 7 (llega a 9): cabe en un vehículo de 10 kg aunque 8 + 7 = 15 > 10
        var paradas = new[] { new ParadaCarga(false, 6, 1), new ParadaCarga(true, 7, 1), new ParadaCarga(false, 2, 1) };

        var resultado = PerfilCargaRuta.Evaluar(paradas, capacidadPeso: 10, capacidadVolumen: 10);

        Assert.True(resultado.Cabe);
        Assert.Equal(9, resultado.PicoPeso);
    }

    [Fact]
    public void ElOrdenDeLasParadasImporta_RetirarAntesDeEntregarPuedeExcederLaCapacidad()
    {
        // Mismas paradas que el caso anterior, pero retirando primero: 8 + 7 = 15 kg a bordo, no cabe
        var paradas = new[] { new ParadaCarga(true, 7, 1), new ParadaCarga(false, 6, 1), new ParadaCarga(false, 2, 1) };

        var resultado = PerfilCargaRuta.Evaluar(paradas, capacidadPeso: 10, capacidadVolumen: 10);

        Assert.False(resultado.Cabe);
        Assert.Equal(1, resultado.PosicionQueExcede);
        Assert.Contains("parada 1", resultado.Motivo);
    }

    [Fact]
    public void LaCapacidadDeVolumenTambienSeValidaALoLargoDelRecorrido()
    {
        var paradas = new[] { new ParadaCarga(false, 1, 4), new ParadaCarga(true, 1, 4), new ParadaCarga(true, 1, 4) };

        var resultado = PerfilCargaRuta.Evaluar(paradas, capacidadPeso: 100, capacidadVolumen: 5);

        Assert.False(resultado.Cabe);
        Assert.Equal(3, resultado.PosicionQueExcede);
    }

    // ---------- Hoja de ruta mixta ----------

    private static HojaDeRuta RutaMixta(out Retiro retiro)
    {
        retiro = EnRuta();
        var ruta = new HojaDeRuta(Guid.NewGuid(), Operador, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParada(Guid.NewGuid(), 1, Guid.NewGuid());
        ruta.AgregarParadaRetiro(retiro.Id, 2, retiro.DireccionRetiroId);
        ruta.Liberar();
        return ruta;
    }

    [Fact]
    public void LaHojaDeRutaMixta_DistingueParadasDeEntregaYDeRetiro()
    {
        var ruta = RutaMixta(out var retiro);

        var paradaRetiro = Assert.Single(ruta.Paradas, p => p.EsRetiro);
        Assert.Equal(retiro.Id, paradaRetiro.RetiroId);
        Assert.Null(paradaRetiro.EnvioId);
        var paradaEntrega = Assert.Single(ruta.Paradas, p => !p.EsRetiro);
        Assert.NotNull(paradaEntrega.EnvioId);
        Assert.Equal(new[] { retiro.Id }, ruta.Paradas.IdsDeRetiros());
        Assert.Single(ruta.Paradas.IdsDeEnvios());
    }

    [Fact]
    public void UnaParadaDeRetiro_NoPuedeRegistrarUnaEntrega_NiUnaDeEntregaUnRetiro()
    {
        var ruta = RutaMixta(out _);
        var retiro = ruta.Paradas.Single(p => p.EsRetiro);
        var entrega = ruta.Paradas.Single(p => !p.EsRetiro);

        Assert.Throws<DomainValidationException>(() => entrega.RegistrarRetiroRealizado());
        retiro.RegistrarRetiroRealizado();
        Assert.Equal("Retirada", retiro.Estado);
    }

    [Fact]
    public void LaRuta_SeFinalizaCuandoSeCerraronTantoLasEntregasComoLosRetiros()
    {
        var ruta = RutaMixta(out _);
        var paradaEntrega = ruta.Paradas.Single(p => !p.EsRetiro);
        var paradaRetiro = ruta.Paradas.Single(p => p.EsRetiro);

        ruta.RegistrarRetiroRealizadoEnParada(paradaRetiro.Id);
        Assert.NotEqual("Finalizada", ruta.Estado);

        ruta.RegistrarEntregaExitosaEnParada(paradaEntrega.Id);
        Assert.Equal("Finalizada", ruta.Estado);
    }

    [Fact]
    public void UnRetiroSePuedeSacarDeLaRutaAntesDeLaSalida_PeroNoDespuesDeCerrarse()
    {
        var ruta = RutaMixta(out var retiro);

        ruta.RetirarRetiro(retiro.Id);

        Assert.Equal("Cancelada", ruta.Paradas.Single(p => p.EsRetiro).Estado);
        Assert.Throws<DomainValidationException>(() => ruta.RetirarRetiro(retiro.Id));
    }

    [Fact]
    public void UnaRutaSoloDeRetiros_PuedeDespacharseSinEscaneoDeCarga()
    {
        var retiro = EnRuta();
        var ruta = new HojaDeRuta(Guid.NewGuid(), Operador, DateOnly.FromDateTime(DateTime.UtcNow));
        ruta.AsignarRecursos(Guid.NewGuid(), Guid.NewGuid());
        ruta.AgregarParadaRetiro(retiro.Id, 1, retiro.DireccionRetiroId);
        ruta.Liberar();

        ruta.Despachar();

        Assert.Equal("Despachada", ruta.Estado);
    }

    [Fact]
    public void UnaRutaConEntregas_SigueExigiendoElEscaneoDeCargaParaDespachar()
    {
        var ruta = RutaMixta(out _);

        Assert.Throws<DomainValidationException>(() => ruta.Despachar());
    }
}
