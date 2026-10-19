using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    // Identificadores fijos sincronizados con UltimaMilla.Mobile (DemoDataDefaults)
    public static readonly Guid DefaultOperadorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultComercioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DefaultComercioOperadorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid DefaultReglaOperativaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid DefaultVehiculoId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid DefaultRepartidorId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid DefaultUsuarioRepartidorId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public static readonly Guid DefaultHojaDeRutaId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");

    public static readonly Guid Parada1Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000001");
    public static readonly Guid Parada2Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000002");
    public static readonly Guid Parada3Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000003");

    public static readonly Guid Envio1Id = Guid.Parse("cccccccc-1111-2222-3333-000000000001");
    public static readonly Guid Envio2Id = Guid.Parse("cccccccc-1111-2222-3333-000000000002");
    public static readonly Guid Envio3Id = Guid.Parse("cccccccc-1111-2222-3333-000000000003");
    public static readonly Guid Envio4Id = Guid.Parse("cccccccc-1111-2222-3333-000000000004");
    public static readonly Guid Envio5Id = Guid.Parse("cccccccc-1111-2222-3333-000000000005");
    public static readonly Guid DefaultCuadroTarifarioId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        // Verificación de idempotencia estricta
        var rutaExiste = await context.HojasDeRuta.AnyAsync(h => h.Id == DefaultHojaDeRutaId, cancellationToken);
        if (rutaExiste)
        {
            logger.LogInformation("DatabaseSeeder: Los datos semilla ya están presentes en PostgreSQL (HojaDeRuta '{RutaId}'). Omitiendo inicialización.", DefaultHojaDeRutaId);
            await SembrarOperacionAsync(context, logger, cancellationToken);
            return;
        }

        logger.LogInformation("DatabaseSeeder: Iniciando carga de datos semilla para la demo de operación en calle (22/10)...");

        // 1. Operador Logístico Principal
        var operador = await context.Operadores.FirstOrDefaultAsync(o => o.Id == DefaultOperadorId, cancellationToken);
        if (operador is null)
        {
            operador = new Operador(
                DefaultOperadorId,
                razonSocial: "Operador Logístico Nacional S.A.",
                nombreComercial: "Última Milla Express",
                estado: "Activo"
            );
            await context.Operadores.AddAsync(operador, cancellationToken);
        }

        // 2. Comercio y Relación ComercioOperador
        var comercio = await context.Comercios.FirstOrDefaultAsync(c => c.Id == DefaultComercioId, cancellationToken);
        if (comercio is null)
        {
            comercio = new Comercio(
                DefaultComercioId,
                razonSocial: "MegaStore Uruguay S.R.L.",
                documento: "RUT 219998880011",
                email: "envios@megastore.com.uy",
                telefono: "+598 2900 1234"
            );
            await context.Comercios.AddAsync(comercio, cancellationToken);
        }

        var comercioOperador = await context.ComerciosOperadores.FirstOrDefaultAsync(co => co.Id == DefaultComercioOperadorId, cancellationToken);
        if (comercioOperador is null)
        {
            comercioOperador = new ComercioOperador(
                DefaultComercioOperadorId,
                DefaultComercioId,
                DefaultOperadorId,
                claveApi: "dev-api-key-megastore-2026",
                estado: "Activo"
            );
            await context.ComerciosOperadores.AddAsync(comercioOperador, cancellationToken);
        }

        // 3. Regla Operativa vigente para el Operador (SLA, reintentos y prueba exigida)
        var reglaExiste = await context.ReglasOperativas.AnyAsync(r => r.OperadorId == DefaultOperadorId, cancellationToken);
        if (!reglaExiste)
        {
            var regla = new ReglaOperativa(
                DefaultReglaOperativaId,
                DefaultOperadorId,
                version: 1,
                vigenteDesde: DateTime.UtcNow.AddYears(-1),
                maxIntentosEntrega: 3,
                plazoEntreIntentos: TimeSpan.FromHours(24),
                tipoPruebaEntregaExigida: "Firma digital y documento del receptor",
                politicaDevolucion: "Retorno a depósito central al alcanzar 3 intentos fallidos",
                plazoComprometidoEstandar: TimeSpan.FromHours(48),
                plazoComprometidoUrgente: TimeSpan.FromHours(24)
            );
            await context.ReglasOperativas.AddAsync(regla, cancellationToken);
        }

        // 4. Catálogo MotivoNoEntrega del Operador (códigos y GUIDs idénticos a DemoDataDefaults)
        var motivosExistentes = await context.MotivosNoEntrega
            .Where(m => m.OperadorId == DefaultOperadorId)
            .ToListAsync(cancellationToken);

        var motivosCatalogo = new List<MotivoNoEntrega>
        {
            new(Guid.Parse("dddddddd-0001-0000-0000-000000000001"), DefaultOperadorId, "CLI_AUSENTE", "Cliente ausente / No atiende llamadas"),
            new(Guid.Parse("dddddddd-0002-0000-0000-000000000002"), DefaultOperadorId, "DIR_INCORRECTA", "Dirección incorrecta o inexistente"),
            new(Guid.Parse("dddddddd-0003-0000-0000-000000000003"), DefaultOperadorId, "COMERCIO_CERRADO", "Negocio o comercio cerrado"),
            new(Guid.Parse("dddddddd-0004-0000-0000-000000000004"), DefaultOperadorId, "RECHAZADO_CLIENTE", "Rechazado expresamente por el cliente"),
            new(Guid.Parse("dddddddd-0005-0000-0000-000000000005"), DefaultOperadorId, "ZONA_INACCESIBLE", "Zona inaccesible o fuerza mayor"),
            new(Guid.Parse("dddddddd-0006-0000-0000-000000000006"), DefaultOperadorId, "BULTO_DANADO", "Bulto dañado / Avería en tránsito"),
            new(Guid.Parse("dddddddd-0007-0000-0000-000000000007"), DefaultOperadorId, "ERROR_PAGO", "Problemas con medio de pago / Cobro"),
            // Alias amigables de respaldo
            new(Guid.Parse("dddddddd-0001-0000-0000-000000000011"), DefaultOperadorId, "AUSENTE", "Cliente ausente"),
            new(Guid.Parse("dddddddd-0002-0000-0000-000000000012"), DefaultOperadorId, "DIRECCION_INCORRECTA", "Dirección incorrecta"),
            new(Guid.Parse("dddddddd-0004-0000-0000-000000000014"), DefaultOperadorId, "RECHAZADO", "Rechazado por destinatario")
        };

        foreach (var m in motivosCatalogo)
        {
            if (!motivosExistentes.Any(me => me.Id == m.Id || me.Codigo == m.Codigo))
            {
                await context.MotivosNoEntrega.AddAsync(m, cancellationToken);
            }
        }

        // 5. Flota y Repartidor
        var vehiculo = await context.Vehiculos.FirstOrDefaultAsync(v => v.Id == DefaultVehiculoId, cancellationToken);
        if (vehiculo is null)
        {
            vehiculo = new Vehiculo(
                DefaultVehiculoId,
                DefaultOperadorId,
                patente: "SBA-4321",
                tipo: "Furgón Utilitario",
                capacidadPeso: 1200m,
                capacidadVolumen: 8.5m
            );
            await context.Vehiculos.AddAsync(vehiculo, cancellationToken);
        }

        var repartidor = await context.Repartidores.FirstOrDefaultAsync(r => r.Id == DefaultRepartidorId, cancellationToken);
        if (repartidor is null)
        {
            repartidor = new Repartidor(
                DefaultRepartidorId,
                DefaultUsuarioRepartidorId,
                DefaultOperadorId,
                documento: "CI 4.321.987-6",
                telefono: "+598 99 654 321",
                vehiculoAsignadoId: DefaultVehiculoId,
                estado: "Disponible"
            );
            await context.Repartidores.AddAsync(repartidor, cancellationToken);
        }

        // 6. Hoja de Ruta en estado EnTransito
        var hojaDeRuta = new HojaDeRuta(
            DefaultHojaDeRutaId,
            DefaultOperadorId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            estado: "Despachada"
        );
        hojaDeRuta.AsignarRecursos(DefaultRepartidorId, DefaultVehiculoId);

        // 7. Envíos y Bultos sincronizados con la app móvil
        // Envío 1: Centro, Montevideo
        var envio1 = new Envio(
            Envio1Id,
            DefaultComercioOperadorId,
            direccionDestino: "Av. 18 de Julio 1450, Apto 402, Centro, Montevideo",
            nombreDestinatario: "Martín Rodríguez",
            codigoSeguimientoPublico: "ENV-2026-00108",
            operadorId: DefaultOperadorId
        );
        envio1.AgregarBulto("BLT-001-A", 1.2m);
        envio1.AgregarBulto("BLT-001-B", 1.5m);
        PrepararEnDepositoClasificado(envio1, new[] { "BLT-001-A", "BLT-001-B" });
        envio1.AsignarRuta(DefaultHojaDeRutaId);
        envio1.DespacharEnRuta(DefaultHojaDeRutaId, DefaultUsuarioRepartidorId, "Carga confirmada y despachada para entrega matutina");

        // Envío 2: Pocitos, Montevideo
        var envio2 = new Envio(
            Envio2Id,
            DefaultComercioOperadorId,
            direccionDestino: "Bvar. España 2678, Apto 501, Pocitos, Montevideo",
            nombreDestinatario: "Lucía Fernández",
            codigoSeguimientoPublico: "ENV-2026-00109",
            operadorId: DefaultOperadorId
        );
        envio2.AgregarBulto("BLT-002-A", 1.8m);
        PrepararEnDepositoClasificado(envio2, new[] { "BLT-002-A" });
        envio2.AsignarRuta(DefaultHojaDeRutaId);
        envio2.DespacharEnRuta(DefaultHojaDeRutaId, DefaultUsuarioRepartidorId, "Carga confirmada y despachada para entrega matutina");

        // Envío 3: Punta Carretas, Montevideo
        var envio3 = new Envio(
            Envio3Id,
            DefaultComercioOperadorId,
            direccionDestino: "Rambla Gandhi 456, Piso 8, Punta Carretas, Montevideo",
            nombreDestinatario: "Esteban Morales",
            codigoSeguimientoPublico: "ENV-2026-00110",
            operadorId: DefaultOperadorId
        );
        envio3.AgregarBulto("BLT-003-A", 2.1m);
        envio3.AgregarBulto("BLT-003-B", 2.5m);
        envio3.AgregarBulto("BLT-003-C", 2.7m);
        PrepararEnDepositoClasificado(envio3, new[] { "BLT-003-A", "BLT-003-B", "BLT-003-C" });
        envio3.AsignarRuta(DefaultHojaDeRutaId);
        envio3.DespacharEnRuta(DefaultHojaDeRutaId, DefaultUsuarioRepartidorId, "Carga confirmada y despachada para entrega matutina");

        await context.Envios.AddRangeAsync(new[] { envio1, envio2, envio3 }, cancellationToken);

        // 8. Paradas de la Hoja de Ruta vinculadas a los envíos
        var direccionId1 = Guid.Parse("eeeeeeee-0001-0000-0000-000000000001");
        var direccionId2 = Guid.Parse("eeeeeeee-0002-0000-0000-000000000002");
        var direccionId3 = Guid.Parse("eeeeeeee-0003-0000-0000-000000000003");

        var franja1 = Guid.Parse("ffffffff-0001-0000-0000-000000000001");
        var franja2 = Guid.Parse("ffffffff-0002-0000-0000-000000000002");
        var franja3 = Guid.Parse("ffffffff-0003-0000-0000-000000000003");

        hojaDeRuta.AgregarParada(Parada1Id, Envio1Id, orden: 1, direccionId1, franja1, estado: "Pendiente");
        hojaDeRuta.RegistrarAsignacion(Envio1Id, DefaultUsuarioRepartidorId);

        hojaDeRuta.AgregarParada(Parada2Id, Envio2Id, orden: 2, direccionId2, franja2, estado: "Pendiente");
        hojaDeRuta.RegistrarAsignacion(Envio2Id, DefaultUsuarioRepartidorId);

        hojaDeRuta.AgregarParada(Parada3Id, Envio3Id, orden: 3, direccionId3, franja3, estado: "Pendiente");
        hojaDeRuta.RegistrarAsignacion(Envio3Id, DefaultUsuarioRepartidorId);

        await context.HojasDeRuta.AddAsync(hojaDeRuta, cancellationToken);

        // Persistencia atómica de todos los datos semilla
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DatabaseSeeder: Datos semilla para la demo insertados con éxito en PostgreSQL (HojaDeRutaId: {RutaId}).", DefaultHojaDeRutaId);

        await SembrarOperacionAsync(context, logger, cancellationToken);
    }

    /// <summary>Recepciona y clasifica los bultos del envío, como ocurriría en depósito antes de armar una hoja de ruta.</summary>
    private static void PrepararEnDepositoClasificado(Envio envio, IReadOnlyCollection<string> codigosBultos)
    {
        envio.RecepcionarEnDeposito(codigosBultos, null, null, DefaultUsuarioRepartidorId);
        envio.ClasificarBultos(
            codigosBultos.Select(c => new ClasificacionBulto(c, Domain.Enums.PrioridadBulto.Normal)).ToList(),
            DefaultUsuarioRepartidorId);
    }

    /// <summary>
    /// Datos para demostrar el ciclo previo a la calle: cuadro tarifario (alta), un envío admitido (recepción)
    /// y un envío clasificado en depósito (armado de hoja de ruta). Es idempotente e independiente de la ruta demo.
    /// </summary>
    private static async Task SembrarOperacionAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken)
    {
        if (await context.CuadrosTarifarios.AnyAsync(c => c.OperadorId == DefaultOperadorId, cancellationToken))
            return;

        var reglaEstandarId = Guid.Parse("99999999-0001-0000-0000-000000000001");
        var cuadro = new CuadroTarifario(DefaultCuadroTarifarioId, DefaultOperadorId, 1, DateTime.UtcNow.AddYears(-1));
        cuadro.AgregarRegla(new ReglaTarifa(reglaEstandarId, cuadro.Id, "Estandar", 0m, 1000m, 0m, 1000000000m, 150m));
        cuadro.AgregarRegla(new ReglaTarifa(Guid.Parse("99999999-0002-0000-0000-000000000002"), cuadro.Id, "Urgente", 0m, 1000m, 0m, 1000000000m, 300m));
        await context.CuadrosTarifarios.AddAsync(cuadro, cancellationToken);

        var destinatario4 = new Destinatario(Guid.Parse("88888888-0004-0000-0000-000000000004"), "Carolina Silva", "3.456.789-0", "carolina@correo.com", "+598 99 111 444");
        var direccion4 = new Direccion(Guid.Parse("eeeeeeee-0004-0000-0000-000000000004"), "Av. Brasil", "2890", "Montevideo", null, -34.9112, -56.1560, "Casa con reja verde");
        var envio4 = new Envio(Envio4Id, DefaultComercioOperadorId, destinatario4.Id, "Estandar", "ENV-2026-00111", direccion4.Id,
            direccionDestino: direccion4.ToString(), nombreDestinatario: destinatario4.Nombre, operadorId: DefaultOperadorId);
        envio4.AplicarTarifa(150m, reglaEstandarId);
        envio4.AgregarBulto("BLT-004-A", 2.0m);
        PrepararEnDepositoClasificado(envio4, new[] { "BLT-004-A" });

        var destinatario5 = new Destinatario(Guid.Parse("88888888-0005-0000-0000-000000000005"), "Diego Pereira", "2.345.678-9", "diego@correo.com", "+598 99 111 555");
        var direccion5 = new Direccion(Guid.Parse("eeeeeeee-0005-0000-0000-000000000005"), "Bulevar Artigas", "1100", "Montevideo", null, -34.8990, -56.1650, null);
        var envio5 = new Envio(Envio5Id, DefaultComercioOperadorId, destinatario5.Id, "Estandar", "ENV-2026-00112", direccion5.Id,
            direccionDestino: direccion5.ToString(), nombreDestinatario: destinatario5.Nombre, operadorId: DefaultOperadorId);
        envio5.AplicarTarifa(150m, reglaEstandarId);
        envio5.AgregarBulto("BLT-005-A", 1.0m);

        await context.Destinatarios.AddRangeAsync(new[] { destinatario4, destinatario5 }, cancellationToken);
        await context.Direcciones.AddRangeAsync(new[] { direccion4, direccion5 }, cancellationToken);
        await context.Envios.AddRangeAsync(new[] { envio4, envio5 }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DatabaseSeeder: cuadro tarifario y envíos de depósito sembrados (ENV-2026-00111 clasificado en depósito, ENV-2026-00112 admitido).");
    }
}
