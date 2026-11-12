using UltimaMilla.Application.DTOs.Operador;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Application.Services;

/// <summary>
/// Servicio de validación de consistencia y superposiciones de franjas horarias.
/// </summary>
public static class ValidadorFranjasHorarias
{
    public static void Validar(IEnumerable<CrearFranjaHorariaDto>? franjas)
    {
        if (franjas is null)
            return;

        var listaFranjas = franjas.ToList();

        foreach (var franja in listaFranjas)
        {
            if (franja.HoraInicio >= franja.HoraFin)
            {
                throw new DomainValidationException(
                    $"La franja del {ObtenerNombreDia(franja.DiaSemana)} es inválida: la hora de inicio ({franja.HoraInicio:HH\\:mm}) debe ser anterior a la de fin ({franja.HoraFin:HH\\:mm}).");
            }

            if (franja.CapacidadMaximaEnvios <= 0)
            {
                throw new DomainValidationException(
                    $"La capacidad máxima de la franja del {ObtenerNombreDia(franja.DiaSemana)} debe ser mayor a cero.");
            }
        }

        // Validar que no existan superposiciones dentro del mismo día de la semana
        foreach (var grupoDia in listaFranjas.GroupBy(f => f.DiaSemana))
        {
            var franjasOrdenadas = grupoDia.OrderBy(f => f.HoraInicio).ToList();
            for (var i = 1; i < franjasOrdenadas.Count; i++)
            {
                var anterior = franjasOrdenadas[i - 1];
                var actual = franjasOrdenadas[i];

                if (actual.HoraInicio < anterior.HoraFin)
                {
                    throw new DomainValidationException(
                        $"Las franjas del {ObtenerNombreDia(grupoDia.Key)} se superponen: {anterior.HoraInicio:HH\\:mm}-{anterior.HoraFin:HH\\:mm} y {actual.HoraInicio:HH\\:mm}-{actual.HoraFin:HH\\:mm}.");
                }
            }
        }
    }

    public static string ObtenerNombreDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        _ => "domingo"
    };
}

