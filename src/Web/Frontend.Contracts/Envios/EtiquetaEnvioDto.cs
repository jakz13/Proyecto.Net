using System.Net;
using System.Text;

namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Datos de una etiqueta física individual correspondiente a un bulto de un envío admitido.
/// </summary>
public record EtiquetaBultoDto(
    Guid EnvioId,
    string CodigoSeguimiento,
    string IdentificacionBulto,
    int NumeroBulto,
    int TotalBultos,
    string ComercioRemitente,
    string OperadorLogistico,
    string NombreDestinatario,
    string DireccionDestino,
    string? TelefonoDestinatario,
    string ModalidadServicio,
    decimal PesoKg,
    string? Observaciones,
    DateTime FechaAdmision);

/// <summary>
/// Resultado de la generación de etiquetas para uno o más envíos seleccionados en estado Admitido.
/// </summary>
public record ResultadoGeneracionEtiquetasDto(
    IReadOnlyList<EtiquetaBultoDto> Etiquetas,
    int TotalEnviosSeleccionados,
    int TotalEtiquetasGeneradas,
    string NombreArchivoDescarga,
    string ContenidoHtmlDescargable);

/// <summary>
/// Generador de representación visual y archivo descargable de etiquetas logísticas para adherir a los bultos.
/// </summary>
public static class GeneradorDocumentoEtiquetas
{
    /// <summary>
    /// Calcula las posiciones y anchos de barras determinísticas a partir del identificador del bulto.
    /// </summary>
    public static IReadOnlyList<(int PosicionX, int Ancho)> CalcularPatronBarras(string codigo)
    {
        var texto = string.IsNullOrWhiteSpace(codigo) ? "TRK-0000" : codigo.Trim().ToUpperInvariant();
        var barras = new List<(int PosicionX, int Ancho)>();
        var cursorX = 8;

        // Guarda de inicio
        barras.Add((cursorX, 3));
        cursorX += 5;
        barras.Add((cursorX, 1));
        cursorX += 4;

        foreach (var caracter in texto)
        {
            var valor = (int)caracter;
            var ancho1 = (valor % 3) + 1;
            var espacio1 = ((valor / 3) % 2) + 2;
            var ancho2 = ((valor / 7) % 3) + 1;
            var espacio2 = ((valor / 11) % 2) + 2;

            barras.Add((cursorX, ancho1));
            cursorX += ancho1 + espacio1;

            barras.Add((cursorX, ancho2));
            cursorX += ancho2 + espacio2;
        }

        // Guarda de cierre
        barras.Add((cursorX, 2));
        cursorX += 4;
        barras.Add((cursorX, 3));

        return barras;
    }

