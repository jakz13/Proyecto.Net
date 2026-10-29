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
