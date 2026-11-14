using System.Diagnostics;
using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// Las pantallas con formularios cargan jQuery y su validación desde /lib (parcial _ValidationScriptsPartial, 16 pantallas). Las imágenes que construye
/// el pipeline salen del repositorio: si esos archivos están ignorados por Git no llegan a la imagen, el servidor responde 404 y la validación del lado del
/// cliente deja de funcionar sin que ninguna prueba HTTP lo note (hallazgo D-20 de la auditoría del 14/11, visto en un navegador real).
/// </summary>
public class LibreriasDelBackofficeTests
{
    private static readonly string[] Archivos =
    {
        "src/Web/Backoffice/wwwroot/lib/jquery/dist/jquery.min.js",
        "src/Web/Backoffice/wwwroot/lib/jquery-validation/dist/jquery.validate.min.js",
        "src/Web/Backoffice/wwwroot/lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js"
    };

    private static string RaizDelRepositorio()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return carpeta!.FullName;
    }

    [Fact]
    public void jQueryYSuValidacion_ExistenYNoEstanIgnoradosPorGit()
    {
        var raiz = RaizDelRepositorio();

        foreach (var archivo in Archivos)
        {
            Assert.True(File.Exists(Path.Combine(raiz, archivo)), $"Falta {archivo}.");

            using var git = Process.Start(new ProcessStartInfo("git", $"check-ignore -q \"{archivo}\"")
            {
                WorkingDirectory = raiz,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            })!;
            git.WaitForExit();

            // check-ignore -q: código 0 = el archivo está ignorado; 1 = no lo está
            Assert.True(git.ExitCode == 1, $"{archivo} está ignorado por .gitignore: no llega a la imagen del pipeline y el navegador recibe 404.");
        }
    }
}
