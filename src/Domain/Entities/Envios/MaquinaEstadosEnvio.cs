using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>
/// Máquina de estados declarativa del envío. Define todas las transiciones permitidas
/// entre estados y el carácter terminal de cada uno.
/// </summary>
public static class MaquinaEstadosEnvio
{
    private static readonly IReadOnlyDictionary<EstadoEnvio, IReadOnlySet<EstadoEnvio>> Transiciones =
        new Dictionary<EstadoEnvio, IReadOnlySet<EstadoEnvio>>
        {
            [EstadoEnvio.Admitido] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.EnDeposito,
                EstadoEnvio.Cancelado,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.EnDeposito] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.AsignadoARuta,
                EstadoEnvio.EnDevolucion,
                EstadoEnvio.Cancelado,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.AsignadoARuta] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.EnTransito,
                EstadoEnvio.EnDeposito,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.EnTransito] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.Entregado,
                EstadoEnvio.EntregadoParcialmente,
                EstadoEnvio.NoEntregado,
                EstadoEnvio.EnDeposito,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.EntregadoParcialmente] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.EnDeposito,
                EstadoEnvio.AsignadoARuta,
                EstadoEnvio.EnTransito,
                EstadoEnvio.Entregado,
                EstadoEnvio.Reprogramado,
                EstadoEnvio.EnDevolucion,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.NoEntregado] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.Reprogramado,
                EstadoEnvio.EnDevolucion,
                EstadoEnvio.EnDeposito,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.Reprogramado] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.EnDeposito,
                EstadoEnvio.AsignadoARuta,
                EstadoEnvio.EnDevolucion,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.EnDevolucion] = new HashSet<EstadoEnvio>
            {
                EstadoEnvio.Devuelto,
                EstadoEnvio.Extraviado
            },
            [EstadoEnvio.Entregado] = new HashSet<EstadoEnvio>(),
            [EstadoEnvio.Devuelto] = new HashSet<EstadoEnvio>(),
            [EstadoEnvio.Extraviado] = new HashSet<EstadoEnvio>(),
            [EstadoEnvio.Cancelado] = new HashSet<EstadoEnvio>()
        };

    /// <summary>Expone el grafo completo de transiciones para inspección y pruebas automáticas.</summary>
    public static IReadOnlyDictionary<EstadoEnvio, IReadOnlySet<EstadoEnvio>> TablaTransiciones => Transiciones;

    /// <summary>Indica si el paso de <paramref name="estadoActual"/> a <paramref name="nuevoEstado"/> está permitido.</summary>
    public static bool EsTransicionValida(EstadoEnvio estadoActual, EstadoEnvio nuevoEstado) =>
        Transiciones.TryGetValue(estadoActual, out var destinos) && destinos.Contains(nuevoEstado);

    /// <summary>Devuelve el conjunto de estados alcanzables desde <paramref name="estadoActual"/>.</summary>
    public static IReadOnlySet<EstadoEnvio> ObtenerDestinosPermitidos(EstadoEnvio estadoActual) =>
        Transiciones.TryGetValue(estadoActual, out var destinos) ? destinos : new HashSet<EstadoEnvio>();

    /// <summary>Devuelve la colección de estados alcanzables desde <paramref name="origen"/>.</summary>
    public static IReadOnlyCollection<EstadoEnvio> DestinosPermitidos(EstadoEnvio origen) =>
        Transiciones.TryGetValue(origen, out var destinos) ? destinos : Array.Empty<EstadoEnvio>();

    /// <summary>Un estado es terminal cuando no admite ninguna transición saliente en la tabla declarativa.</summary>
    public static bool EsEstadoTerminal(EstadoEnvio estado) =>
        Transiciones.TryGetValue(estado, out var destinos) && destinos.Count == 0;

    /// <summary>Recorre todas las transiciones declaradas (útil para sembrar la tabla de referencia).</summary>
    public static IEnumerable<(EstadoEnvio Origen, EstadoEnvio Destino)> TodasLasTransiciones() =>
        Transiciones.SelectMany(kv => kv.Value.Select(d => (kv.Key, d)));
}
