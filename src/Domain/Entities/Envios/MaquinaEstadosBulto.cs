using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

/// <summary>
/// Máquina de estados declarativa y verificable a nivel de bulto (RF-31).
/// Define los estados válidos de cada bulto, sus transiciones permitidas y la regla declarativa
/// con la que el estado del envío se deriva a partir de los estados individuales de sus bultos.
/// </summary>
public static class MaquinaEstadosBulto
{
    public const string Ingresado = "Ingresado";
    public const string Recibido = "Recibido";
    public const string Danado = "Danado";
    public const string FaltanteEnRecepcion = "FaltanteEnRecepcion";
    public const string CargadoEnVehiculo = "CargadoEnVehiculo";
    public const string FaltanteEnCarga = "FaltanteEnCarga";
    public const string Entregado = "Entregado";
    public const string NoEntregado = "NoEntregado";
    public const string PendienteReenvio = "PendienteReenvio";
    public const string EnRetornoADeposito = "EnRetornoADeposito";
    public const string Devuelto = "Devuelto";

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Transiciones =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Ingresado] = new HashSet<string>(StringComparer.Ordinal)
            {
                Recibido,
                Danado,
                FaltanteEnRecepcion,
                CargadoEnVehiculo
            },
            [Recibido] = new HashSet<string>(StringComparer.Ordinal)
            {
                Recibido,
                Danado,
                CargadoEnVehiculo,
                FaltanteEnCarga,
                EnRetornoADeposito,
                Devuelto
            },
            [Danado] = new HashSet<string>(StringComparer.Ordinal)
            {
                Recibido,
                Danado,
                CargadoEnVehiculo,
                FaltanteEnCarga,
                EnRetornoADeposito,
                Devuelto
            },
            [FaltanteEnRecepcion] = new HashSet<string>(StringComparer.Ordinal)
            {
                Recibido,
                Danado
            },
            [CargadoEnVehiculo] = new HashSet<string>(StringComparer.Ordinal)
            {
                CargadoEnVehiculo,
                Entregado,
                NoEntregado,
                PendienteReenvio,
                EnRetornoADeposito,
                Recibido,
                FaltanteEnCarga
            },
            [FaltanteEnCarga] = new HashSet<string>(StringComparer.Ordinal)
            {
                Recibido,
                CargadoEnVehiculo,
                EnRetornoADeposito
            },
            [NoEntregado] = new HashSet<string>(StringComparer.Ordinal)
            {
                NoEntregado,
                PendienteReenvio,
                Recibido,
                CargadoEnVehiculo,
                EnRetornoADeposito,
                Devuelto,
                Entregado
            },
            [PendienteReenvio] = new HashSet<string>(StringComparer.Ordinal)
            {
                PendienteReenvio,
                Recibido,
                CargadoEnVehiculo,
                EnRetornoADeposito,
                Devuelto,
                Entregado
            },
            [EnRetornoADeposito] = new HashSet<string>(StringComparer.Ordinal)
            {
                EnRetornoADeposito,
                Recibido,
                PendienteReenvio,
                CargadoEnVehiculo,
                Devuelto
            },
            [Entregado] = new HashSet<string>(StringComparer.Ordinal),
            [Devuelto] = new HashSet<string>(StringComparer.Ordinal)
        };

    /// <summary>Expone la tabla declarativa de transiciones de bulto para inspección y pruebas automáticas.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> TablaTransiciones => Transiciones;

    /// <summary>Comprueba si la transición de <paramref name="estadoActual"/> a <paramref name="estadoNuevo"/> está permitida.</summary>
    public static bool EsTransicionValida(string? estadoActual, string? estadoNuevo)
    {
        if (string.IsNullOrWhiteSpace(estadoActual) || string.IsNullOrWhiteSpace(estadoNuevo))
            return false;

        return Transiciones.TryGetValue(estadoActual.Trim(), out var destinos)
            && destinos.Contains(estadoNuevo.Trim());
    }

    /// <summary>Valida la transición declarativa del bulto o lanza una excepción de validación de dominio.</summary>
    public static void ValidarTransicion(string identificacionBulto, string estadoActual, string estadoNuevo)
    {
        if (!EsTransicionValida(estadoActual, estadoNuevo))
        {
            throw new DomainValidationException(
                $"Transición de estado inválida para el bulto '{identificacionBulto}': '{estadoActual}' -> '{estadoNuevo}'.");
        }
    }

    /// <summary>Indica si el estado del bulto es terminal dentro de su ciclo de vida.</summary>
    public static bool EsEstadoTerminal(string estado) =>
        !string.IsNullOrWhiteSpace(estado)
        && Transiciones.TryGetValue(estado.Trim(), out var destinos)
        && destinos.Count == 0;

    /// <summary>
    /// Deriva declarativamente el estado que le corresponde al envío a partir de los estados individuales de sus bultos (RF-31).
    /// Los bultos faltantes en recepción no forman parte de los bultos operativos que viajan hacia el destinatario.
    /// </summary>
    public static EstadoEnvio DerivarEstadoEnvio(IReadOnlyCollection<Bulto> bultos, EstadoEnvio estadoActualEnvio)
    {
        if (bultos is null || bultos.Count == 0)
            return estadoActualEnvio;

        var operativos = bultos
            .Where(b => !string.Equals(b.EstadoBulto, FaltanteEnRecepcion, StringComparison.Ordinal))
            .ToList();

        if (operativos.Count == 0)
            return estadoActualEnvio;

        int entregados = operativos.Count(b => string.Equals(b.EstadoBulto, Entregado, StringComparison.Ordinal));
        int devueltos = operativos.Count(b => string.Equals(b.EstadoBulto, Devuelto, StringComparison.Ordinal));
        int enRetorno = operativos.Count(b => string.Equals(b.EstadoBulto, EnRetornoADeposito, StringComparison.Ordinal));
        int noEntregadosOPendientes = operativos.Count(b =>
            b.EstadoBulto is NoEntregado or PendienteReenvio);

        // Si todos los bultos operativos fueron entregados, el envío queda Entregado
        if (entregados == operativos.Count)
            return EstadoEnvio.Entregado;

        // Si al menos un bulto fue entregado y quedan otros sin entregar, el envío está EntregadoParcialmente
        if (entregados > 0 && entregados < operativos.Count)
            return EstadoEnvio.EntregadoParcialmente;

        // Si ningún bulto fue entregado y todos fueron devueltos al comercio, queda Devuelto
        if (devueltos == operativos.Count)
            return EstadoEnvio.Devuelto;

        // Si ningún bulto fue entregado y todos están en retorno al comercio, queda EnDevolucion
        if (enRetorno == operativos.Count)
            return EstadoEnvio.EnDevolucion;

        // Si ningún bulto fue entregado y se registraron bultos no entregados, queda NoEntregado
        if (noEntregadosOPendientes > 0 || enRetorno > 0)
            return EstadoEnvio.NoEntregado;

        if (operativos.Any(b => string.Equals(b.EstadoBulto, CargadoEnVehiculo, StringComparison.Ordinal)))
            return EstadoEnvio.EnTransito;

        if (operativos.All(b => b.EstadoBulto is Recibido or Danado))
            return EstadoEnvio.EnDeposito;

        return estadoActualEnvio;
    }
}

