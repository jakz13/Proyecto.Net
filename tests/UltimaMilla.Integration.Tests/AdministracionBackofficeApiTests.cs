using System.Net;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Frontend.Contracts.Modelos.Usuarios;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Administración del operador (comercios, usuarios, zonas, tarifas, reglas, motivos, identidad, flota y mensajería).
/// Se prueba con los clientes HTTP reales del frontend contra la API y un Postgres real: valida la integración de punta a punta.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class AdministracionBackofficeApiTests
{
    private readonly ApiFactory _factory;

    public AdministracionBackofficeApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    // ───────────────────────────── Comercios ─────────────────────────────

    [FactPostgres]
    public async Task Comercios_Ciclo_AltaListadoEdicionYBaja()
    {
        var api = new HttpComerciosApi(_factory.CreateClient());
        var rut = string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));

        var creado = await api.CrearComercioAsync(new CrearComercioDto
        {
            RazonSocial = "Ferretería Test S.A.", NombreFantasia = "FerreTest", Rut = rut,
            Email = "Contacto@FerreTest.com", Telefono = "099123456", Direccion = "Av. Siempre Viva 742"
        });
        Assert.True(creado.Activo);
        Assert.Equal("contacto@ferretest.com", creado.Email);
        Assert.False(string.IsNullOrEmpty(creado.SelloModificacion));

        var encontrado = await api.ListarComerciosAsync(new FiltroComerciosDto { TextoBusqueda = "ferretest" });
        Assert.Contains(encontrado.Elementos, c => c.Id == creado.Id);

        var duplicado = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearComercioAsync(new CrearComercioDto
        {
            RazonSocial = "Otra", NombreFantasia = "Otra", Rut = rut, Email = "x@x.com", Telefono = "099", Direccion = "Calle 123"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicado.CodigoEstado);

        var actualizado = await api.ActualizarComercioAsync(creado.Id, new ActualizarComercioDto
        {
            RazonSocial = "Ferretería Test S.R.L.", NombreFantasia = "FerreTest 2", Email = "nuevo@ferretest.com",
            Telefono = "098765432", Direccion = "Otra dirección 1", Activo = true, SelloModificacion = creado.SelloModificacion
        });
        Assert.Equal("FerreTest 2", actualizado.NombreFantasia);
        Assert.NotEqual(creado.SelloModificacion, actualizado.SelloModificacion);

        // Editar con el sello anterior simula a otra persona que ya modificó el comercio
        var conflicto = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ActualizarComercioAsync(creado.Id, new ActualizarComercioDto
        {
            RazonSocial = "X", NombreFantasia = "X", Email = "x@x.com", Telefono = "1", Direccion = "X", SelloModificacion = creado.SelloModificacion
        }));
        Assert.True(conflicto.EsConflictoConcurrencia);

        await api.CambiarEstadoComercioAsync(creado.Id, false);
        Assert.False((await api.ObtenerPorIdAsync(creado.Id))!.Activo);
        var soloActivos = await api.ListarComerciosAsync(new FiltroComerciosDto { TextoBusqueda = "ferretest", SoloActivos = true });
        Assert.DoesNotContain(soloActivos.Elementos, c => c.Id == creado.Id);

        Assert.Null(await api.ObtenerPorIdAsync(Guid.NewGuid()));
    }

    [FactPostgres]
    public async Task Comercios_ListadoPagina_RespetaTamanoYTotal()
    {
        var api = new HttpComerciosApi(_factory.CreateClient());

        var pagina = await api.ListarComerciosAsync(1, 1);

        Assert.Single(pagina.Elementos);
        Assert.True(pagina.TotalRegistros >= 1);
        Assert.Equal(1, pagina.TamanoPagina);
    }

    // ───────────────────────────── Usuarios ─────────────────────────────

    [FactPostgres]
    public async Task Usuarios_Ciclo_AltaPerfilEstadoYDuplicado()
    {
        var api = new HttpUsuariosApi(_factory.CreateClient());
        var perfiles = await api.ListarPerfilesAsync();
        Assert.True(perfiles.Count >= 5);
        var despachador = perfiles.Single(p => p.Nombre == "Despachador");
        var operario = perfiles.Single(p => p.Nombre == "Operario");
        var email = $"usuario.{Sufijo()}@ultimamilla.com.uy";

        var creado = await api.CrearUsuarioAsync(new CrearUsuarioDto
        {
            Nombre = "Usuario Prueba", Email = email, Password = "secreto123", PerfilId = despachador.Id
        });
        Assert.Equal("Despachador", creado.NombrePerfil);
        Assert.True(creado.Activo);

        var duplicado = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearUsuarioAsync(new CrearUsuarioDto
        {
            Nombre = "Otro", Email = email.ToUpperInvariant(), Password = "secreto123", PerfilId = despachador.Id
        }));
        Assert.Equal(HttpStatusCode.BadRequest, duplicado.CodigoEstado);

        await api.AsignarPerfilAsync(creado.Id, operario.Id);
        Assert.Equal("Operario", (await api.ObtenerPorIdAsync(creado.Id))!.NombrePerfil);

        await api.CambiarEstadoUsuarioAsync(creado.Id, false);
        var activos = await api.ListarUsuariosAsync(new FiltroUsuariosDto { TextoBusqueda = email, SoloActivos = true });
        Assert.Empty(activos.Elementos);

        var porPerfil = await api.ListarUsuariosAsync(new FiltroUsuariosDto { PerfilId = operario.Id, TextoBusqueda = email });
        Assert.Single(porPerfil.Elementos);

        var actualizado = await api.ActualizarUsuarioAsync(creado.Id, new ActualizarUsuarioDto
        {
            Nombre = "Usuario Renombrado", Email = email, PerfilId = despachador.Id, Activo = true
        });
        Assert.Equal("Usuario Renombrado", actualizado.Nombre);
        Assert.True(actualizado.Activo);
    }

    // ───────────────────────────── Zonas y franjas ─────────────────────────────

    [FactPostgres]
    public async Task Zonas_Ciclo_AltaConFranjasReemplazoYValidacion()
    {
        var api = new HttpZonasCoberturaApi(_factory.CreateClient());
        Assert.True((await api.ListarZonasAsync()).Count >= 3);

        var nombre = "Zona " + Sufijo();
        var creada = await api.CrearZonaAsync(new CrearZonaCoberturaDto
        {
            Nombre = nombre, PoligonoGeografico = "[[-34.9, -56.1], [-34.8, -56.1], [-34.8, -56.0]]", ColorHex = "#ff0000",
            FranjasHorarias =
            {
                new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(12, 0), CapacidadMaximaEnvios = 10 },
                new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(18, 0), CapacidadMaximaEnvios = 12 }
            }
        });
        Assert.Equal(2, creada.TotalFranjas);
        Assert.Equal("#ff0000", creada.ColorHex);

        var superpuestas = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearZonaAsync(new CrearZonaCoberturaDto
        {
            Nombre = "Zona rara " + Sufijo(), PoligonoGeografico = "[[0,0]]",
            FranjasHorarias =
            {
                new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Tuesday, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(12, 0) },
                new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Tuesday, HoraInicio = new TimeOnly(11, 0), HoraFin = new TimeOnly(13, 0) }
            }
        }));
        Assert.Contains("superponen", superpuestas.Message);

        var (esValido, mensaje) = await api.ValidarSuperposicionFranjasAsync(new[]
        {
            new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Friday, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(12, 0) },
            new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Saturday, HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(12, 0) }
        });
        Assert.True(esValido, mensaje);

        var actualizada = await api.ActualizarZonaAsync(creada.Id, new ActualizarZonaCoberturaDto
        {
            Nombre = nombre + " v2", PoligonoGeografico = "[[1,1],[2,2],[3,3]]", ColorHex = "#00ff00", Activa = true,
            FranjasHorarias = { new CrearFranjaHorariaDto { DiaSemana = DayOfWeek.Wednesday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(10, 0), CapacidadMaximaEnvios = 5 } }
        });
        Assert.Single(actualizada.FranjasHorarias);
        Assert.Equal(DayOfWeek.Wednesday, actualizada.FranjasHorarias[0].DiaSemana);

        var releida = await api.ObtenerPorIdAsync(creada.Id);
        Assert.Single(releida!.FranjasHorarias);
        Assert.Equal(nombre + " v2", releida.Nombre);

        await api.CambiarEstadoZonaAsync(creada.Id, false);
        var activas = await api.ListarZonasAsync(new FiltroZonasDto { TextoBusqueda = nombre, SoloActivas = true });
        Assert.Empty(activas);
    }

    // ───────────────────────────── Cuadro tarifario ─────────────────────────────

    [FactPostgres]
    public async Task Tarifas_PublicarVersion_CierraLaAnteriorYSimula()
    {
        var api = new HttpCuadroTarifarioApi(_factory.CreateClient());
        var anterior = await api.ObtenerVersionVigenteAsync();
        Assert.NotNull(anterior);

        var zonas = await new HttpZonasCoberturaApi(_factory.CreateClient()).ListarZonasAsync();
        var modalidad = "Prueba " + Sufijo();

        // La versión nueva conserva las reglas vigentes: las demás pruebas comparten esta base y dependen de ellas
        var reglasPrevias = anterior!.ReglasTarifa.Select(r => new CrearReglaTarifaDto
        {
            ZonaCoberturaId = r.ZonaCoberturaId, ModalidadServicio = r.ModalidadServicio, PesoDesde = r.PesoDesde, PesoHasta = r.PesoHasta,
            VolumenDesde = r.VolumenDesde, VolumenHasta = r.VolumenHasta, PrecioBase = r.PrecioBase, Recargo = r.Recargo, Bonificacion = r.Bonificacion
        }).ToList();

        var nueva = await api.PublicarNuevaVersionAsync(new CrearVersionCuadroTarifarioDto
        {
            VigenteDesde = DateTime.UtcNow.AddSeconds(-1),
            Comentario = "Ajuste de precios",
            ReglasTarifa = reglasPrevias.Concat(new[]
            {
                new CrearReglaTarifaDto { ModalidadServicio = modalidad, PesoDesde = 0, PesoHasta = 5, VolumenDesde = 0, VolumenHasta = 1, PrecioBase = 200, Recargo = 20, Bonificacion = 10 },
                new CrearReglaTarifaDto { ZonaCoberturaId = zonas[0].Id, ModalidadServicio = modalidad, PesoDesde = 0, PesoHasta = 5, VolumenDesde = 0, VolumenHasta = 1, PrecioBase = 150 }
            }).ToList()
        });
        Assert.Equal(anterior.Version + 1, nueva.Version);
        Assert.Equal("Ajuste de precios", nueva.Comentario);
        Assert.Equal(anterior.TotalReglas + 2, nueva.TotalReglas);

        var vigente = await api.ObtenerVersionVigenteAsync();
        Assert.Equal(nueva.Id, vigente!.Id);

        var historial = await api.ListarVersionesAsync();
        var cerrada = historial.Single(v => v.Id == anterior.Id);
        Assert.NotNull(cerrada.VigenteHasta);

        var general = await api.SimularTarifaAsync(new SimularTarifaDto { PesoKg = 2, VolumenM3 = 0.5m, ModalidadServicio = modalidad });
        Assert.True(general.Exitoso, general.Mensaje);
        Assert.Equal(210m, general.PrecioFinal);

        // La regla específica de la zona tiene prioridad sobre la general
        var enZona = await api.SimularTarifaAsync(new SimularTarifaDto { PesoKg = 2, VolumenM3 = 0.5m, ModalidadServicio = modalidad, ZonaCoberturaId = zonas[0].Id });
        Assert.Equal(150m, enZona.PrecioFinal);

        var sinCobertura = await api.SimularTarifaAsync(new SimularTarifaDto { PesoKg = 500, VolumenM3 = 0.5m, ModalidadServicio = modalidad });
        Assert.False(sinCobertura.Exitoso);

        await Assert.ThrowsAsync<ApiExcepcion>(() => api.PublicarNuevaVersionAsync(new CrearVersionCuadroTarifarioDto()));
    }

    // ───────────────────────────── Reglas operativas ─────────────────────────────

    [FactPostgres]
    public async Task Reglas_PublicarVersion_YSimularReintentos()
    {
        var api = new HttpReglasOperativasApi(_factory.CreateClient());
        var anterior = await api.ObtenerVersionVigenteAsync();
        Assert.NotNull(anterior);

        // La versión nueva repite las reglas vigentes salvo el plazo entre intentos: el resto de las pruebas comparten esta base
        var nueva = await api.PublicarNuevaVersionAsync(new CrearReglaOperativaDto
        {
            MaxIntentosEntrega = anterior!.MaxIntentosEntrega, HorasEntreIntentos = 12, TipoPruebaEntregaExigida = anterior.TipoPruebaEntregaExigida,
            PoliticaDevolucion = anterior.PoliticaDevolucion, HorasPlazoComprometidoEstandar = anterior.HorasPlazoComprometidoEstandar,
            HorasPlazoComprometidoUrgente = anterior.HorasPlazoComprometidoUrgente,
            VigenteDesde = DateTime.UtcNow.AddSeconds(-1), Comentario = "Endurecer reintentos"
        });
        Assert.Equal(anterior.Version + 1, nueva.Version);
        Assert.Equal(12, nueva.HorasEntreIntentos);

        var vigente = await api.ObtenerVersionVigenteAsync();
        Assert.Equal(nueva.Id, vigente!.Id);
        Assert.Equal("Endurecer reintentos", vigente.Comentario);
        Assert.NotNull((await api.ObtenerPorIdAsync(anterior.Id))!.VigenteHasta);

        var enEspera = await api.SimularReglasAsync(new SimularReglaOperativaDto
        {
            ModalidadServicio = "Estándar 24-48h", IntentosRealizados = 1,
            FechaDespacho = DateTime.UtcNow.AddHours(-2), FechaUltimoIntento = DateTime.UtcNow.AddHours(-1)
        });
        Assert.True(enEspera.Exitoso);
        Assert.False(enEspera.PuedeReintentarAhora);
        Assert.Equal(nueva.MaxIntentosEntrega - 1, enEspera.IntentosRestantes);

        var agotado = await api.SimularReglasAsync(new SimularReglaOperativaDto { IntentosRealizados = nueva.MaxIntentosEntrega, FechaUltimoIntento = null });
        Assert.True(agotado.SuperaMaximoIntentos);
        Assert.Contains("devolución", agotado.AccionSugerida);
    }

    // ───────────────────────────── Motivos de no entrega ─────────────────────────────

    [FactPostgres]
    public async Task Motivos_Catalogo_AltaEdicionEstadoYSincronizacionMovil()
    {
        var api = new HttpMotivosNoEntregaApi(_factory.CreateClient());
        var catalogo = await api.ListarMotivosAsync();
        Assert.NotEmpty(catalogo);
        Assert.DoesNotContain(catalogo, m => m.Codigo == "RETORNO_SIN_INTENTO");

        var codigo = "TEST_" + Sufijo().ToUpperInvariant();
        var creado = await api.CrearMotivoAsync(new CrearMotivoNoEntregaDto { Codigo = codigo.ToLowerInvariant(), Descripcion = "Motivo de prueba" });
        Assert.Equal(codigo, creado.Codigo);
        Assert.True(creado.Activo);

        var repetido = await Assert.ThrowsAsync<ApiExcepcion>(() => api.CrearMotivoAsync(new CrearMotivoNoEntregaDto { Codigo = codigo, Descripcion = "otra" }));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.CodigoEstado);

        var actualizado = await api.ActualizarMotivoAsync(new ActualizarMotivoNoEntregaDto { Id = creado.Id, Descripcion = "Descripción nueva", Activo = true });
        Assert.Equal("Descripción nueva", actualizado.Descripcion);

        Assert.Contains(await api.ObtenerMotivosParaSincronizacionMovilAsync(), m => m.Id == creado.Id);

        await api.CambiarEstadoAsync(creado.Id, false);
        Assert.DoesNotContain(await api.ObtenerMotivosParaSincronizacionMovilAsync(), m => m.Id == creado.Id);
        Assert.Contains(await api.ListarMotivosAsync(new FiltroMotivosNoEntregaDto { Busqueda = codigo }), m => m.Id == creado.Id && !m.Activo);

        var resumen = await api.ObtenerResumenAsync();
        Assert.Equal(resumen.TotalMotivos, resumen.MotivosActivosParaMovil + resumen.MotivosInactivos);
        Assert.True(resumen.MotivosInactivos >= 1);
    }

    // ───────────────────────────── Identidad visual ─────────────────────────────

    [FactPostgres]
    public async Task IdentidadVisual_ActualizaMarcaYValidaColores()
    {
        var api = new HttpIdentidadVisualApi(_factory.CreateClient());
        var actual = await api.ObtenerIdentidadActualAsync();
        Assert.False(string.IsNullOrEmpty(actual.OperadorId));

        var actualizada = await api.ActualizarIdentidadVisualAsync(new ActualizarIdentidadVisualDto
        {
            NombreComercial = "Última Milla Express", ColorPrimario = "#0f766e", ColorSecundario = "#facc15",
            EmailContacto = "Hola@UltimaMilla.com.uy", TelefonoContacto = "+598 2000 0000",
            SitioWeb = "https://ultimamilla.com.uy", MensajeComunicaciones = "¡Gracias!"
        });
        Assert.Equal("#0F766E", actualizada.ColorPrimario);
        Assert.Equal("hola@ultimamilla.com.uy", actualizada.EmailContacto);

        var releida = await api.ObtenerIdentidadActualAsync();
        Assert.Equal("#FACC15", releida.ColorSecundario);
        Assert.Equal("¡Gracias!", releida.MensajeComunicaciones);

        var invalida = await Assert.ThrowsAsync<ApiExcepcion>(() => api.ActualizarIdentidadVisualAsync(new ActualizarIdentidadVisualDto
        {
            NombreComercial = "X", ColorPrimario = "azul", ColorSecundario = "#fff", EmailContacto = "a@a.com", TelefonoContacto = "1"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.CodigoEstado);

        Assert.NotEmpty(await api.ListarOperadoresDemoAsync());
        Assert.Equal(actual.OperadorId, (await api.ObtenerPorOperadorDemoAsync(actual.OperadorId)).OperadorId);
    }

    // ───────────────────────────── Flota ─────────────────────────────

    [FactPostgres]
    public async Task Flota_VehiculosYRepartidores_AltaAsignacionYReglas()
    {
        var api = new HttpFlotaApi(_factory.CreateClient());

        var existentes = await api.ListarRepartidoresAsync();
        var demo = existentes.Single(r => r.NombreCompleto == "Martín Rodríguez");
        Assert.NotNull(demo.VehiculoPatente);

        var patente = "T" + Sufijo().ToUpperInvariant();
        var vehiculo = await api.CrearVehiculoAsync(new CrearVehiculoDto { Patente = patente.ToLowerInvariant(), Tipo = "Moto", CapacidadPeso = 40, CapacidadVolumen = 0.3m });
        Assert.Equal(patente, vehiculo.Patente);
        Assert.Null(vehiculo.RepartidorAsignadoId);

        await Assert.ThrowsAsync<ApiExcepcion>(() =>
            api.CrearVehiculoAsync(new CrearVehiculoDto { Patente = patente, Tipo = "Moto", CapacidadPeso = 1, CapacidadVolumen = 1 }));

        var email = $"repartidor.{Sufijo()}@ultimamilla.com.uy";
        var repartidor = await api.CrearRepartidorAsync(new CrearRepartidorDto
        {
            NombreCompleto = "Repartidor Prueba", Email = email, Documento = "4.111.222-3", Telefono = "099111222", VehiculoAsignadoId = vehiculo.Id
        });
        Assert.Equal("Disponible", repartidor.Estado);
        Assert.Equal(patente, repartidor.VehiculoPatente);

        // El vehículo ya está tomado: no puede asignarse a otro repartidor
        await Assert.ThrowsAsync<ApiExcepcion>(() => api.AsignarVehiculoAsync(demo.Id, vehiculo.Id));

        await api.AsignarVehiculoAsync(repartidor.Id, null);
        Assert.Null((await api.ObtenerRepartidorPorIdAsync(repartidor.Id))!.VehiculoAsignadoId);
        await api.AsignarVehiculoAsync(demo.Id, demo.VehiculoAsignadoId);

        await api.CambiarEstadoRepartidorAsync(repartidor.Id, "Inactivo");
        Assert.Contains(await api.ListarRepartidoresAsync("Inactivo"), r => r.Id == repartidor.Id);

        var actualizado = await api.ActualizarRepartidorAsync(new ActualizarRepartidorDto
        {
            Id = repartidor.Id, NombreCompleto = "Repartidor Renombrado", Email = email, Documento = "4.111.222-3",
            Telefono = "099333444", VehiculoAsignadoId = vehiculo.Id, Estado = "Disponible"
        });
        Assert.Equal("Repartidor Renombrado", actualizado.NombreCompleto);

        await api.CambiarEstadoVehiculoAsync(vehiculo.Id, false);
        Assert.DoesNotContain(await api.ListarVehiculosAsync(true), v => v.Id == vehiculo.Id);
        var asignado = (await api.ListarVehiculosAsync()).Single(v => v.Id == vehiculo.Id);
        Assert.Equal("Repartidor Renombrado", asignado.RepartidorAsignadoNombre);
    }

    // ───────────────────────────── Cola de mensajes fallidos ─────────────────────────────

    [FactPostgres]
    public async Task Mensajeria_ReintentoDescarteYLimpieza()
    {
        var api = new HttpMensajeriaApi(_factory.CreateClient());

        var cola = await api.ListarMensajesFallidosAsync();
        Assert.True(cola.Count >= 3);
        var primero = cola.First(m => m.Estado == "Fallido");
        var segundo = cola.First(m => m.Estado == "Fallido" && m.Id != primero.Id);
        Assert.False(string.IsNullOrEmpty(primero.MotivoFallo));

        var detalle = await api.ObtenerMensajePorIdAsync(primero.Id);
        Assert.Equal(primero.TipoMensaje, detalle!.TipoMensaje);

        var fallo = await api.ReintentarMensajesAsync(new[] { primero.Id }, forzarFallo: true);
        Assert.Equal(1, fallo.Fallidos);
        Assert.Equal(primero.Intentos + 1, (await api.ObtenerMensajePorIdAsync(primero.Id))!.Intentos);

        var exito = await api.ReintentarMensajesAsync(new[] { primero.Id, Guid.NewGuid() });
        Assert.Equal(1, exito.Exitosos);
        Assert.Equal("Resuelto", (await api.ObtenerMensajePorIdAsync(primero.Id))!.Estado);

        Assert.True(await api.DescartarMensajeAsync(segundo.Id));
        Assert.False(await api.DescartarMensajeAsync(segundo.Id));

        Assert.True(await api.LimpiarMensajesResueltosAsync() >= 1);
        Assert.Null(await ObtenerOrNullAsync(api, primero.Id));

        await Assert.ThrowsAsync<ApiExcepcion>(() => api.ReintentarMensajesAsync(Array.Empty<Guid>()));
    }

    private static async Task<object?> ObtenerOrNullAsync(HttpMensajeriaApi api, Guid id)
    {
        try
        {
            return await api.ObtenerMensajePorIdAsync(id);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
