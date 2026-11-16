using System.Text.RegularExpressions;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// En la clasificación del depósito la zona es obligatoria y la prioridad y el tipo no (el servidor aplica "Normal" y "Estándar" si faltan), pero el formulario
/// no lo decía y el navegador dejaba enviar "Sin zona asignada" para recién recibir el rechazo del servidor.
/// </summary>
public class ClasificacionDelDepositoTests
{
    private static string Vista()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return File.ReadAllText(Path.Combine(carpeta!.FullName, "src/Web/Backoffice/Pages/Deposito/Clasificacion.cshtml"));
    }

    [Fact]
    public void LaZona_EsObligatoriaYElNavegadorNoDejaEnviarSinElla()
    {
        var vista = Vista();

        Assert.Contains("Zona de Cobertura *", vista);
        var corte = vista.IndexOf("id=\"sel_zona_@i\"", StringComparison.Ordinal);
        Assert.True(corte >= 0, "No se encontró el selector de zona.");
        Assert.Matches(@"required", vista.Substring(corte, 120));
    }

    [Fact]
    public void LaPrioridadYElTipo_DicenQueSonOpcionalesYCualEsSuValorPorDefecto()
    {
        var vista = Vista();

        Assert.Matches(@"Prioridad\s*<span[^>]*>\(opcional, por defecto Normal\)</span>", vista);
        Assert.Matches(@"Tipo Operativo\s*<span[^>]*>\(opcional, por defecto Est.ndar\)</span>", vista);
    }

    [Fact]
    public void ConTodosLosBultosClasificados_LaPaginaVuelveALaListaSinEnvioSeleccionado()
    {
        // Antes se volvía a mostrar el formulario con los datos ya guardados y se podía seguir editando una clasificación terminada.
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        var codigo = File.ReadAllText(Path.Combine(carpeta!.FullName, "src/Web/Backoffice/Pages/Deposito/Clasificacion.cshtml.cs"));

        var listo = codigo.IndexOf("if (Resultado.ListoParaHojaDeRuta)", StringComparison.Ordinal);
        Assert.True(listo >= 0, "No se encontró el caso de clasificación completa.");
        Assert.Contains("RedirectToPage(\"/Deposito/Clasificacion\"", codigo.Substring(listo, 600));
    }
}
