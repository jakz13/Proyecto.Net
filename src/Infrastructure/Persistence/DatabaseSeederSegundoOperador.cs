using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Infrastructure.Services;

namespace UltimaMilla.Infrastructure.Persistence;

/// <summary>
/// Segundo operador logístico de la demo, con configuración propia (reglas, tarifas, zona, comercio y usuarios distintos a los del
/// operador por defecto). Permite demostrar y probar el aislamiento entre inquilinos (ADR-002). Todos los datos son sintéticos.
/// Es idempotente: cada bloque verifica su existencia antes de insertar.
/// </summary>
public static class DatabaseSeederSegundoOperador
{
    /// <summary>Clave de integración del comercio del segundo operador; en la base solo se guarda su huella.</summary>
    public const string ClaveApiDemo = "dev-api-key-expressmarket-2026";

    public static readonly Guid OperadorId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid ComercioId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    public static readonly Guid ComercioOperadorId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    public static readonly Guid ReglaOperativaId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
    public static readonly Guid CuadroTarifarioId = Guid.Parse("b4000000-0000-0000-0000-000000000001");
    public static readonly Guid ReglaTarifaEstandarId = Guid.Parse("b4000000-0001-0000-0000-000000000001");
    public static readonly Guid ZonaId = Guid.Parse("b5000000-0000-0000-0000-000000000001");
    public static readonly Guid IdentidadVisualId = Guid.Parse("b6000000-0000-0000-0000-000000000001");
    public static readonly Guid UsuarioAdministradorId = Guid.Parse("b7000000-0000-0000-0000-000000000001");
    public static readonly Guid UsuarioDespachadorId = Guid.Parse("b7000000-0000-0000-0000-000000000002");
    public static readonly Guid Envio1Id = Guid.Parse("b8000000-0000-0000-0000-000000000001");
    public static readonly Guid Envio2Id = Guid.Parse("b8000000-0000-0000-0000-000000000002");

    public const string EmailAdministrador = "admin.express@ultimamilla.com.uy";
    public const string EmailDespachador = "despacho.express@ultimamilla.com.uy";
    public const string CodigoEnvio1 = "ENV-2026-00201";
    public const string CodigoEnvio2 = "ENV-2026-00202";

    public static async Task SembrarAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (!await context.Operadores.AnyAsync(o => o.Id == OperadorId, cancellationToken))
        {
            await context.Operadores.AddAsync(
                new Operador(OperadorId, "TransLogística Express S.R.L.", "TransLogística Express", "Activo"), cancellationToken);
        }

        if (!await context.Comercios.AnyAsync(c => c.Id == ComercioId, cancellationToken))
        {
            await context.Comercios.AddAsync(
                new Comercio(ComercioId, "ExpressMarket Uruguay S.A.", "RUT 215550000019", "envios@expressmarket.com.uy", "+598 2900 5678"),
                cancellationToken);
        }

        if (!await context.ComerciosOperadores.IgnoreQueryFilters().AnyAsync(c => c.Id == ComercioOperadorId, cancellationToken))
        {
            await context.ComerciosOperadores.AddAsync(
                new ComercioOperador(ComercioOperadorId, ComercioId, OperadorId, ComercioOperador.CalcularHuellaClaveApi(ClaveApiDemo), "Activo"),
                cancellationToken);
        }

