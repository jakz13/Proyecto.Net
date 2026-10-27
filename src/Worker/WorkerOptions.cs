namespace UltimaMilla.Worker;

/// <summary>Parámetros operativos del worker. Todos tienen un valor por defecto razonable y pueden ajustarse por configuración.</summary>
public class WorkerOptions
{
    public const string Seccion = "Worker";

    /// <summary>Cada cuántos segundos se publican en el broker los mensajes pendientes del outbox.</summary>
    public int OutboxIntervaloSegundos { get; set; } = 3;

    public int OutboxTamanoLote { get; set; } = 50;

    /// <summary>Cada cuántos segundos se evalúan los reintentos de avisos a comercios y el cierre diario.</summary>
    public int ProcesosProgramadosIntervaloSegundos { get; set; } = 30;

    public int AvisosTamanoLote { get; set; } = 50;

    /// <summary>Mensajes que el consumidor puede tener sin confirmar a la vez.</summary>
    public ushort ConsumidorPrefetch { get; set; } = 10;

    /// <summary>Hora local de la operación a partir de la cual se recalculan los indicadores y se cierra la jornada.</summary>
    public string CierreDiarioHoraLocal { get; set; } = "23:30";
}
