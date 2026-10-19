using System.Text;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Reportes;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria para reportes de gestión operativa y comercial (UC-13, RF-30).
/// </summary>
public class MockReportesApi : IReportesApi
{
    public Task<ResultadoReporteDto> GenerarReporteAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default)
    {
        var itemsCumplimiento = new List<ItemReporteCumplimientoDto>();
        var itemsMotivos = new List<ItemReporteMotivosDto>();
        var itemsVolumen = new List<ItemReporteVolumenDto>();
        var itemsLiquidacion = new List<ItemReporteLiquidacionDto>();

        switch (filtro.TipoReporte)
        {
            case "CumplimientoZona":
                itemsCumplimiento = new List<ItemReporteCumplimientoDto>
                {
                    new() { Agrupador = "Pocitos & Punta Carretas", DetalleAdicional = "Zona Urbana Sur", TotalAsignados = 320, EntregadosEnPlazo = 302, EntregadosConDemora = 12, NoEntregados = 6 },
                    new() { Agrupador = "Centro & Cordón", DetalleAdicional = "Zona Central Comercial", TotalAsignados = 280, EntregadosEnPlazo = 255, EntregadosConDemora = 18, NoEntregados = 7 },
                    new() { Agrupador = "Malvín & Punta Gorda", DetalleAdicional = "Zona Costera Este", TotalAsignados = 210, EntregadosEnPlazo = 198, EntregadosConDemora = 8, NoEntregados = 4 },
                    new() { Agrupador = "Prado & Bella Vista", DetalleAdicional = "Zona Norte", TotalAsignados = 195, EntregadosEnPlazo = 172, EntregadosConDemora = 15, NoEntregados = 8 },
                    new() { Agrupador = "Carrasco & Costa de Oro", DetalleAdicional = "Zona Suburbana Canelones", TotalAsignados = 165, EntregadosEnPlazo = 155, EntregadosConDemora = 7, NoEntregados = 3 }
                };
                break;

            case "CumplimientoRepartidor":
                itemsCumplimiento = new List<ItemReporteCumplimientoDto>
                {
                    new() { Agrupador = "Carlos Gómez", DetalleAdicional = "Móvil STX-1045 (Mercedes Sprinter)", TotalAsignados = 280, EntregadosEnPlazo = 268, EntregadosConDemora = 8, NoEntregados = 4 },
                    new() { Agrupador = "Juan Pérez", DetalleAdicional = "Móvil STX-1021 (Renault Kangoo)", TotalAsignados = 245, EntregadosEnPlazo = 232, EntregadosConDemora = 9, NoEntregados = 4 },
                    new() { Agrupador = "Martín Silva", DetalleAdicional = "Móvil STX-1077 (Fiat Fiorino)", TotalAsignados = 220, EntregadosEnPlazo = 204, EntregadosConDemora = 11, NoEntregados = 5 },
                    new() { Agrupador = "Lucía Fernández", DetalleAdicional = "Móvil STX-1029 (Peugeot Partner)", TotalAsignados = 215, EntregadosEnPlazo = 188, EntregadosConDemora = 19, NoEntregados = 8 }
                };
                break;

            case "CumplimientoComercio":
                itemsCumplimiento = new List<ItemReporteCumplimientoDto>
                {
                    new() { Agrupador = "Farmashop Uruguay", DetalleAdicional = "RUT 214567890012", TotalAsignados = 380, EntregadosEnPlazo = 362, EntregadosConDemora = 14, NoEntregados = 4 },
                    new() { Agrupador = "Tienda Inglesa Express", DetalleAdicional = "RUT 217894560018", TotalAsignados = 260, EntregadosEnPlazo = 248, EntregadosConDemora = 9, NoEntregados = 3 },
                    new() { Agrupador = "Supermercados Ta-Ta", DetalleAdicional = "RUT 210987650011", TotalAsignados = 310, EntregadosEnPlazo = 288, EntregadosConDemora = 15, NoEntregados = 7 },
                    new() { Agrupador = "Óptica San Roque", DetalleAdicional = "RUT 213334440019", TotalAsignados = 220, EntregadosEnPlazo = 194, EntregadosConDemora = 19, NoEntregados = 7 }
                };
                break;

            case "MotivosNoEntrega":
                itemsMotivos = new List<ItemReporteMotivosDto>
                {
                    new() { CodigoMotivo = "M-AUS", DescripcionMotivo = "Destinatario no responde o ausente en domicilio", Categoria = "ExternoDestinatario", CantidadOcurrencias = 28, PorcentajeDelTotal = 46.7m, ImpactoComun = "Reprogramación a segunda visita" },
                    new() { CodigoMotivo = "M-DIR", DescripcionMotivo = "Dirección inexacta, incompleta o número no visible", Categoria = "ExternoDestinatario", CantidadOcurrencias = 14, PorcentajeDelTotal = 23.3m, ImpactoComun = "Contacto telefónico y devolución" },
                    new() { CodigoMotivo = "M-REC", DescripcionMotivo = "Destinatario rechazó paquete por rotura de embalaje", Categoria = "Interno", CantidadOcurrencias = 8, PorcentajeDelTotal = 13.3m, ImpactoComun = "Apertura de Incidencia y devolución" },
                    new() { CodigoMotivo = "M-VEH", DescripcionMotivo = "Avería mecánica o siniestro durante la hoja de ruta", Categoria = "Interno", CantidadOcurrencias = 6, PorcentajeDelTotal = 10.0m, ImpactoComun = "Reasignación a vehículo de apoyo" },
                    new() { CodigoMotivo = "M-VIA", DescripcionMotivo = "Corte vial imprevisto o intransitabilidad climática", Categoria = "FuerzaMayor", CantidadOcurrencias = 4, PorcentajeDelTotal = 6.7m, ImpactoComun = "Reprogramación a siguiente turno" }
                };
                break;

            case "VolumenPeriodo":
                var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
                itemsVolumen = new List<ItemReporteVolumenDto>
                {
                    new() { Fecha = hoy.AddDays(-6), CantidadGuias = 142, CantidadBultos = 158, PesoTotalKg = 512.4m, VolumenTotalM3 = 6.8m, RutasDespachadas = 4 },
                    new() { Fecha = hoy.AddDays(-5), CantidadGuias = 168, CantidadBultos = 190, PesoTotalKg = 620.1m, VolumenTotalM3 = 8.1m, RutasDespachadas = 5 },
                    new() { Fecha = hoy.AddDays(-4), CantidadGuias = 155, CantidadBultos = 174, PesoTotalKg = 580.6m, VolumenTotalM3 = 7.4m, RutasDespachadas = 4 },
                    new() { Fecha = hoy.AddDays(-3), CantidadGuias = 178, CantidadBultos = 205, PesoTotalKg = 690.3m, VolumenTotalM3 = 9.2m, RutasDespachadas = 5 },
                    new() { Fecha = hoy.AddDays(-2), CantidadGuias = 192, CantidadBultos = 224, PesoTotalKg = 745.0m, VolumenTotalM3 = 10.1m, RutasDespachadas = 5 },
                    new() { Fecha = hoy.AddDays(-1), CantidadGuias = 110, CantidadBultos = 126, PesoTotalKg = 410.2m, VolumenTotalM3 = 5.3m, RutasDespachadas = 3 },
                    new() { Fecha = hoy, CantidadGuias = 85, CantidadBultos = 98, PesoTotalKg = 315.8m, VolumenTotalM3 = 4.2m, RutasDespachadas = 4 }
                };
                break;

            case "LiquidacionComercio":
                itemsLiquidacion = new List<ItemReporteLiquidacionDto>
                {
                    new()
                    {
                        ComercioId = Guid.NewGuid(),
                        RazonSocial = "Farmashop Uruguay S.A.",
                        Rut = "214567890012",
                        CantidadEnviosLiquidados = 380,
                        TarifaBaseTotal = 133000.00m,
                        RecargoPesoTotal = 14200.00m,
                        RecargoVolumenTotal = 0m,
                        DescuentoVolumenTotal = 5000.00m
                    },
                    new()
                    {
                        ComercioId = Guid.NewGuid(),
                        RazonSocial = "Supermercados Ta-Ta S.A.",
                        Rut = "210987650011",
                        CantidadEnviosLiquidados = 310,
                        TarifaBaseTotal = 108500.00m,
                        RecargoPesoTotal = 22400.00m,
                        RecargoVolumenTotal = 3500.00m,
                        DescuentoVolumenTotal = 3000.00m
                    },
                    new()
                    {
                        ComercioId = Guid.NewGuid(),
                        RazonSocial = "Tienda Inglesa Express",
                        Rut = "217894560018",
                        CantidadEnviosLiquidados = 260,
                        TarifaBaseTotal = 91000.00m,
                        RecargoPesoTotal = 11500.00m,
                        RecargoVolumenTotal = 0m,
                        DescuentoVolumenTotal = 2500.00m
                    },
                    new()
                    {
                        ComercioId = Guid.NewGuid(),
                        RazonSocial = "Óptica San Roque",
                        Rut = "213334440019",
                        CantidadEnviosLiquidados = 220,
                        TarifaBaseTotal = 77000.00m,
                        RecargoPesoTotal = 4800.00m,
                        RecargoVolumenTotal = 0m,
                        DescuentoVolumenTotal = 0m
                    }
                };
                break;

            default:
                break;
        }

        // Totales consolidados
        int totalProcesados = 0;
        int totalEntregados = 0;
        int totalNoEntregados = 0;
        decimal efectividadGlobal = 0;
        decimal totalFacturado = 0;

        if (itemsCumplimiento.Count > 0)
        {
            totalProcesados = itemsCumplimiento.Sum(i => i.TotalAsignados);
            totalEntregados = itemsCumplimiento.Sum(i => i.EntregadosEnPlazo + i.EntregadosConDemora);
            totalNoEntregados = itemsCumplimiento.Sum(i => i.NoEntregados);
            efectividadGlobal = totalProcesados > 0 ? Math.Round((decimal)itemsCumplimiento.Sum(i => i.EntregadosEnPlazo) / totalProcesados * 100, 1) : 0;
        }
        else if (itemsMotivos.Count > 0)
        {
            totalNoEntregados = itemsMotivos.Sum(m => m.CantidadOcurrencias);
            totalProcesados = totalNoEntregados;
        }
        else if (itemsVolumen.Count > 0)
        {
            totalProcesados = itemsVolumen.Sum(v => v.CantidadGuias);
        }
        else if (itemsLiquidacion.Count > 0)
        {
            totalProcesados = itemsLiquidacion.Sum(l => l.CantidadEnviosLiquidados);
            totalFacturado = itemsLiquidacion.Sum(l => l.TotalFacturado);
        }

        var resultado = new ResultadoReporteDto
        {
            Filtro = filtro,
            FechaGeneracion = DateTime.UtcNow,
            TotalEnviosProcesados = totalProcesados,
            TasaEfectividadGlobal = efectividadGlobal,
            TotalEntregados = totalEntregados,
            TotalNoEntregados = totalNoEntregados,
            TotalFacturadoLiquidacion = totalFacturado,
            ItemsCumplimiento = itemsCumplimiento,
            ItemsMotivos = itemsMotivos,
            ItemsVolumen = itemsVolumen,
            ItemsLiquidacion = itemsLiquidacion
        };

        return Task.FromResult(resultado);
    }

    public async Task<byte[]> ExportarReporteCsvAsync(FiltroReporteDto filtro, CancellationToken cancelacion = default)
    {
        var datos = await GenerarReporteAsync(filtro, cancelacion);
        var sb = new StringBuilder();

        // Encabezado general con UTF-8
        sb.AppendLine($"Reporte de Gestión;Tipo: {filtro.TipoReporte};Periodo: {filtro.FechaDesde:dd/MM/yyyy} a {filtro.FechaHasta:dd/MM/yyyy}");
        sb.AppendLine($"Generado el;{DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine();

        if (datos.ItemsCumplimiento.Count > 0)
        {
            sb.AppendLine("Entidad/Zona;Detalle;Asignados;En Plazo;Con Demora;No Entregados;% Efectividad SLA;% Entrega Total");
            foreach (var item in datos.ItemsCumplimiento)
            {
                sb.AppendLine($"\"{item.Agrupador}\";\"{item.DetalleAdicional}\";{item.TotalAsignados};{item.EntregadosEnPlazo};{item.EntregadosConDemora};{item.NoEntregados};{item.PorcentajeEfectividad}%;{item.PorcentajeEntregaTotal}%");
            }
        }
        else if (datos.ItemsMotivos.Count > 0)
        {
            sb.AppendLine("Codigo Motivo;Descripcion;Categoria;Ocurrencias;% del Total;Impacto Operativo");
            foreach (var m in datos.ItemsMotivos)
            {
                sb.AppendLine($"\"{m.CodigoMotivo}\";\"{m.DescripcionMotivo}\";\"{m.Categoria}\";{m.CantidadOcurrencias};{m.PorcentajeDelTotal}%;\"{m.ImpactoComun}\"");
            }
        }
        else if (datos.ItemsVolumen.Count > 0)
        {
            sb.AppendLine("Fecha;Guias;Bultos;Peso Total (kg);Volumen Total (m3);Rutas Despachadas");
            foreach (var v in datos.ItemsVolumen)
            {
                sb.AppendLine($"{v.Fecha:dd/MM/yyyy};{v.CantidadGuias};{v.CantidadBultos};{v.PesoTotalKg};{v.VolumenTotalM3};{v.RutasDespachadas}");
            }
        }
        else if (datos.ItemsLiquidacion.Count > 0)
        {
            sb.AppendLine("Comercio;RUT;Envios;Tarifa Base ($);Recargo Peso ($);Subtotal ($);IVA 22% ($);Total Facturado ($)");
            foreach (var l in datos.ItemsLiquidacion)
            {
                sb.AppendLine($"\"{l.RazonSocial}\";\"{l.Rut}\";{l.CantidadEnviosLiquidados};{l.TarifaBaseTotal:F2};{l.RecargoPesoTotal:F2};{l.Subtotal:F2};{l.Iva:F2};{l.TotalFacturado:F2}");
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }
}
