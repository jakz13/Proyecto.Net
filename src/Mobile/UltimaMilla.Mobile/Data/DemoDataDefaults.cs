using UltimaMilla.Mobile.Models.Local;

namespace UltimaMilla.Mobile.Data;

public static class DemoDataDefaults
{
    public static readonly Guid DefaultHojaDeRutaId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    public static readonly Guid DefaultRepartidorId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid DefaultUsuarioRepartidorId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    public static readonly Guid Parada1Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000001");
    public static readonly Guid Parada2Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000002");
    public static readonly Guid Parada3Id = Guid.Parse("bbbbbbbb-1111-2222-3333-000000000003");

    public static readonly Guid Envio1Id = Guid.Parse("cccccccc-1111-2222-3333-000000000001");
    public static readonly Guid Envio2Id = Guid.Parse("cccccccc-1111-2222-3333-000000000002");
    public static readonly Guid Envio3Id = Guid.Parse("cccccccc-1111-2222-3333-000000000003");

    // Coordenadas reales y consistentes de Montevideo para las 3 paradas (Sección 7.3 - Geocercas)
    // Parada 1 (ENV-2026-00108, Av. 18 de Julio 1450)
    public const double Parada1Latitud = -34.9058;
    public const double Parada1Longitud = -56.1885;

    // Parada 2 (ENV-2026-00109, Bvar. España 2678)
    public const double Parada2Latitud = -34.9145;
    public const double Parada2Longitud = -56.1552;

    // Parada 3 (ENV-2026-00110, Rambla Gandhi 456)
    public const double Parada3Latitud = -34.9238;
    public const double Parada3Longitud = -56.1598;

    // Coordenadas de simulación en vivo para Parada 1 (Av. 18 de Julio 1450)
    // ~45 metros (Dentro del radio de entrada < 100m -> dispara LLEGADA_GEOCERCA)
    public const double Parada1SimulacionEntrada45mLat = -34.905395;
    public const double Parada1SimulacionEntrada45mLon = -56.1885;

    // ~220 metros (Fuera del radio de salida > 150m -> dispara SALIDA_GEOCERCA)
    public const double Parada1SimulacionSalida220mLat = -34.903820;
    public const double Parada1SimulacionSalida220mLon = -56.1885;

    public static (double Latitud, double Longitud) ObtenerCoordenadasPorParada(string? codigoSeguimiento, int orden)
    {
        return (codigoSeguimiento?.Trim().ToUpperInvariant(), orden) switch
        {
            ("ENV-2026-00108", _) or (_, 1) => (Parada1Latitud, Parada1Longitud),
            ("ENV-2026-00109", _) or (_, 2) => (Parada2Latitud, Parada2Longitud),
            ("ENV-2026-00110", _) or (_, 3) => (Parada3Latitud, Parada3Longitud),
            _ => (Parada1Latitud - (orden * 0.005), Parada1Longitud - (orden * 0.005))
        };
    }

    public static List<MotivoNoEntregaLocal> ObtenerCatalogoMotivos() => new()
    {
        new() { Id = Guid.Parse("dddddddd-0001-0000-0000-000000000001"), Codigo = "CLI_AUSENTE", Descripcion = "Cliente ausente / No atiende llamadas", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0002-0000-0000-000000000002"), Codigo = "DIR_INCORRECTA", Descripcion = "Dirección incorrecta o inexistente", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0003-0000-0000-000000000003"), Codigo = "COMERCIO_CERRADO", Descripcion = "Negocio o comercio cerrado", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0004-0000-0000-000000000004"), Codigo = "RECHAZADO_CLIENTE", Descripcion = "Rechazado expresamente por el cliente", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0005-0000-0000-000000000005"), Codigo = "ZONA_INACCESIBLE", Descripcion = "Zona inaccesible o fuerza mayor", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0006-0000-0000-000000000006"), Codigo = "BULTO_DANADO", Descripcion = "Bulto dañado / Avería en tránsito", Activo = true },
        new() { Id = Guid.Parse("dddddddd-0007-0000-0000-000000000007"), Codigo = "ERROR_PAGO", Descripcion = "Problemas con medio de pago / Cobro", Activo = true }
    };
}
