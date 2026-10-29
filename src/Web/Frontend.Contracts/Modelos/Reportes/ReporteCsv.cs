using System.Globalization;
using System.Text;

namespace UltimaMilla.Frontend.Contracts.Modelos.Reportes;

/// <summary>
/// Exportación a CSV (compatible con Excel: UTF-8 con BOM y punto y coma como separador) de un reporte ya generado.
/// La comparten el cliente real y el simulado para que la descarga sea idéntica.
/// </summary>
public static class ReporteCsv
{
    public static byte[] Generar(ResultadoReporteDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var filtro = datos.Filtro;
        var sb = new StringBuilder();

        sb.AppendLine($"Reporte de Gestión;Tipo: {filtro.TipoReporte};Periodo: {filtro.FechaDesde:dd/MM/yyyy} a {filtro.FechaHasta:dd/MM/yyyy}");
        sb.AppendLine($"Generado el;{datos.FechaGeneracion:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine();

        if (datos.ItemsCumplimiento.Count > 0)
        {
            sb.AppendLine("Entidad/Zona;Detalle;Asignados;En Plazo;Con Demora;No Entregados;% Efectividad SLA;% Entrega Total");
            foreach (var item in datos.ItemsCumplimiento)
                sb.AppendLine(Fila(Texto(item.Agrupador), Texto(item.DetalleAdicional), item.TotalAsignados, item.EntregadosEnPlazo,
                    item.EntregadosConDemora, item.NoEntregados, Porcentaje(item.PorcentajeEfectividad), Porcentaje(item.PorcentajeEntregaTotal)));
        }
        else if (datos.ItemsMotivos.Count > 0)
        {
            sb.AppendLine("Codigo Motivo;Descripcion;Categoria;Ocurrencias;% del Total;Impacto Operativo");
            foreach (var m in datos.ItemsMotivos)
                sb.AppendLine(Fila(Texto(m.CodigoMotivo), Texto(m.DescripcionMotivo), Texto(m.Categoria), m.CantidadOcurrencias,
                    Porcentaje(m.PorcentajeDelTotal), Texto(m.ImpactoComun)));
        }
        else if (datos.ItemsVolumen.Count > 0)
        {
            sb.AppendLine("Fecha;Guias;Bultos;Peso Total (kg);Volumen Total (m3);Rutas Despachadas");
            foreach (var v in datos.ItemsVolumen)
                sb.AppendLine(Fila(v.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), v.CantidadGuias, v.CantidadBultos,
                    Numero(v.PesoTotalKg), Numero(v.VolumenTotalM3), v.RutasDespachadas));
        }
        else if (datos.ItemsLiquidacion.Count > 0)
        {
            sb.AppendLine("Comercio;RUT;Envios;Tarifa Base ($);Recargo Peso ($);Recargo Volumen ($);Descuento ($);Subtotal ($);IVA 22% ($);Total Facturado ($)");
            foreach (var l in datos.ItemsLiquidacion)
                sb.AppendLine(Fila(Texto(l.RazonSocial), Texto(l.Rut), l.CantidadEnviosLiquidados, Numero(l.TarifaBaseTotal), Numero(l.RecargoPesoTotal),
                    Numero(l.RecargoVolumenTotal), Numero(l.DescuentoVolumenTotal), Numero(l.Subtotal), Numero(l.Iva), Numero(l.TotalFacturado)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Fila(params object[] celdas) =>
        string.Join(';', celdas.Select(c => Convert.ToString(c, CultureInfo.InvariantCulture)));

    private static string Numero(decimal valor) => valor.ToString("F2", CultureInfo.InvariantCulture);

    private static string Porcentaje(decimal valor) => valor.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>Entrecomilla el texto y neutraliza el prefijo de fórmula para que Excel no ejecute contenido ingresado por usuarios.</summary>
    private static string Texto(string? valor)
    {
        var limpio = (valor ?? string.Empty).Replace("\"", "\"\"");
        if (limpio.Length > 0 && limpio[0] is '=' or '+' or '-' or '@')
            limpio = "'" + limpio;

        return $"\"{limpio}\"";
    }
}
