using Xunit;
using System.Text;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;

namespace UltimaMilla.Frontend.Tests;

public class ReporteCsvTests
{
    private static string Texto(byte[] csv) => Encoding.UTF8.GetString(csv);

    [Fact]
    public void Csv_EmpiezaConBomUtf8ParaQueExcelLoAbraBien()
    {
        var csv = ReporteCsv.Generar(new ResultadoReporteDto());

        Assert.Equal(Encoding.UTF8.GetPreamble(), csv.Take(3).ToArray());
    }

    [Fact]
    public void Csv_Cumplimiento_IncluyeUnaFilaPorAgrupadorConSusPorcentajes()
    {
        var reporte = new ResultadoReporteDto
        {
            Filtro = new FiltroReporteDto { TipoReporte = "CumplimientoZona" },
            ItemsCumplimiento = { new ItemReporteCumplimientoDto { Agrupador = "Centro", TotalAsignados = 10, EntregadosEnPlazo = 8, EntregadosConDemora = 1, NoEntregados = 1 } }
        };

        var texto = Texto(ReporteCsv.Generar(reporte));

        Assert.Contains("Entidad/Zona;Detalle;Asignados", texto);
        Assert.Contains("\"Centro\";\"\";10;8;1;1;80%;90%", texto);
    }

    [Fact]
    public void Csv_Liquidacion_DesgloSaTarifaIvaYTotal()
    {
        var reporte = new ResultadoReporteDto
        {
            ItemsLiquidacion =
            {
                new ItemReporteLiquidacionDto { RazonSocial = "MegaStore", Rut = "2100", CantidadEnviosLiquidados = 2, TarifaBaseTotal = 200m, RecargoPesoTotal = 20m, DescuentoVolumenTotal = 20m }
            }
        };

        var texto = Texto(ReporteCsv.Generar(reporte));

        Assert.Contains("\"MegaStore\";\"2100\";2;200.00;20.00;0.00;20.00;200.00;44.00;244.00", texto);
    }

    [Fact]
    public void Csv_NoDejaQueUnTextoIngresadoPorUsuariosSeEjecuteComoFormula()
    {
        var reporte = new ResultadoReporteDto
        {
            ItemsMotivos = { new ItemReporteMotivosDto { CodigoMotivo = "X", DescripcionMotivo = "=HYPERLINK(\"http://malo\")", CantidadOcurrencias = 1 } }
        };

        var texto = Texto(ReporteCsv.Generar(reporte));

        Assert.DoesNotContain(";\"=HYPERLINK", texto);
        Assert.Contains("\"'=HYPERLINK(\"\"http://malo\"\")\"", texto);
    }

    [Fact]
    public void Csv_UsaPuntoDecimalSinImportarLaCulturaDelEquipo()
    {
        var reporte = new ResultadoReporteDto
        {
            ItemsVolumen = { new ItemReporteVolumenDto { Fecha = new DateOnly(2026, 10, 28), CantidadGuias = 3, PesoTotalKg = 12.5m, VolumenTotalM3 = 0.125m } }
        };

        var texto = Texto(ReporteCsv.Generar(reporte));

        Assert.Contains("28/10/2026;3;0;12.50;0.13;0", texto);
    }
}
