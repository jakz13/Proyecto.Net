using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using UltimaMilla.Frontend.Contracts.Validaciones;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Cambios del 16/11: (1) el portal quedaba en "Cargando Portal del Comercio…" por el manifiesto de recursos en caché; (2) todos los
/// formularios muestran el texto rojo de error en email, teléfono y dirección; (3) "Entregado Parcialmente" se ve en todos los lados.
/// </summary>
public class ValidacionDeFormulariosYParcialTests
{
    private static string Leer(string relativa)
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return File.ReadAllText(Path.Combine(carpeta!.FullName, relativa));
    }

    // ---------- Portal: caché del manifiesto de recursos ----------

    [Fact]
    public void Portal_ElManifiestoDeRecursosSeRevalidaSiempre()
    {
        var programa = Leer("src/Web/PortalComercio/Program.cs");

        // resource-collection.<huella>.js salía "immutable" por un año con una huella que no cambia con el contenido
        Assert.Contains("/_framework/resource-collection", programa);
        Assert.Contains("Headers.CacheControl = \"no-cache\"", programa);
    }

    [Fact]
    public void Portal_PurgaUnaSolaVezLaCacheViejaDelNavegador()
    {
        var programa = Leer("src/Web/PortalComercio/Program.cs");

        Assert.Contains("Clear-Site-Data", programa);
        Assert.Contains("CookiePurgaCache", programa);
        // solo se purga si el navegador todavía no tiene la marca: no se vacía la caché en cada visita
        Assert.Contains("!contexto.Request.Cookies.ContainsKey(CookiePurgaCache)", programa);
    }

    // ---------- Formularios: texto rojo en cada campo ----------

    [Theory]
    [InlineData("src/Web/Backoffice/Pages/Comercios/Nuevo.cshtml", "NuevoComercio", new[] { "Email", "Telefono", "Direccion", "RazonSocial" })]
    [InlineData("src/Web/Backoffice/Pages/Comercios/Editar.cshtml", "Comercio", new[] { "Email", "Telefono", "Direccion" })]
    [InlineData("src/Web/Backoffice/Pages/Usuarios/Nuevo.cshtml", "NuevoUsuario", new[] { "Email", "Telefono" })]
    [InlineData("src/Web/Backoffice/Pages/Usuarios/Editar.cshtml", "Usuario", new[] { "Email", "Telefono" })]
    [InlineData("src/Web/Backoffice/Pages/Flota/Index.cshtml", "NuevoRepartidor", new[] { "Email", "Telefono" })]
    [InlineData("src/Web/Backoffice/Pages/IdentidadVisual/Index.cshtml", "Modelo", new[] { "EmailContacto", "TelefonoContacto", "DireccionDeposito" })]
    [InlineData("src/Web/Backoffice/Pages/Zonas/Nueva.cshtml", "NuevaZona", new[] { "PoligonoGeografico", "Nombre" })]
    [InlineData("src/Web/Backoffice/Pages/Zonas/Editar.cshtml", "Zona", new[] { "PoligonoGeografico", "Nombre" })]
    public void Backoffice_CadaCampoTieneSuTextoRojoDeError(string vista, string modelo, string[] campos)
    {
        var contenido = Leer(vista);
        foreach (var campo in campos)
        {
            var span = $"asp-validation-for=\"{modelo}.{campo}\"";
            Assert.True(contenido.Contains(span), $"{vista}: falta el mensaje de error del campo {modelo}.{campo}.");
        }
    }

    [Fact]
    public void PortalDeRetiros_CadaCampoTieneSuTextoRojoDeError()
    {
        var pagina = Leer("src/Web/PortalComercio.Client/Pages/Retiros.razor");
        foreach (var campo in new[] { "NombreContacto", "TelefonoContacto", "EmailContacto", "Calle", "Numero", "Ciudad", "Motivo" })
            Assert.Contains($"<ValidationMessage For=\"@(() => solicitud.{campo})\"", pagina);
    }

    [Fact]
    public void LosDtoNoUsanLosAtributosLaxos()
    {
        // [EmailAddress] acepta "a@b" y [Phone] acepta casi cualquier cosa: los formularios usan EmailValido y TelefonoNueveDigitos
        var modelos = Path.Combine(RutaRaiz(), "src/Web/Frontend.Contracts/Modelos");
        Assert.True(Directory.Exists(modelos), modelos);

        var infractores = Directory.EnumerateFiles(modelos, "*.cs", SearchOption.AllDirectories)
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\[(EmailAddress|Phone)\b"))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(infractores.Count == 0, "Usan [EmailAddress]/[Phone]: " + string.Join(", ", infractores));
    }

    private static string RutaRaiz()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        return carpeta!.FullName;
    }

    // ---------- Validación en el navegador ----------

    [Theory]
    [InlineData(typeof(EmailValidoAttribute), EmailValidoAttribute.PatronEmail, EmailValidoAttribute.Mensaje)]
    [InlineData(typeof(TelefonoNueveDigitosAttribute), TelefonoNueveDigitosAttribute.PatronTelefono, TelefonoNueveDigitosAttribute.Mensaje)]
    [InlineData(typeof(DireccionValidaAttribute), DireccionValidaAttribute.PatronDireccion, DireccionValidaAttribute.Mensaje)]
    [InlineData(typeof(ColorHexValidoAttribute), ColorHexValidoAttribute.PatronColor, ColorHexValidoAttribute.Mensaje)]
    public void LosAtributos_SeValidanTambienEnElNavegador(Type tipo, string patron, string mensaje)
    {
        // Si ASP.NET no los reconociera como "regex", el error solo aparecería al enviar el formulario (y no en vivo, debajo del campo)
        var atributo = (ValidationAttribute)Activator.CreateInstance(tipo)!;
        var adapter = new ValidationAttributeAdapterProvider().GetAttributeAdapter(atributo, null);
        Assert.NotNull(adapter);

        var proveedor = new EmptyModelMetadataProvider();
        var atributosHtml = new Dictionary<string, string>();
        adapter!.AddValidation(new ClientModelValidationContext(new ActionContext(), proveedor.GetMetadataForType(typeof(string)), proveedor, atributosHtml));

        Assert.Equal(patron, atributosHtml["data-val-regex-pattern"]);
        Assert.Equal(mensaje, atributosHtml["data-val-regex"]);
    }

    // ---------- Entregado Parcialmente ----------

    [Theory]
    [InlineData("src/Web/Backoffice/wwwroot/js/mapas-leaflet.js")]
    [InlineData("src/Web/PortalComercio/wwwroot/js/mapas-leaflet.js")]
    public void MapaDeSeguimiento_ConoceElEstadoEntregadoParcialmente(string js)
    {
        var codigo = Leer(js);

        Assert.Contains("case '11': return 'EntregadoParcialmente'", codigo);      // el estado llega numérico desde la API
        Assert.Contains("case 'EntregadoParcialmente': return 'Entregado parcialmente'", codigo);
        Assert.Contains("estadoNorm === 'EntregadoParcialmente') return 'activo'", codigo);
    }

    [Fact]
    public void ListadoDelBackoffice_PermiteFiltrarPorEntregadoParcialmente()
    {
        Assert.Contains("value=\"EntregadoParcialmente\"", Leer("src/Web/Backoffice/Pages/Envios/Index.cshtml"));
        Assert.Contains("EstadoFiltro == \"EntregadoParcialmente\"", Leer("src/Web/Backoffice/Pages/Envios/Index.cshtml.cs"));
    }

    [Fact]
    public void FiltroDelPortal_NombraElEstadoEntregadoParcialmente()
    {
        Assert.Contains("EstadoEnvioDto.EntregadoParcialmente => \"Entregado parcialmente\"", Leer("src/Web/PortalComercio.Client/Pages/Envios.razor"));
    }

    // ---------- Confirmación de la nueva versión del cuadro tarifario ----------

    [Fact]
    public void Tarifas_ConfirmarPublicacion_TienePanelConElTextoPedido()
    {
        var vista = Leer("src/Web/Backoffice/Pages/Tarifas/NuevaVersion.cshtml");

        Assert.Contains("id=\"modalConfirmarPublicacion\"", vista);
        Assert.Contains("¿Confirma la publicación de esta nueva versión? La versión anterior cerrará su vigencia y los nuevos envíos se tarifarán con estas reglas.", vista);
        Assert.Contains("bootstrap.Modal.getOrCreateInstance(modalEl)", vista);
    }
}
