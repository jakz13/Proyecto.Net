using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;

namespace UltimaMilla.Backoffice.Pages.Zonas;

/// <summary>
/// Entrada de formulario para una franja horaria. Admite días individuales de <see cref="DayOfWeek"/>
/// y el valor especial <c>"-1"</c> ("Todos los días") sin que la validación de enum del model binder lo rechace.
/// </summary>
public class FranjaHorariaFormulario
{
    [Required(ErrorMessage = "El día de la semana es obligatorio.")]
    public string DiaSemana { get; set; } = nameof(DayOfWeek.Monday);

    [Required(ErrorMessage = "La hora de inicio es obligatoria.")]
    public TimeOnly HoraInicio { get; set; } = new(9, 0);

    [Required(ErrorMessage = "La hora de fin es obligatoria.")]
    public TimeOnly HoraFin { get; set; } = new(13, 0);

    [Range(1, 500, ErrorMessage = "La capacidad máxima debe estar entre 1 y 500 envíos.")]
    public int CapacidadMaximaEnvios { get; set; } = 30;
}

/// <summary>
/// Opción "Todos los días" del selector de día de una franja horaria: el formulario la envía con el valor -1 y,
/// antes de validar y guardar, se reemplaza por una franja idéntica para cada día de la semana.
/// </summary>
public static class FranjasTodosLosDias
{
    public const string CodigoTodosLosDias = "-1";
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

    public static List<CrearFranjaHorariaDto>? ConvertirYExpandir(IEnumerable<FranjaHorariaFormulario> franjas, out string? error)
    {
        var baseDtos = new List<CrearFranjaHorariaDto>();
        foreach (var f in franjas)
        {
            var textoDia = (f.DiaSemana ?? string.Empty).Trim();
            DayOfWeek dia;
            if (string.Equals(textoDia, CodigoTodosLosDias, StringComparison.Ordinal))
            {
                dia = Valor;
            }
            else if (Enum.TryParse<DayOfWeek>(textoDia, ignoreCase: true, out var diaParseado) && Enum.IsDefined(diaParseado))
            {
                dia = diaParseado;
            }
            else
            {
                error = $"El día de la semana '{f.DiaSemana}' no es válido.";
                return null;
            }

            baseDtos.Add(new CrearFranjaHorariaDto
            {
                DiaSemana = dia,
                HoraInicio = f.HoraInicio,
                HoraFin = f.HoraFin,
                CapacidadMaximaEnvios = f.CapacidadMaximaEnvios
            });
        }

        error = null;
        return Expandir(baseDtos);
    }

    public static string ResumirErroresModelState(ModelStateDictionary modelState)
    {
        var mensajes = modelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();

        return mensajes.Count > 0
            ? string.Join(" ", mensajes)
            : "Verificá los datos ingresados en el formulario.";
    }
}
