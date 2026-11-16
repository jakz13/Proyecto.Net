using Xunit;

namespace UltimaMilla.Backoffice.Tests;

/// <summary>
/// El panel "Línea de Vida y Mapa de Seguimiento del Envío" es un modal: el tiempo real de la página no toca los modales, así que los
/// "Eventos Históricos de Auditoría" quedaban congelados con lo que se pidió al abrirlo. Con el panel abierto debe volver a pedirse todo
/// cada vez que la página se actualiza por un cambio del sistema.
/// </summary>
public class PanelDeLineaDeVidaEnVivoTests
{
    private static string Leer(string relativa)
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "docker-compose.prod.yml")))
            carpeta = carpeta.Parent;
        Assert.NotNull(carpeta);
        return File.ReadAllText(Path.Combine(carpeta!.FullName, relativa));
    }

    [Fact]
    public void ElPanelAbierto_SeActualizaConCadaCambioDelSistema()
    {
        var vista = Leer("src/Web/Backoffice/Pages/Envios/Index.cshtml");

        var escucha = vista.IndexOf("addEventListener('tiempo-real:actualizado'", StringComparison.Ordinal);
        Assert.True(escucha >= 0, "El panel no escucha la actualización en vivo de la página.");
        var cuerpo = vista.Substring(escucha, 1500);

        Assert.Contains("cargarEventosHistorial(envioId, true)", cuerpo);   // eventos históricos de auditoría
        Assert.Contains("cargarEvidencias(envioId, true)", cuerpo);
        Assert.Contains("cargarBultosEnContenedor(envioId, 'listaBultosEnHistorial', true)", cuerpo);
        Assert.Contains("renderizarMapaSeguimientoEnvio", cuerpo);           // mapa
    }

    [Fact]
    public void AlCerrarElPanel_DejaDeActualizarse()
    {
        var vista = Leer("src/Web/Backoffice/Pages/Envios/Index.cshtml");

        Assert.Contains("'hidden.bs.modal', function () { historialAbierto = null; }", vista);
    }

    [Fact]
    public void ElPortalDelComercio_PoneAlDiaLaLineaDeVidaDelEnvioAbierto()
    {
        var pagina = Leer("src/Web/PortalComercio.Client/Pages/Envios.razor");

        var manejador = pagina.IndexOf("private async Task AlCambiarEstadoEnvio", StringComparison.Ordinal);
        Assert.True(manejador >= 0);
        Assert.Contains("RefrescarDetalleDelEnvioAbiertoAsync", pagina.Substring(manejador, 2500));
        Assert.Contains("ObtenerHistorialEnvioAsync", pagina.Substring(pagina.IndexOf("private async Task RefrescarDetalleDelEnvioAbiertoAsync", StringComparison.Ordinal), 700));
    }
}
