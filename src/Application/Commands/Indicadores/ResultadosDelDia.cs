using UltimaMilla.Application.Queries.Tablero;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Enums;

namespace UltimaMilla.Application.Commands.Indicadores;

/// <summary>Reglas compartidas por el recálculo de indicadores (UC-37) y el cierre diario (UC-38) para clasificar los resultados de un día.</summary>
public static class ResultadosDelDia
{
    /// <summary>Último resultado de entrega (entregado o no entregado) del envío dentro del intervalo, o <c>null</c> si no tuvo ninguno.</summary>
    public static EventoEnvio? UltimoResultado(Envio envio, DateTime desdeUtc, DateTime hastaUtcExclusivo)
    {
        ArgumentNullException.ThrowIfNull(envio);

        return envio.Eventos
            .Where(e => e.EstadoNuevo is EstadoEnvio.Entregado or EstadoEnvio.NoEntregado
                        && e.Timestamp >= desdeUtc && e.Timestamp < hastaUtcExclusivo)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault();
    }

    /// <summary>
    /// Un envío entregado cumple su compromiso si no tenía ventana comprometida, o si se entregó el día de la franja y antes de que cerrara.
    /// Una entrega en un día de la semana distinto al de la franja no puede verificarse contra ella y se cuenta como fuera de plazo.
    /// </summary>
    public static bool EntregadoEnPlazo(DateTime entregadoUtc, FranjaHoraria? franjaComprometida)
    {
        if (franjaComprometida is null)
            return true;

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entregadoUtc, DateTimeKind.Utc), ZonaHorariaOperacion.Zona);
        return local.DayOfWeek == franjaComprometida.DiaSemana && TimeOnly.FromDateTime(local) <= franjaComprometida.HoraFin;
    }
}