    /// <summary>
    /// Construye un documento HTML autónomo listo para descargar o imprimir en formato de etiqueta térmica (100x150 mm).
    /// </summary>
    public static string ConstruirHtmlDescargable(IReadOnlyList<EtiquetaBultoDto> etiquetas)
    {
        ArgumentNullException.ThrowIfNull(etiquetas);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine("  <title>Etiquetas de Envío — Plataforma Última Milla</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif; background: #f1f5f9; color: #0f172a; padding: 24px; }");
        sb.AppendLine("    .barra-acciones { max-width: 820px; margin: 0 auto 20px auto; display: flex; justify-content: space-between; align-items: center; background: #ffffff; padding: 14px 20px; border-radius: 8px; border: 1px solid #cbd5e1; }");
        sb.AppendLine("    .btn-imprimir { background: #0f172a; color: #ffffff; border: none; padding: 8px 18px; border-radius: 6px; font-weight: 600; cursor: pointer; font-size: 14px; }");
        sb.AppendLine("    .grilla-etiquetas { display: flex; flex-wrap: wrap; gap: 24px; justify-content: center; max-width: 860px; margin: 0 auto; }");
        sb.AppendLine("    .etiqueta-fisica { width: 380px; min-height: 520px; background: #ffffff; border: 2px solid #0f172a; border-radius: 6px; padding: 16px; display: flex; flex-direction: column; justify-content: space-between; page-break-inside: avoid; }");
        sb.AppendLine("    .etiqueta-cabecera { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #0f172a; padding-bottom: 10px; margin-bottom: 12px; }");
        sb.AppendLine("    .operador-nombre { font-size: 16px; font-weight: 800; text-transform: uppercase; letter-spacing: 0.03em; }");
        sb.AppendLine("    .modalidad-texto { font-size: 14px; font-weight: 800; text-transform: uppercase; }");
        sb.AppendLine("    .modalidad-express { color: #dc2626; }");
        sb.AppendLine("    .modalidad-estandar { color: #0f172a; }");
        sb.AppendLine("    .bloque-seccion { border-bottom: 1px solid #cbd5e1; padding-bottom: 10px; margin-bottom: 10px; }");
        sb.AppendLine("    .etiqueta-campo { font-size: 11px; font-weight: 700; text-transform: uppercase; color: #475569; margin-bottom: 3px; }");
        sb.AppendLine("    .destinatario-nombre { font-size: 18px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }");
        sb.AppendLine("    .destinatario-direccion { font-size: 14px; font-weight: 600; color: #1e293b; line-height: 1.35; }");
        sb.AppendLine("    .destinatario-meta { font-size: 12px; color: #334155; margin-top: 4px; }");
        sb.AppendLine("    .fila-datos { display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 10px; border-bottom: 1px solid #cbd5e1; padding-bottom: 10px; }");
        sb.AppendLine("    .dato-valor { font-weight: 700; font-family: Consolas, monospace; font-size: 14px; }");
        sb.AppendLine("    .bloque-codigo { text-align: center; padding-top: 6px; }");
        sb.AppendLine("    .codigo-seguimiento { font-family: Consolas, monospace; font-size: 18px; font-weight: 800; letter-spacing: 0.06em; margin-bottom: 6px; }");
        sb.AppendLine("    .codigo-bulto { font-family: Consolas, monospace; font-size: 13px; font-weight: 700; color: #334155; margin-top: 4px; }");
        sb.AppendLine("    .pie-corte { font-size: 11px; color: #64748b; text-align: center; border-top: 1px dashed #94a3b8; padding-top: 8px; margin-top: 10px; }");
        sb.AppendLine("    @media print { body { background: #ffffff; padding: 0; } .barra-acciones { display: none; } .etiqueta-fisica { margin-bottom: 16px; } }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"barra-acciones\">");
        sb.AppendLine($"    <div><strong>Etiquetas listas para adherir al bulto</strong> · Total generadas: {etiquetas.Count}</div>");
        sb.AppendLine("    <button class=\"btn-imprimir\" onclick=\"window.print()\">Imprimir etiquetas</button>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"grilla-etiquetas\">");

        foreach (var et in etiquetas)
        {
            var esExpress = string.Equals(et.ModalidadServicio, "Express", StringComparison.OrdinalIgnoreCase);
            var claseModalidad = esExpress ? "modalidad-texto modalidad-express" : "modalidad-texto modalidad-estandar";
            var barras = CalcularPatronBarras(et.IdentificacionBulto);
            var anchoSvg = barras.Count > 0 ? barras[^1].PosicionX + barras[^1].Ancho + 8 : 260;

            sb.AppendLine("    <div class=\"etiqueta-fisica\">");
            sb.AppendLine("      <div>");
            sb.AppendLine("        <div class=\"etiqueta-cabecera\">");
            sb.AppendLine("          <div>");
            sb.AppendLine($"            <div class=\"operador-nombre\">{Escapar(et.OperadorLogistico)}</div>");
            sb.AppendLine($"            <div style=\"font-size: 11px; color: #475569;\">Admisión: {et.FechaAdmision.ToLocalTime():dd/MM/yyyy HH:mm}</div>");
            sb.AppendLine("          </div>");
            sb.AppendLine($"          <div class=\"{claseModalidad}\">{Escapar(et.ModalidadServicio)}</div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"bloque-seccion\">");
            sb.AppendLine("          <div class=\"etiqueta-campo\">Remitente (Comercio)</div>");
            sb.AppendLine($"          <div style=\"font-size: 13px; font-weight: 700;\">{Escapar(et.ComercioRemitente)}</div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"bloque-seccion\">");
            sb.AppendLine("          <div class=\"etiqueta-campo\">Destinatario y Dirección de Entrega</div>");
            sb.AppendLine($"          <div class=\"destinatario-nombre\">{Escapar(et.NombreDestinatario)}</div>");
            sb.AppendLine($"          <div class=\"destinatario-direccion\">{Escapar(et.DireccionDestino)}</div>");
            if (!string.IsNullOrWhiteSpace(et.TelefonoDestinatario))
            {
                sb.AppendLine($"          <div class=\"destinatario-meta\">Tel: {Escapar(et.TelefonoDestinatario)}</div>");
            }
            if (!string.IsNullOrWhiteSpace(et.Observaciones))
            {
                sb.AppendLine($"          <div class=\"destinatario-meta\">Ref: {Escapar(et.Observaciones)}</div>");
            }
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"fila-datos\">");
            sb.AppendLine($"          <div><span class=\"etiqueta-campo\">Bulto</span><div class=\"dato-valor\">{et.NumeroBulto} de {et.TotalBultos}</div></div>");
            sb.AppendLine($"          <div><span class=\"etiqueta-campo\">Peso Declarado</span><div class=\"dato-valor\">{et.PesoKg:F2} kg</div></div>");
            sb.AppendLine($"          <div><span class=\"etiqueta-campo\">Estado</span><div class=\"dato-valor\">Admitido</div></div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            sb.AppendLine("      <div>");
            sb.AppendLine("        <div class=\"bloque-codigo\">");
            sb.AppendLine($"          <div class=\"codigo-seguimiento\">{Escapar(et.CodigoSeguimiento)}</div>");
            sb.AppendLine($"          <svg viewBox=\"0 0 {anchoSvg} 64\" width=\"100%\" height=\"64\" xmlns=\"http://www.w3.org/2000/svg\" aria-label=\"Código de barras {Escapar(et.IdentificacionBulto)}\">");
            sb.AppendLine($"            <rect width=\"{anchoSvg}\" height=\"64\" fill=\"#ffffff\" />");
            foreach (var (posX, ancho) in barras)
            {
                sb.AppendLine($"            <rect x=\"{posX}\" y=\"4\" width=\"{ancho}\" height=\"56\" fill=\"#0f172a\" />");
            }
            sb.AppendLine("          </svg>");
            sb.AppendLine($"          <div class=\"codigo-bulto\">ID Bulto: {Escapar(et.IdentificacionBulto)}</div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <div class=\"pie-corte\">Adherir esta etiqueta en la cara superior del bulto sin cubrir el código de barras</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </div>");
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string Escapar(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);
}

