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

        if (datos.ItemsCumplimiento.Count > 0)
        {
            sb.AppendLine("Entidad/Zona;Detalle;Total Asignados;En Plazo (SLA);Con Demora;No Entregados;% Cumplimiento SLA;Tasa Entrega Total;Nivel de Rendimiento");
            foreach (var item in datos.ItemsCumplimiento)
            {
                var nivel = item.PorcentajeEfectividad >= 95m ? "Excelente" : item.PorcentajeEfectividad >= 85m ? "Aceptable" : "En Riesgo";
                sb.AppendLine(Fila(Texto(item.Agrupador), Texto(item.DetalleAdicional), item.TotalAsignados, item.EntregadosEnPlazo,
                    item.EntregadosConDemora, item.NoEntregados, Porcentaje(item.PorcentajeEfectividad), Porcentaje(item.PorcentajeEntregaTotal), Texto(nivel)));
            }
        }
        else if (datos.ItemsMotivos.Count > 0)
        {
            sb.AppendLine("Codigo Motivo;Descripcion del Motivo;Categoria;Ocurrencias;% del Total;Impacto en la Operacion");
            foreach (var m in datos.ItemsMotivos)
                sb.AppendLine(Fila(Texto(m.CodigoMotivo), Texto(m.DescripcionMotivo), Texto(m.Categoria), m.CantidadOcurrencias,
                    Porcentaje(m.PorcentajeDelTotal), Texto(m.ImpactoComun)));
        }
        else if (datos.ItemsVolumen.Count > 0)
        {
            sb.AppendLine("Fecha;Guias Procesadas;Bultos Fisicos;Peso Total (kg);Volumen Total (m3);Hojas de Ruta;Promedio Bultos por Ruta");
            foreach (var v in datos.ItemsVolumen)
            {
                var prom = v.RutasDespachadas > 0 ? (decimal)v.CantidadBultos / v.RutasDespachadas : 0;
                sb.AppendLine(Fila(v.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), v.CantidadGuias, v.CantidadBultos,
                    Numero(v.PesoTotalKg), Numero(v.VolumenTotalM3), v.RutasDespachadas, prom.ToString("F1", CultureInfo.InvariantCulture)));
            }
        }
        else if (datos.ItemsLiquidacion.Count > 0)
        {
            sb.AppendLine("Comercio Cliente;RUT;Envios Facturados;Tarifa Base ($);Recargo Peso ($);Recargo Volumen ($);Bonificacion ($);Subtotal ($);IVA 22% ($);Total Facturado ($)");
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
