namespace UltimaMilla.Application.Queries.Tablero;

/// <summary>
/// Zona horaria en la que se interpretan las ventanas comprometidas (las franjas se definen en hora local de la operación).
/// Uruguay no usa horario de verano, por lo que si el sistema no conoce la zona se usa el desfase fijo UTC-3.
/// </summary>
public static class ZonaHorariaOperacion
{
    public static readonly TimeZoneInfo Zona = Resolver();

    private static TimeZoneInfo Resolver()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Montevideo");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.CreateCustomTimeZone("UY-3", TimeSpan.FromHours(-3), "Uruguay", "Uruguay");
    }

    /// <summary>Intervalo UTC [desde, hasta) que cubre el día calendario local de la operación.</summary>
    public static (DateTime DesdeUtc, DateTime HastaUtcExclusivo) LimitesUtcDelDia(DateOnly fecha)
    {
        var inicioLocal = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var desdeUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Zona);
        var hastaUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal.AddDays(1), Zona);
        return (desdeUtc, hastaUtc);
    }

    public static DateOnly FechaLocalDe(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona));
}
