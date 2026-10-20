using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>
/// Máquina de estados declarativa del envío (RF-11, UC-39). Define de forma única
/// qué transiciones de estado son válidas; cualquier otra se rechaza.
/// </summary>
public static class MaquinaEstadosEnvio
{
    private static readonly IReadOnlyDictionary<EstadoEnvio, EstadoEnvio[]> Transiciones =
        new Dictionary<EstadoEnvio, EstadoEnvio[]>
        {
            // Recepción en depósito (UC-14) o extravío antes de ingresar
            [EstadoEnvio.Admitido] = [EstadoEnvio.EnDeposito, EstadoEnvio.Extraviado],

            // Asignación a hoja de ruta (UC-08) o extravío en depósito
            [EstadoEnvio.EnDeposito] = [EstadoEnvio.AsignadoARuta, EstadoEnvio.Extraviado],

            // Carga del vehículo (UC-18) o retiro de la ruta (UC-10, faltante en carga)
            [EstadoEnvio.AsignadoARuta] = [EstadoEnvio.EnTransito, EstadoEnvio.EnDeposito, EstadoEnvio.Extraviado],

            // Resultado en calle (UC-20, UC-21) o regreso al depósito sin intentar (UC-16)
            [EstadoEnvio.EnTransito] = [EstadoEnvio.Entregado, EstadoEnvio.NoEntregado, EstadoEnvio.EnDeposito, EstadoEnvio.Extraviado],

            // Regla operativa tras un intento fallido (UC-21) o regreso a depósito sin intento en la rendición (UC-16)
            [EstadoEnvio.NoEntregado] = [EstadoEnvio.Reprogramado, EstadoEnvio.EnDevolucion, EstadoEnvio.EnDeposito, EstadoEnvio.Extraviado],

            // Nuevo intento en otra ruta (UC-10, UC-34) o regreso al depósito
            [EstadoEnvio.Reprogramado] = [EstadoEnvio.AsignadoARuta, EstadoEnvio.EnDeposito, EstadoEnvio.Extraviado],

            // Devolución cerrada (UC-32) o extravío durante la devolución
            [EstadoEnvio.EnDevolucion] = [EstadoEnvio.Devuelto, EstadoEnvio.Extraviado],

            // Estados terminales
            [EstadoEnvio.Entregado] = [],
            [EstadoEnvio.Devuelto] = [],
            [EstadoEnvio.Extraviado] = []
        };

    public static bool EsTransicionValida(EstadoEnvio origen, EstadoEnvio destino) =>
        Transiciones.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

    public static bool EsEstadoTerminal(EstadoEnvio estado) =>
        Transiciones.TryGetValue(estado, out var destinos) && destinos.Length == 0;

    public static IReadOnlyCollection<EstadoEnvio> DestinosPermitidos(EstadoEnvio origen) =>
        Transiciones.TryGetValue(origen, out var destinos) ? destinos : [];

    /// <summary>Recorre todas las transiciones declaradas (útil para sembrar la tabla de referencia).</summary>
    public static IEnumerable<(EstadoEnvio Origen, EstadoEnvio Destino)> TodasLasTransiciones() =>
        Transiciones.SelectMany(kv => kv.Value.Select(d => (kv.Key, d)));
}
