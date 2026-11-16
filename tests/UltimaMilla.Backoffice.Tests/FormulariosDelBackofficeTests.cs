using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.MotivosNoEntrega;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Formularios del Backoffice que fallaban sin que ninguna prueba lo notara: se ejercitan como lo haría el navegador.
/// </summary>
// Las dos clases levantan el Backoffice en el mismo proceso: en un equipo limpio (CI) compiten por crear las claves de protección de datos
// y las sesiones y los tokens antiforgery de una dejan de valer para la otra. Se ejecutan una tras otra.
[Collection("Backoffice")]
public class FormulariosDelBackofficeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly WebApplicationFactory<Program> _fabrica;

    public FormulariosDelBackofficeTests(WebApplicationFactory<Program> fabrica)
    {
        // Pruebas herméticas: el Backoffice usa las maquetas en memoria y no una API real (appsettings.json apunta a localhost:5000, que en CI no existe)
        _fabrica = fabrica.WithWebHostBuilder(constructor => constructor.UseSetting("UsarMocks", "true"));
    }

    private Task<HttpClient> SesionDeAdministradorAsync() => SesionComoAsync("Administrador");

    private async Task<HttpClient> SesionComoAsync(string perfil)
    {
        var cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var respuesta = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = perfil,
            ["NombreUsuario"] = "Prueba " + perfil,
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        return cliente;
    }

    [Fact]
    public async Task Flota_UnErrorMostradoUnaVez_NoReapareceJuntoAlSiguienteAvisoDeExito()
    {
        // El error de un alta fallida se guardaba en TempData y la página se pintaba sin redirigir: TempData lo conservaba para la petición siguiente
        // y aparecía otra vez, junto al aviso de éxito de la acción que sí funcionaba ("alta correcta" y "error" a la vez).
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();
        var token = Token.Match(html).Groups[1].Value;

        var fallida = await cliente.PostAsync("/Flota?handler=CrearVehiculo", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoVehiculo.Patente"] = "",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Contains("Por favor verifica los datos ingresados para el vehículo", WebUtility.HtmlDecode(await fallida.Content.ReadAsStringAsync()));

        var siguiente = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Por favor verifica los datos ingresados para el vehículo", WebUtility.HtmlDecode(siguiente));
    }

    [Fact]
    public async Task ZonaNueva_LasFranjasSugeridasSeMuestranEnFormato24Horas()
    {
        // Con el formato "hh" (12 horas) 13:00 se mostraba como 01:00 y la zona no se podía guardar sin corregir las franjas
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Zonas/Nueva")).Content.ReadAsStringAsync();

        var horas = Regex.Matches(html, "<input type=\"time\" name=\"Franjas\\[\\d+\\]\\.Hora(?:Inicio|Fin)\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(new[] { "09:00", "13:00", "14:00", "18:00" }, horas);
    }

    [Fact]
    public async Task MotivosNoEntrega_AltaDeUnMotivo_NoSeRechazaPorLosCamposDelFormularioDeEdicion()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/MotivosNoEntrega")).Content.ReadAsStringAsync();

        var respuesta = await cliente.PostAsync("/MotivosNoEntrega?handler=Crear", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoMotivo.Codigo"] = "MOTIVO_PRUEBA_" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            ["NuevoMotivo.Descripcion"] = "Motivo de prueba",
            ["NuevoMotivo.IniciarActivo"] = "true",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        // Alta correcta: redirige al listado; si se rechazara por validación volvería a pintar la página con el error (200)
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var aviso = Regex.Match(cuerpo, "(Por favor[^<]{0,400}|Error al[^<]{0,160})").Groups[1].Value + " / HTTP " + (int)respuesta.StatusCode;
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, "No redirigió: " + aviso);
    }

    [Fact]
    public async Task MotivosNoEntrega_Listado_NoMuestraAltaCeroUnoCeroUnoCeroCeroCeroUno()
    {
        var fechaValida = new DateTime(2026, 11, 10, 15, 30, 0, DateTimeKind.Utc);
        using var fabrica = _fabrica.WithWebHostBuilder(constructor => constructor.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<IMotivosNoEntregaApi>();
            servicios.AddSingleton<IMotivosNoEntregaApi>(new StubMotivosNoEntregaApi(
                new MotivoNoEntregaDto { Id = Guid.NewGuid(), Codigo = "SIN_FECHA", Descripcion = "Motivo sin fecha", Activo = true, FechaAlta = default },
                new MotivoNoEntregaDto { Id = Guid.NewGuid(), Codigo = "CON_FECHA", Descripcion = "Motivo con fecha", Activo = true, FechaAlta = fechaValida }
            ));
        }));

        var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var loginHtml = await (await cliente.GetAsync("/Login")).Content.ReadAsStringAsync();
        var login = await cliente.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RolSeleccionado"] = "Administrador",
            ["NombreUsuario"] = "Prueba Administrador",
            ["__RequestVerificationToken"] = Token.Match(loginHtml).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var html = WebUtility.HtmlDecode(await (await cliente.GetAsync("/MotivosNoEntrega")).Content.ReadAsStringAsync());

        Assert.Contains("Motivo sin fecha", html);
        Assert.Contains("Motivo con fecha", html);
        Assert.DoesNotContain("01/01/0001", html);
        Assert.Contains($"Alta: {fechaValida.ToLocalTime():dd/MM/yyyy}", html);
    }

    private sealed class StubMotivosNoEntregaApi(params MotivoNoEntregaDto[] motivos) : IMotivosNoEntregaApi
    {
        public Task<IReadOnlyList<MotivoNoEntregaDto>> ListarMotivosAsync(FiltroMotivosNoEntregaDto? filtro = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MotivoNoEntregaDto>>(motivos);
        public Task<MotivoNoEntregaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(motivos.FirstOrDefault(m => m.Id == id));
        public Task<MotivoNoEntregaDto> CrearMotivoAsync(CrearMotivoNoEntregaDto dto, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<MotivoNoEntregaDto> ActualizarMotivoAsync(ActualizarMotivoNoEntregaDto dto, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<ResumenMotivosNoEntregaDto> ObtenerResumenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResumenMotivosNoEntregaDto { TotalMotivos = motivos.Length, MotivosActivosParaMovil = motivos.Count(m => m.Activo), MotivosInactivos = motivos.Count(m => !m.Activo) });
        public Task<IReadOnlyList<MotivoNoEntregaDto>> ObtenerMotivosParaSincronizacionMovilAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MotivoNoEntregaDto>>(motivos.Where(m => m.Activo).ToList());
    }

    [Fact]
    public async Task TarifaNuevaVersion_CadaReglaPrecargadaConservaSuModalidadAlGuardar()
    {
        // Las modalidades reales son "Estandar", "Urgente" y "Retiro". Si la lista desplegable no las ofrece, ninguna opción queda marcada, el navegador
        // envía la primera y publicar una versión renombra todas las reglas: el operador se queda sin tarifa y fallan todas las altas (D-06, 14/11).
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Tarifas/NuevaVersion")).Content.ReadAsStringAsync();

        var filas = Regex.Matches(html, "(?s)<select name=\"Reglas\\[\\d+\\]\\.ModalidadServicio\".*?</select>");
        Assert.NotEmpty(filas);
        var enviadas = filas.Select(f =>
        {
            var opciones = Regex.Matches(f.Value, "<option value=\"([^\"]*)\"([^>]*)>");
            var marcada = opciones.FirstOrDefault(o => Regex.IsMatch(o.Groups[2].Value, "\\bselected\\b"));
            // Sin ninguna opción marcada, un navegador envía la primera
            return WebUtility.HtmlDecode((marcada ?? opciones.First()).Groups[1].Value);
        }).ToList();

        Assert.Contains("Estandar", enviadas);
        Assert.Contains("Urgente", enviadas);
        Assert.Contains("Carga Pesada", enviadas); // un valor anterior desconocido se conserva, no se reemplaza en silencio
        Assert.DoesNotContain("Estándar 24-48h", enviadas);
    }

    /// <summary>Los datos que enviaría un navegador al guardar: todos los campos con nombre; en cada lista, la opción marcada o, si no hay, la primera.</summary>
    private static Dictionary<string, string> DatosDelFormulario(string html)
    {
        var datos = new Dictionary<string, string>();
        foreach (Match campo in Regex.Matches(html, "<input\\b[^>]*>"))
        {
            var nombre = Regex.Match(campo.Value, "\\bname=\"([^\"]+)\"").Groups[1].Value;
            if (nombre.Length == 0) continue;
            var tipo = Regex.Match(campo.Value, "\\btype=\"([^\"]+)\"").Groups[1].Value;
            if ((tipo == "checkbox" || tipo == "radio") && !Regex.IsMatch(campo.Value, "\\bchecked\\b")) continue;
            datos[nombre] = WebUtility.HtmlDecode(Regex.Match(campo.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value);
        }
        foreach (Match lista in Regex.Matches(html, "(?s)<select\\b[^>]*\\bname=\"([^\"]+)\"[^>]*>(.*?)</select>"))
        {
            var opciones = Regex.Matches(lista.Groups[2].Value, "<option\\b([^>]*)>").ToList();
            if (opciones.Count == 0) continue;
            var elegida = opciones.FirstOrDefault(o => Regex.IsMatch(o.Groups[1].Value, "\\bselected\\b")) ?? opciones[0];
            datos[lista.Groups[1].Value] = WebUtility.HtmlDecode(Regex.Match(elegida.Groups[1].Value, "value=\"([^\"]*)\"").Groups[1].Value);
        }
        return datos;
    }

    /// <summary>Como el anterior pero conserva el orden del documento y los nombres repetidos: ASP.NET toma el primer valor de cada nombre.</summary>
    private static List<(string Nombre, string Valor)> ParesDelFormulario(string html)
    {
        var pares = new List<(string, string)>();
        foreach (Match campo in Regex.Matches(html, "(?s)<input\\b[^>]*>|<select\\b[^>]*>.*?</select>"))
        {
            var nombre = Regex.Match(campo.Value.Split('>')[0], "\\bname=\"([^\"]+)\"").Groups[1].Value;
            if (nombre.Length == 0) continue;
            if (campo.Value.StartsWith("<input"))
            {
                var tipo = Regex.Match(campo.Value, "\\btype=\"([^\"]+)\"").Groups[1].Value;
                if ((tipo == "checkbox" || tipo == "radio") && !Regex.IsMatch(campo.Value, "\\bchecked\\b")) continue;
                pares.Add((nombre, WebUtility.HtmlDecode(Regex.Match(campo.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value)));
            }
            else
            {
                var opciones = Regex.Matches(campo.Value, "<option\\b([^>]*)>").ToList();
                if (opciones.Count == 0) continue;
                var elegida = opciones.FirstOrDefault(o => Regex.IsMatch(o.Groups[1].Value, "\\bselected\\b")) ?? opciones[0];
                pares.Add((nombre, WebUtility.HtmlDecode(Regex.Match(elegida.Groups[1].Value, "value=\"([^\"]*)\"").Groups[1].Value)));
            }
        }
        return pares;
    }

    [Fact]
    public async Task ReglasOperativasNuevaVersion_SinTocarNada_ConservaLaPruebaDeEntregaYLaPoliticaDeDevolucion()
    {
        // La lista desplegable y la caja de texto compartían el mismo nombre: ASP.NET tomaba el valor de la lista (la primera opción si ninguna
        // coincidía con el valor vigente) y publicar una versión cambiaba en silencio la prueba exigida y la política de devolución (D-08, 14/11).
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/ReglasOperativas/NuevaVersion")).Content.ReadAsStringAsync();
        var pares = ParesDelFormulario(html);

        foreach (var (campo, esperado) in new[]
                 {
                     ("NuevaVersion.TipoPruebaEntregaExigida", "Firma digital y documento del receptor"),
                     ("NuevaVersion.PoliticaDevolucion", "72")
                 })
        {
            var valores = pares.Where(p => p.Nombre == campo).Select(p => p.Valor).ToList();
            Assert.Single(valores);          // la lista ya no envía nada
            Assert.Equal(esperado, valores[0]);
        }
    }

    [Fact]
    public async Task TarifaNuevaVersion_LosValoresPrecargadosCumplenElPasoDeSusCampos()
    {
        // jquery-validation rechaza un valor con más decimales que su "step" ("150.00" con step="1"): el servidor escribe los números con la escala de la
        // base y, con la validación del navegador funcionando, el formulario no se podía publicar con sus propios valores (D-21, visto en un navegador real)
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Tarifas/NuevaVersion")).Content.ReadAsStringAsync();

        var campos = Regex.Matches(html, "<input type=\"number\" step=\"([0-9.]+)\" name=\"Reglas\\[\\d+\\]\\.(\\w+)\" value=\"([^\"]*)\"").ToList();
        Assert.NotEmpty(campos);
        static int Decimales(string numero) { var corte = numero.IndexOfAny(new[] { '.', ',' }); return corte < 0 ? 0 : numero.Length - corte - 1; }
        foreach (var campo in campos)
        {
            var paso = campo.Groups[1].Value; var valor = campo.Groups[3].Value;
            Assert.True(Decimales(valor) <= Decimales(paso),
                $"{campo.Groups[2].Value}: el valor '{valor}' tiene más decimales que el paso '{paso}'; la validación del navegador lo rechaza.");
        }
    }

    [Fact]
    public async Task TarifaNuevaVersion_ConElVolumenSinLimiteDeLaTarifaReal_SePublica()
    {
        // La tarifa de producción usa 1.000.000.000 como "sin límite" de volumen; el formulario solo admitía hasta 1.000, así que editar un precio
        // no hacía nada y no decía por qué (D-07, 14/11). Se simula lo que enviaría el navegador con los valores reales.
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Tarifas/NuevaVersion")).Content.ReadAsStringAsync();
        var datos = DatosDelFormulario(html);
        foreach (var clave in datos.Keys.Where(k => k.EndsWith(".VolumenHasta")).ToList())
            datos[clave] = "1000000000";
        datos["Reglas[0].PrecioBase"] = "160";

        var respuesta = await cliente.PostAsync("/Tarifas/NuevaVersion", new FormUrlEncodedContent(datos));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        var aviso = Regex.Match(WebUtility.HtmlDecode(cuerpo), "(No se pudo publicar[^<]{0,300}|Error de validación[^<]{0,300})").Groups[1].Value;
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, $"No redirigió (HTTP {(int)respuesta.StatusCode}): {aviso}");
    }

    [Fact]
    public async Task TarifaNuevaVersion_ConUnDatoInvalido_MuestraElMotivoEnLaPagina()
    {
        // Antes la página se volvía a mostrar igual, sin ningún mensaje
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Tarifas/NuevaVersion")).Content.ReadAsStringAsync();
        var datos = DatosDelFormulario(html);
        datos["Reglas[0].PesoDesde"] = "5";
        datos["Reglas[0].PesoHasta"] = "5"; // el peso inicial no es menor al final

        var respuesta = await cliente.PostAsync("/Tarifas/NuevaVersion", new FormUrlEncodedContent(datos));

        var cuerpo = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("el peso inicial debe ser menor al peso final", cuerpo);
        Assert.Contains("No se pudo publicar", cuerpo);
    }

    [Fact]
    public async Task TarifaNuevaVersion_IncluyePanelDeConfirmacionParaPublicarNuevaVersion()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = WebUtility.HtmlDecode(await (await cliente.GetAsync("/Tarifas/NuevaVersion")).Content.ReadAsStringAsync());

        Assert.Contains("id=\"modalConfirmarPublicacion\"", html);
        Assert.Contains("¿Confirma la publicación de esta nueva versión? La versión anterior cerrará su vigencia y los nuevos envíos se tarifarán con estas reglas.", html);
        Assert.Contains("id=\"btnConfirmarPublicacionModal\"", html);
        Assert.DoesNotContain("onclick=\"return confirm(", html);
    }

    [Fact]
    public async Task Recepcion_SiElOperarioDesmarcaTodosLosBultos_NoSeDaElEnvioPorRecibido()
    {
        // Antes, una lista vacía se interpretaba como "llegaron todos" y el envío quedaba En Depósito sin discrepancias
        var cliente = await SesionComoAsync("Operario");
        var muelle = await (await cliente.GetAsync("/Deposito")).Content.ReadAsStringAsync();
        var envioId = Regex.Match(muelle, @"Deposito/Recepcion\?id=([0-9a-f-]{36})").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(envioId), "El muelle no lista ningún envío admitido.");
        var pagina = await cliente.GetAsync($"/Deposito/Recepcion?id={envioId}");
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.True(pagina.StatusCode == HttpStatusCode.OK, "GET recepción: " + (int)pagina.StatusCode + " " + pagina.Headers.Location);

        var respuesta = await cliente.PostAsync($"/Deposito/Recepcion?id={envioId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Formulario.EnvioId"] = envioId,
            ["Formulario.CodigosEscaneadosCsv"] = "",
            ["Formulario.CodigosDanadosCsv"] = "",
            ["Formulario.Observaciones"] = "",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("No hay ningún bulto marcado", WebUtility.HtmlDecode(cuerpo));
    }

    [Fact]
    public async Task ZonaNueva_ConTodosLosDias_CreaLaZonaYExpandeLasFranjas()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Zonas/Nueva")).Content.ReadAsStringAsync();
        var datos = DatosDelFormulario(html);
        var nombreZona = "Zona Todos Los Dias " + Guid.NewGuid().ToString("N")[..6];
        datos["NuevaZona.Nombre"] = nombreZona;
        datos["NuevaZona.PoligonoGeografico"] = "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9080, -56.2120]]";
        datos["Franjas[0].DiaSemana"] = "-1";
        datos["Franjas[0].HoraInicio"] = "09:00";
        datos["Franjas[0].HoraFin"] = "13:00";
        datos["Franjas[0].CapacidadMaximaEnvios"] = "35";
        datos.Remove("Franjas[1].DiaSemana");
        datos.Remove("Franjas[1].HoraInicio");
        datos.Remove("Franjas[1].HoraFin");
        datos.Remove("Franjas[1].CapacidadMaximaEnvios");

        var respuesta = await cliente.PostAsync("/Zonas/Nueva", new FormUrlEncodedContent(datos));

        var cuerpo = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, $"No redirigió (HTTP {(int)respuesta.StatusCode}): {cuerpo[..Math.Min(cuerpo.Length, 1200)]}");

        var listado = WebUtility.HtmlDecode(await (await cliente.GetAsync("/Zonas")).Content.ReadAsStringAsync());
        Assert.Contains(nombreZona, listado);
        Assert.Contains("7 franjas horarias", listado);
    }

    [Fact]
    public async Task ZonaNueva_ConNombreMuyCortoYTodosLosDias_MuestraElErrorEnLaPagina()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Zonas/Nueva")).Content.ReadAsStringAsync();
        var datos = DatosDelFormulario(html);
        datos["NuevaZona.Nombre"] = "AB"; // menor a 3 caracteres
        datos["Franjas[0].DiaSemana"] = "-1";

        var respuesta = await cliente.PostAsync("/Zonas/Nueva", new FormUrlEncodedContent(datos));

        var cuerpo = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Error de validación:", cuerpo);
        Assert.Contains("entre 3 y 100 caracteres", cuerpo);
    }

    [Fact]
    public async Task ZonaEditar_ConTodosLosDias_ActualizaLaZonaYExpandeLasFranjas()
    {
        var cliente = await SesionDeAdministradorAsync();
        var listado = await (await cliente.GetAsync("/Zonas")).Content.ReadAsStringAsync();
        var zonaId = Regex.Match(listado, @"/Zonas/Editar/([0-9a-f-]{36})", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(zonaId), "No se encontró ninguna zona editable.");

        var html = await (await cliente.GetAsync($"/Zonas/Editar/{zonaId}")).Content.ReadAsStringAsync();
        var datos = DatosDelFormulario(html);
        foreach (var clave in datos.Keys.Where(k => k.StartsWith("Franjas[", StringComparison.Ordinal)).ToList())
            datos.Remove(clave);

        datos["Zona.PoligonoGeografico"] = "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9080, -56.2120]]";
        datos["Franjas[0].DiaSemana"] = "-1";
        datos["Franjas[0].HoraInicio"] = "08:00";
        datos["Franjas[0].HoraFin"] = "12:00";
        datos["Franjas[0].CapacidadMaximaEnvios"] = "25";

        var respuesta = await cliente.PostAsync($"/Zonas/Editar/{zonaId}", new FormUrlEncodedContent(datos));

        var cuerpo = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, $"No redirigió (HTTP {(int)respuesta.StatusCode}): {cuerpo[..Math.Min(cuerpo.Length, 1200)]}");
    }

    [Fact]
    public async Task Flota_CamposDeCedulaExigenFormatoOchoDigitosSinPuntosNiGuiones()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();

        Assert.Contains("placeholder=\"Ej: 12345678\"", html);
        Assert.Contains("pattern=\"[0-9]{8}\"", html);
        Assert.Contains("maxlength=\"8\"", html);
        Assert.DoesNotContain("4.512.980-3", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("4.512.980-3")]
    [InlineData("4512980-3")]
    [InlineData("cualquiera")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    public async Task Flota_AltaRepartidorConCedulaInvalida_SeRechazaYConservaPanelAbierto(string cedulaInvalida)
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();

        var respuesta = await cliente.PostAsync("/Flota?handler=CrearRepartidor", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoRepartidor.NombreCompleto"] = "Juan Repartidor",
            ["NuevoRepartidor.Email"] = $"repartidor.{Guid.NewGuid():N}@operador.uy",
            ["NuevoRepartidor.Documento"] = cedulaInvalida,
            ["NuevoRepartidor.Telefono"] = "+598 99 111 222",
            ["NuevoRepartidor.Password"] = "Segura.2026!",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        var cuerpo = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("panelRepartidorNuevo", cuerpo);
    }

    [Fact]
    public async Task Flota_AltaRepartidorConCedulaOchoDigitos_RedirigeConExito()
    {
        var cliente = await SesionDeAdministradorAsync();
        var html = await (await cliente.GetAsync("/Flota")).Content.ReadAsStringAsync();
        var sufijo = Guid.NewGuid().ToString("N")[..6];
        var cedulaUnica = Random.Shared.Next(10_000_000, 100_000_000).ToString(System.Globalization.CultureInfo.InvariantCulture);

        var respuesta = await cliente.PostAsync("/Flota?handler=CrearRepartidor", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NuevoRepartidor.NombreCompleto"] = "Repartidor Válido " + sufijo,
            ["NuevoRepartidor.Email"] = $"valido.{sufijo}@operador.uy",
            ["NuevoRepartidor.Documento"] = cedulaUnica,
            ["NuevoRepartidor.Telefono"] = "+598 99 111 222",
            ["NuevoRepartidor.Password"] = "Segura.2026!",
            ["__RequestVerificationToken"] = Token.Match(html).Groups[1].Value
        }));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == HttpStatusCode.Redirect, $"No redirigió: {cuerpo}");
    }
}
