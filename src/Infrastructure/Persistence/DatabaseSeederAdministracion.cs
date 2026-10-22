using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence;

/// <summary>
/// Datos semilla de la administración del operador (zonas, identidad visual y cola de mensajes fallidos); perfiles y usuarios los siembra DatabaseSeeder.
/// Es idempotente: cada bloque verifica su existencia, por lo que también completa bases creadas antes de estos módulos.
/// </summary>
public static class DatabaseSeederAdministracion
{
    public static readonly Guid ZonaCentroId = Guid.Parse("a3000000-0000-0000-0000-000000000001");
    public static readonly Guid ZonaCostaId = Guid.Parse("a3000000-0000-0000-0000-000000000002");
    public static readonly Guid ZonaPeriferiaId = Guid.Parse("a3000000-0000-0000-0000-000000000003");

    public static readonly Guid IdentidadVisualId = Guid.Parse("a4000000-0000-0000-0000-000000000001");

    public static async Task SembrarAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        await SembrarComercioAsync(context, cancellationToken);
        await SembrarZonasAsync(context, cancellationToken);
        await SembrarIdentidadVisualAsync(context, cancellationToken);
        await SembrarMensajesFallidosAsync(context, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DatabaseSeeder: datos de administración del operador verificados (zonas, identidad visual, cola de fallidos).");
    }

    /// <summary>Completa los datos de contacto del comercio demo, que se sembró antes de existir nombre de fantasía y dirección.</summary>
    private static async Task SembrarComercioAsync(AppDbContext context, CancellationToken ct)
    {
        var comercio = await context.Comercios.FirstOrDefaultAsync(c => c.Id == DatabaseSeeder.DefaultComercioId, ct);
        if (comercio is not null && string.IsNullOrWhiteSpace(comercio.NombreFantasia))
            comercio.ActualizarPerfil(comercio.RazonSocial, "MegaStore", comercio.Email, comercio.Telefono, "Av. Italia 3456, Montevideo");
    }

    private static async Task SembrarZonasAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.ZonasCobertura.AnyAsync(z => z.OperadorId == DatabaseSeeder.DefaultOperadorId, ct))
            return;

        var operadorId = DatabaseSeeder.DefaultOperadorId;

        var centro = new ZonaCobertura(ZonaCentroId, operadorId, "Montevideo Centro y Cordón",
            "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9310, -56.1600], [-34.9190, -56.2080], [-34.9080, -56.2120]]");
        centro.ActualizarDatos(centro.Nombre, "#2563eb");
        AgregarFranjasLaborales(centro, 30);

        var costa = new ZonaCobertura(ZonaCostaId, operadorId, "Costa Este (Pocitos - Carrasco)",
            "[[-34.8800, -56.0500], [-34.8500, -55.9700], [-34.8850, -55.9500], [-34.9050, -56.0350], [-34.8800, -56.0500]]");
        costa.ActualizarDatos(costa.Nombre, "#16a34a");
        AgregarFranjasLaborales(costa, 25);

        var periferia = new ZonaCobertura(ZonaPeriferiaId, operadorId, "Periferia Norte",
            "[[-34.8550, -56.2150], [-34.8450, -56.1850], [-34.8750, -56.1750], [-34.8850, -56.2050], [-34.8550, -56.2150]]");
        periferia.ActualizarDatos(periferia.Nombre, "#f59e0b");
        AgregarFranjasLaborales(periferia, 15);

        await context.ZonasCobertura.AddRangeAsync(new[] { centro, costa, periferia }, ct);
    }

    /// <summary>Franjas de mañana y tarde de lunes a viernes.</summary>
    private static void AgregarFranjasLaborales(ZonaCobertura zona, int capacidad)
    {
        foreach (var dia in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            zona.AgregarFranjaHoraria(new FranjaHoraria(Guid.NewGuid(), zona.Id, dia, new TimeOnly(9, 0), new TimeOnly(13, 0), capacidad));
            zona.AgregarFranjaHoraria(new FranjaHoraria(Guid.NewGuid(), zona.Id, dia, new TimeOnly(14, 0), new TimeOnly(18, 0), capacidad));
        }
    }

    private static async Task SembrarIdentidadVisualAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.IdentidadesVisuales.AnyAsync(i => i.OperadorId == DatabaseSeeder.DefaultOperadorId, ct))
            return;

        var identidad = new IdentidadVisual(IdentidadVisualId, DatabaseSeeder.DefaultOperadorId, "#1D4ED8", "#F97316",
            "contacto@ultimamilla.com.uy", "+598 2900 1122");
        identidad.ActualizarComunicaciones("https://ultimamilla.com.uy", "Gracias por confiar en nosotros. Seguí tu envío en línea.");
        await context.IdentidadesVisuales.AddAsync(identidad, ct);
    }

    private static async Task SembrarMensajesFallidosAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.MensajesFallidos.AnyAsync(ct))
            return;

        var operadorId = DatabaseSeeder.DefaultOperadorId;
        var mensajes = new[]
        {
            new MensajeFallidoCola(Guid.Parse("a5000000-0000-0000-0000-000000000001"), "NotificacionWebhookComercio",
                "{\"envio\":\"ENV-2026-00108\",\"evento\":\"Entregado\"}", "HttpRequestException: 504 Gateway Timeout al invocar tiendacentral.megastore.com.uy",
                "Fallido", "cola.webhooks.comercios", operadorId),
            new MensajeFallidoCola(Guid.Parse("a5000000-0000-0000-0000-000000000002"), "GeneracionEtiquetaZPL",
                "{\"envio\":\"ENV-2026-00111\",\"bultos\":1}", "TimeoutException: la impresora de etiquetas ZPL no respondió",
                "Fallido", "cola.etiquetas", operadorId),
            new MensajeFallidoCola(Guid.Parse("a5000000-0000-0000-0000-000000000003"), "SincronizacionOfflineRepartidor",
                "{\"repartidor\":\"66666666-6666-6666-6666-666666666666\",\"operaciones\":3}", "ConcurrencyException: el paquete ya fue modificado",
                "Fallido", "cola.sincronizacion.movil", operadorId)
        };
        await context.MensajesFallidos.AddRangeAsync(mensajes, ct);
    }
}
