using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Entities.Retiros;

/// <summary>
/// Máquina de estados declarativa del retiro en domicilio. Convive con la del envío pero es independiente de ella:
/// las transiciones válidas están en una tabla verificable y el estado del retiro no puede modificarse fuera de la entidad.
/// </summary>
public static class MaquinaEstadosRetiro
{
    private static readonly IReadOnlyDictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>> Transiciones =
        new Dictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>>
        {
            [EstadoRetiro.Solicitado] = new HashSet<EstadoRetiro>
            {
                EstadoRetiro.AsignadoARuta,
                EstadoRetiro.Cancelado
            },
            [EstadoRetiro.AsignadoARuta] = new HashSet<EstadoRetiro>
            {
                EstadoRetiro.Retirado,
                EstadoRetiro.NoRetirado,
                // El despachador lo saca de la ruta antes de la salida: vuelve a estar pendiente de asignación
                EstadoRetiro.Solicitado,
                EstadoRetiro.Cancelado
            },
            [EstadoRetiro.NoRetirado] = new HashSet<EstadoRetiro>
            {
                // Nueva visita
                EstadoRetiro.Solicitado,
                EstadoRetiro.Cancelado
            },
            [EstadoRetiro.Retirado] = new HashSet<EstadoRetiro>
            {
                EstadoRetiro.EnDeposito
            },
            [EstadoRetiro.EnDeposito] = new HashSet<EstadoRetiro>
            {
                EstadoRetiro.EntregadoAlComercio
            },
            [EstadoRetiro.EntregadoAlComercio] = new HashSet<EstadoRetiro>(),
            [EstadoRetiro.Cancelado] = new HashSet<EstadoRetiro>()
        };

    /// <summary>
    /// Transiciones adicionales que solo se admiten al resolver un conflicto de sincronización: el repartidor trabajó sin conexión
    /// y, mientras tanto, el servidor sacó el retiro de la ruta o lo canceló. Prevalece lo realizado físicamente en el domicilio, con su evidencia,
    /// y el conflicto queda asentado. Es una política explícita y verificable, no una excepción dispersa en el código.
    /// </summary>
    private static readonly IReadOnlyDictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>> TransicionesPorConflicto =
        new Dictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>>
        {
            [EstadoRetiro.Solicitado] = new HashSet<EstadoRetiro> { EstadoRetiro.Retirado, EstadoRetiro.NoRetirado },
            [EstadoRetiro.Cancelado] = new HashSet<EstadoRetiro> { EstadoRetiro.Retirado, EstadoRetiro.NoRetirado }
        };

    public static IReadOnlyDictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>> TablaTransicionesPorConflicto => TransicionesPorConflicto;

    public static bool EsTransicionValidaPorConflicto(EstadoRetiro origen, EstadoRetiro destino) =>
        TransicionesPorConflicto.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

    /// <summary>Expone el grafo completo de transiciones para inspección y pruebas automáticas.</summary>
    public static IReadOnlyDictionary<EstadoRetiro, IReadOnlySet<EstadoRetiro>> TablaTransiciones => Transiciones;

    public static bool EsTransicionValida(EstadoRetiro origen, EstadoRetiro destino) =>
        Transiciones.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

    public static IReadOnlySet<EstadoRetiro> ObtenerDestinosPermitidos(EstadoRetiro origen) =>
        Transiciones.TryGetValue(origen, out var destinos) ? destinos : new HashSet<EstadoRetiro>();

    public static bool EsEstadoTerminal(EstadoRetiro estado) =>
        Transiciones.TryGetValue(estado, out var destinos) && destinos.Count == 0;
}
