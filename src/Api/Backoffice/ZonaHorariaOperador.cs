namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Las franjas horarias, las fechas de las rutas y los períodos de los reportes se expresan en hora local del operador
/// (Uruguay); los instantes se guardan en UTC. Aquí se convierte entre ambos.
/// </summary>
internal static class ZonaHorariaOperador
{
    private static readonly TimeZoneInfo Zona = Obtener();

    public static DateOnly FechaLocal(DateTime utc) => DateOnly.FromDateTime(HoraLocal(utc));

    public static DateTime HoraLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zona);

    public static DateTime ALaHoraUtc(DateOnly fecha, TimeOnly hora) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fecha.ToDateTime(hora), DateTimeKind.Unspecified), Zona);

    /// <summary>Instante UTC en que empieza el día local indicado.</summary>
    public static DateTime InicioDelDiaUtc(DateOnly fecha) => ALaHoraUtc(fecha, TimeOnly.MinValue);

    private static TimeZoneInfo Obtener()
    {
        foreach (var id in new[] { "America/Montevideo", "Montevideo Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("UTC-03", TimeSpan.FromHours(-3), "UTC-03", "UTC-03");
    }
}