        await SembrarConfiguracionAsync(context, cancellationToken);
        await SembrarUsuariosAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await SembrarEnviosAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("DatabaseSeeder: segundo operador de la demo verificado (configuración propia, usuarios y envíos).");
    }

    private static async Task SembrarConfiguracionAsync(AppDbContext context, CancellationToken ct)
    {
        // Regla operativa deliberadamente distinta a la del operador por defecto (2 intentos en lugar de 3)
        if (!await context.ReglasOperativas.IgnoreQueryFilters().AnyAsync(r => r.OperadorId == OperadorId, ct))
        {
            await context.ReglasOperativas.AddAsync(new ReglaOperativa(
                ReglaOperativaId, OperadorId, version: 1, vigenteDesde: DateTime.UtcNow.AddYears(-1),
                maxIntentosEntrega: 2,
                plazoEntreIntentos: TimeSpan.FromHours(12),
                tipoPruebaEntregaExigida: "Foto del bulto entregado",
                politicaDevolucion: "Retorno a depósito al alcanzar 2 intentos fallidos",
                plazoComprometidoEstandar: TimeSpan.FromHours(72),
                plazoComprometidoUrgente: TimeSpan.FromHours(24)), ct);
        }

        if (!await context.MotivosNoEntrega.IgnoreQueryFilters().AnyAsync(m => m.OperadorId == OperadorId, ct))
        {
            await context.MotivosNoEntrega.AddRangeAsync(new[]
            {
                new MotivoNoEntrega(Guid.Parse("b9000000-0000-0000-0000-000000000001"), OperadorId, "CLI_AUSENTE", "Cliente ausente"),
                new MotivoNoEntrega(Guid.Parse("b9000000-0000-0000-0000-000000000002"), OperadorId, "DIR_INCORRECTA", "Dirección incorrecta"),
                new MotivoNoEntrega(Guid.Parse("b9000000-0000-0000-0000-000000000003"), OperadorId, "RETORNO_SIN_INTENTO", "Retorno a depósito sin intento de entrega", esInterno: true)
            }, ct);
        }

        // Tarifa distinta: el mismo envío estándar cuesta más que en el operador por defecto
        if (!await context.CuadrosTarifarios.IgnoreQueryFilters().AnyAsync(c => c.OperadorId == OperadorId, ct))
        {
            var cuadro = new CuadroTarifario(CuadroTarifarioId, OperadorId, 1, DateTime.UtcNow.AddYears(-1));
            cuadro.AgregarRegla(new ReglaTarifa(ReglaTarifaEstandarId, cuadro.Id, "Estandar", 0m, 1000m, 0m, 1000000000m, 200m));
            cuadro.AgregarRegla(new ReglaTarifa(Guid.Parse("b4000000-0002-0000-0000-000000000002"), cuadro.Id, "Urgente", 0m, 1000m, 0m, 1000000000m, 400m));
            await context.CuadrosTarifarios.AddAsync(cuadro, ct);
        }

        if (!await context.ZonasCobertura.IgnoreQueryFilters().AnyAsync(z => z.OperadorId == OperadorId, ct))
        {
            var zona = new ZonaCobertura(ZonaId, OperadorId, "Canelones Metropolitano",
                "[[-34.8400, -56.0300], [-34.8300, -55.9800], [-34.8700, -55.9600], [-34.8800, -56.0200], [-34.8400, -56.0300]]");
            zona.ActualizarDatos(zona.Nombre, "#7c3aed");
            DatabaseSeederAdministracion.AgregarFranjasLaborales(zona, 20);
            await context.ZonasCobertura.AddAsync(zona, ct);
        }

        if (!await context.IdentidadesVisuales.IgnoreQueryFilters().AnyAsync(i => i.OperadorId == OperadorId, ct))
        {
            var identidad = new IdentidadVisual(IdentidadVisualId, OperadorId, "#7C3AED", "#10B981",
                "contacto@translogistica-express.com.uy", "+598 2900 3344");
            identidad.ActualizarComunicaciones("https://translogistica-express.com.uy", "Gracias por elegirnos. Seguí tu envío en línea.");
            await context.IdentidadesVisuales.AddAsync(identidad, ct);
        }
    }

    private static async Task SembrarUsuariosAsync(AppDbContext context, CancellationToken ct)
    {
        var emailsExistentes = await context.Usuarios.Select(u => u.Email).ToListAsync(ct);
        var hash = HashContrasena.Generar(DatabaseSeederAdministracion.PasswordDemo);

        var usuarios = new[]
        {
            new Usuario(UsuarioAdministradorId, "Lucía Fernández (Express)", EmailAdministrador, hash,
                DatabaseSeederAdministracion.PerfilAdministradorId, OperadorId),
            new Usuario(UsuarioDespachadorId, "Martín Acosta (Express)", EmailDespachador, hash,
                DatabaseSeederAdministracion.PerfilDespachadorId, OperadorId)
        };

        foreach (var usuario in usuarios.Where(u => !emailsExistentes.Contains(u.Email)))
            await context.Usuarios.AddAsync(usuario, ct);
    }

    private static async Task SembrarEnviosAsync(AppDbContext context, CancellationToken ct)
    {
        if (await context.Envios.IgnoreQueryFilters().AnyAsync(e => e.OperadorId == OperadorId, ct))
            return;

        var destinatario1 = new Destinatario(Guid.Parse("ba000000-0001-0000-0000-000000000001"), "Sofía Méndez", "4.111.222-3", "sofia@correo.com", "+598 99 222 001");
        var direccion1 = new Direccion(Guid.Parse("bb000000-0001-0000-0000-000000000001"), "Av. Giannattasio", "1500", "Canelones", null, -34.8550, -56.0100, null);
        var envio1 = new Envio(Envio1Id, ComercioOperadorId, destinatario1.Id, "Estandar", CodigoEnvio1, direccion1.Id,
            direccionDestino: direccion1.ToString(), nombreDestinatario: destinatario1.Nombre, operadorId: OperadorId);
        envio1.AplicarTarifa(200m, ReglaTarifaEstandarId);
        envio1.AgregarBulto("BLT-201-A", 1.5m);

        var destinatario2 = new Destinatario(Guid.Parse("ba000000-0002-0000-0000-000000000002"), "Andrés Cabrera", "3.222.333-4", "andres@correo.com", "+598 99 222 002");
        var direccion2 = new Direccion(Guid.Parse("bb000000-0002-0000-0000-000000000002"), "Ruta Interbalnearia", "km 20", "Canelones", null, -34.8450, -55.9900, null);
        var envio2 = new Envio(Envio2Id, ComercioOperadorId, destinatario2.Id, "Estandar", CodigoEnvio2, direccion2.Id,
            direccionDestino: direccion2.ToString(), nombreDestinatario: destinatario2.Nombre, operadorId: OperadorId);
        envio2.AplicarTarifa(200m, ReglaTarifaEstandarId);
        envio2.AgregarBulto("BLT-202-A", 3.0m);

        await context.Destinatarios.AddRangeAsync(new[] { destinatario1, destinatario2 }, ct);
        await context.Direcciones.AddRangeAsync(new[] { direccion1, direccion2 }, ct);
        await context.Envios.AddRangeAsync(new[] { envio1, envio2 }, ct);
    }
}

