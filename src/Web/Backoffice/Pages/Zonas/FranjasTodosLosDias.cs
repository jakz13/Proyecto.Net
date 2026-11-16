using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Backoffice.Pages.Zonas;

/// <summary>
/// Opción "Todos los días" del selector de día de una franja horaria: el formulario la envía con el valor -1 y,
/// antes de validar y guardar, se reemplaza por una franja idéntica para cada día de la semana.
/// </summary>
public static class FranjasTodosLosDias
{
    public const DayOfWeek Valor = (DayOfWeek)(-1);

    public static List<CrearFranjaHorariaDto> Expandir(IEnumerable<CrearFranjaHorariaDto> franjas)
    {
        var resultado = new List<CrearFranjaHorariaDto>();
        foreach (var franja in franjas)
        {
            if (franja.DiaSemana != Valor)
            {
                resultado.Add(franja);
                continue;
            }

            for (var dia = 0; dia < 7; dia++)
                resultado.Add(franja with { DiaSemana = (DayOfWeek)dia });
        }
        return resultado;
    }
}
