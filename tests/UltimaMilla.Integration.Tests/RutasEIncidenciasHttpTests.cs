using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Frontend.Contracts.Modelos.Incidencias;
using UltimaMilla.Frontend.Contracts.Modelos.Rutas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;
using UltimaMilla.Frontend.Infrastructure.Http;

namespace UltimaMilla.Integration.Tests;

/// <summary>
/// Hojas de ruta e incidencias consumidas con los clientes HTTP reales del backoffice contra la API y Postgres real.
/// Cada prueba usa sus propios repartidor, vehículo y envío para no alterar el estado que esperan las demás.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class RutasEIncidenciasHttpTests
{
    private readonly ApiFactory _factory;

    public RutasEIncidenciasHttpTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Da de alta un envío y lo deja recepcionado y clasificado, listo para entrar en una hoja de ruta.</summary>
    private async Task<(Guid Id, string Codigo)> CrearEnvioClasificadoAsync()
    {
        var cliente = _factory.CreateClient();

        var respuestaAlta = await cliente.PostAsJsonAsync("/api/envios", new
        {
            modalidadServicio = "Estandar", nombreDestinatario = "Cliente " + Sufijo(), calle = "Av. Rivera", numero = "2500", ciudad = "Montevideo",
            bultos = new[] { new { peso = 2, alto = 10, ancho = 10, profundo = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, respuestaAlta.StatusCode);
        var alta = await respuestaAlta.Content.ReadFromJsonAsync<JsonElement>();
        var id = alta.GetProperty("id").GetGuid();
        var bulto = alta.GetProperty("bultos")[0].GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{id}/recepcion", new { codigosBultosEscaneados = new[] { bulto } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync($"/api/deposito/envios/{id}/clasificacion",
            new { bultos = new[] { new { codigoBulto = bulto, prioridad = "Normal" } } })).StatusCode);

        return (id, alta.GetProperty("codigoSeguimiento").GetString()!);
    }

    [FactPostgres]
    public async Task Rutas_ArmadoReasignacionEHistorial_ConLosClientesDelFront()
    {
        var flota = new HttpFlotaApi(_factory.CreateClient());
        var rutas = new HttpRutasApi(_factory.CreateClient());

        var vehiculo = await flota.CrearVehiculoAsync(new CrearVehiculoDto { Patente = "R" + Sufijo().ToUpperInvariant(), Tipo = "Furgón", CapacidadPeso = 500, CapacidadVolumen = 5 });
        var repartidor = await flota.CrearRepartidorAsync(new CrearRepartidorDto
        {
            NombreCompleto = "Repartidor Ruta " + Sufijo(), Email = $"ruta.{Sufijo()}@ultimamilla.com.uy", Documento = "3.222.111-0", Telefono = "099000111"
        });
        var (envioId, codigo) = await CrearEnvioClasificadoAsync();

        var pendientes = await rutas.ListarEnviosPendientesAsync();
        var pendiente = Assert.Single(pendientes, p => p.Id == envioId);
        Assert.Equal(codigo, pendiente.CodigoSeguimiento);

        var dto = new ArmarHojaDeRutaDto
        {
            RepartidorId = repartidor.Id, VehiculoId = vehiculo.Id,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow), EnviosIds = new List<Guid> { envioId }
        };

        var validacion = await rutas.ValidarArmadoRutaAsync(dto);
        Assert.True(validacion.Exitoso, validacion.Mensaje);

        var armado = await rutas.ArmarYConfirmarRutaAsync(dto);
        Assert.True(armado.Exitoso, armado.Mensaje);
        var ruta = armado.HojaDeRuta!;
        Assert.Equal("Liberada", ruta.Estado);
        Assert.Equal(repartidor.NombreCompleto, ruta.RepartidorNombre);
        Assert.Equal(vehiculo.Patente, ruta.VehiculoMatricula);
        Assert.Equal(codigo, Assert.Single(ruta.Paradas).CodigoSeguimiento);
        Assert.True(await rutas.LiberarHojaDeRutaAsync(ruta.Id));

        // El envío ya asignado no puede entrar en otra ruta: se informa como resultado, sin excepción
        var segundo = await rutas.ArmarYConfirmarRutaAsync(dto);
        Assert.False(segundo.Exitoso);
        Assert.NotEmpty(segundo.ErroresValidacion);

        var delRepartidor = await rutas.ListarHojasDeRutaAsync(new FiltroRutasDto { RepartidorId = repartidor.Id });
        Assert.Contains(delRepartidor, r => r.Id == ruta.Id && r.CantidadParadas == 1);
        Assert.DoesNotContain(await rutas.ListarEnviosPendientesAsync(), p => p.Id == envioId);

        var detalle = await rutas.ObtenerHojaDeRutaPorIdAsync(ruta.Id);
        Assert.Equal(ruta.NumeroRuta, detalle!.NumeroRuta);
        Assert.Null(await rutas.ObtenerHojaDeRutaPorIdAsync(Guid.NewGuid()));

        // Franja inexistente: se rechaza con un mensaje en lugar de reprogramar a ciegas
        var sinFranja = await rutas.ReasignarOReprogramarEnvioAsync(new ReasignarEnvioDto
        {
            HojaDeRutaActualId = ruta.Id, EnvioId = envioId, AccionDestino = "ReprogramarFranja", NuevaFranjaHoraria = "03:00 - 04:00"
        }, "despachador.test");
        Assert.False(sinFranja.Exitoso);

        var retiro = await rutas.ReasignarOReprogramarEnvioAsync(new ReasignarEnvioDto
        {
            HojaDeRutaActualId = ruta.Id, EnvioId = envioId, AccionDestino = "VolverADeposito", MotivoIncidencia = "AveriaVehiculo", DetalleMotivo = "Pinchadura"
        }, "despachador.test");
        Assert.True(retiro.Exitoso, retiro.Mensaje);
        Assert.Equal("EnDeposito", retiro.NuevoEstadoEnvio);

        var historial = await rutas.ListarEventosReasignacionAsync(ruta.Id);
        var evento = Assert.Single(historial);
        Assert.Equal(codigo, evento.CodigoSeguimiento);
        Assert.Contains("Pinchadura", evento.Detalle);

        Assert.Empty((await rutas.ObtenerHojaDeRutaPorIdAsync(ruta.Id))!.Paradas.Where(p => p.Estado != "Cancelada"));
        Assert.Contains(await rutas.ListarEnviosPendientesAsync(), p => p.Id == envioId);
    }

    [FactPostgres]
    public async Task Incidencias_CicloCompleto_ConLosClientesDelFront()
    {
        var incidencias = new HttpIncidenciasApi(_factory.CreateClient());

        var envios = await incidencias.ListarEnviosDisponiblesAsync();
        Assert.NotEmpty(envios);
        var envio = envios[0];
        Assert.NotEmpty(await incidencias.ListarRutasDisponiblesAsync());

        var creada = await incidencias.RegistrarIncidenciaAsync(new RegistrarIncidenciaDto
        {
            Tipo = "AveriaVehiculo", Severidad = "Alta", Descripcion = "Se rompió la dirección " + Sufijo(),
            EnvioAfectadoId = envio.Id, AccionInmediata = "Se envió una grúa"
        }, "despachador.test");
        Assert.StartsWith("INC-", creada.NumeroIncidencia);
        Assert.Equal("Abierta", creada.Estado);
        Assert.Equal(envio.CodigoSeguimiento, creada.CodigoSeguimientoEnvio);
        Assert.Contains("grúa", creada.Descripcion);

        Assert.True(await incidencias.AgregarSeguimientoAsync(new AgregarSeguimientoIncidenciaDto
        {
            IncidenciaId = creada.Id, Comentario = "Llamé al taller", NuevoEstado = "EnInvestigacion"
        }, "despachador.test"));

        var enInvestigacion = await incidencias.ListarIncidenciasAsync(new FiltroIncidenciasDto { Estado = "EnInvestigacion" });
        Assert.Contains(enInvestigacion, i => i.Id == creada.Id);
        Assert.DoesNotContain(await incidencias.ListarIncidenciasAsync(new FiltroIncidenciasDto { Estado = "Abierta" }), i => i.Id == creada.Id);
        Assert.Contains(await incidencias.ListarIncidenciasAsync(new FiltroIncidenciasDto { Severidad = "Alta", Busqueda = creada.Descripcion[..20] }), i => i.Id == creada.Id);

        Assert.True(await incidencias.CerrarIncidenciaAsync(new CerrarIncidenciaDto { IncidenciaId = creada.Id, ResolucionDetalle = "Dirección reparada" }, "despachador.test"));

        var cerrada = await incidencias.ObtenerIncidenciaPorIdAsync(creada.Id);
        Assert.Equal("Cerrada", cerrada!.Estado);
        Assert.Equal("Dirección reparada", cerrada.ResolucionDetalle);
        Assert.NotNull(cerrada.FechaResolucion);
        Assert.True(cerrada.HistorialSeguimiento.Count >= 3);
        Assert.Contains(cerrada.HistorialSeguimiento, h => h.Comentario == "Llamé al taller");

        // Sobre una incidencia cerrada la API rechaza la operación; sobre una inexistente la pantalla recibe false
        await Assert.ThrowsAsync<ApiExcepcion>(() =>
            incidencias.CerrarIncidenciaAsync(new CerrarIncidenciaDto { IncidenciaId = creada.Id, ResolucionDetalle = "otra vez" }, "despachador.test"));
        Assert.False(await incidencias.AgregarSeguimientoAsync(new AgregarSeguimientoIncidenciaDto { IncidenciaId = Guid.NewGuid(), Comentario = "x" }, "despachador.test"));
        Assert.Null(await incidencias.ObtenerIncidenciaPorIdAsync(Guid.NewGuid()));
    }
}
