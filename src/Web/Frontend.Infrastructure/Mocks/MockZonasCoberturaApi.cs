using System.Net;
using UltimaMilla.Frontend.Contracts.Interfaces;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Frontend.Infrastructure.Excepciones;

namespace UltimaMilla.Frontend.Infrastructure.Mocks;

/// <summary>
/// Implementación simulada en memoria del servicio de zonas de cobertura y franjas horarias (UC-02, RF-02).
/// Incluye validación de superposiciones horarias inconsistentes para el operador logístico.
/// </summary>
public class MockZonasCoberturaApi : IZonasCoberturaApi
{
    private static readonly List<ZonaCoberturaDto> ZonasEnMemoria = new()
    {
        new ZonaCoberturaDto
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777771"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Nombre = "Montevideo Centro & Sur",
            PoligonoGeografico = "[[-34.9080, -56.2120], [-34.9030, -56.1780], [-34.9180, -56.1480], [-34.9310, -56.1600], [-34.9190, -56.2080], [-34.9080, -56.2120]]",
            ColorHex = "#2563eb",
            Activa = true,
            FechaCreacion = DateTime.UtcNow.AddMonths(-4),
            FranjasHorarias = new List<FranjaHorariaDto>
            {
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    DiaSemana = DayOfWeek.Monday,
                    HoraInicio = new TimeOnly(9, 0),
                    HoraFin = new TimeOnly(13, 0),
                    CapacidadMaximaEnvios = 40,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    DiaSemana = DayOfWeek.Monday,
                    HoraInicio = new TimeOnly(14, 0),
                    HoraFin = new TimeOnly(18, 0),
                    CapacidadMaximaEnvios = 50,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    DiaSemana = DayOfWeek.Wednesday,
                    HoraInicio = new TimeOnly(9, 0),
                    HoraFin = new TimeOnly(13, 0),
                    CapacidadMaximaEnvios = 40,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    DiaSemana = DayOfWeek.Friday,
                    HoraInicio = new TimeOnly(14, 0),
                    HoraFin = new TimeOnly(18, 0),
                    CapacidadMaximaEnvios = 45,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777771"),
                    DiaSemana = DayOfWeek.Saturday,
                    HoraInicio = new TimeOnly(9, 0),
                    HoraFin = new TimeOnly(13, 0),
                    CapacidadMaximaEnvios = 25,
                    Activa = true
                }
            }
        },
        new ZonaCoberturaDto
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777772"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Nombre = "Montevideo Norte & Prado",
            PoligonoGeografico = "[[-34.8550, -56.2150], [-34.8450, -56.1850], [-34.8750, -56.1750], [-34.8850, -56.2050], [-34.8550, -56.2150]]",
            ColorHex = "#10b981",
            Activa = true,
            FechaCreacion = DateTime.UtcNow.AddMonths(-2),
            FranjasHorarias = new List<FranjaHorariaDto>
            {
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777772"),
                    DiaSemana = DayOfWeek.Tuesday,
                    HoraInicio = new TimeOnly(10, 0),
                    HoraFin = new TimeOnly(14, 0),
                    CapacidadMaximaEnvios = 30,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777772"),
                    DiaSemana = DayOfWeek.Thursday,
                    HoraInicio = new TimeOnly(15, 0),
                    HoraFin = new TimeOnly(19, 0),
                    CapacidadMaximaEnvios = 35,
                    Activa = true
                }
            }
        },
        new ZonaCoberturaDto
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777773"),
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Nombre = "Ciudad de la Costa & Canelones Este",
            PoligonoGeografico = "[[-34.8800, -56.0500], [-34.8500, -55.9700], [-34.8850, -55.9500], [-34.9050, -56.0350], [-34.8800, -56.0500]]",
            ColorHex = "#f59e0b",
            Activa = true,
            FechaCreacion = DateTime.UtcNow.AddMonths(-1),
            FranjasHorarias = new List<FranjaHorariaDto>
            {
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777773"),
                    DiaSemana = DayOfWeek.Monday,
                    HoraInicio = new TimeOnly(13, 0),
                    HoraFin = new TimeOnly(18, 0),
                    CapacidadMaximaEnvios = 30,
                    Activa = true
                },
                new FranjaHorariaDto
                {
                    Id = Guid.NewGuid(),
                    ZonaCoberturaId = Guid.Parse("77777777-7777-7777-7777-777777777773"),
                    DiaSemana = DayOfWeek.Friday,
                    HoraInicio = new TimeOnly(13, 0),
                    HoraFin = new TimeOnly(18, 0),
                    CapacidadMaximaEnvios = 30,
                    Activa = true
                }
            }
        }
    };

    public Task<IReadOnlyList<ZonaCoberturaDto>> ListarZonasAsync(FiltroZonasDto? filtro = null, CancellationToken cancelacion = default)
    {
        var consulta = ZonasEnMemoria.AsQueryable();

        if (filtro != null)
        {
            if (!string.IsNullOrWhiteSpace(filtro.TextoBusqueda))
            {
                var texto = filtro.TextoBusqueda.Trim().ToLowerInvariant();
                consulta = consulta.Where(z => z.Nombre.ToLowerInvariant().Contains(texto));
            }

            if (filtro.SoloActivas.HasValue)
            {
                consulta = consulta.Where(z => z.Activa == filtro.SoloActivas.Value);
            }
        }

        var lista = consulta.OrderByDescending(z => z.FechaCreacion).ToList();
        return Task.FromResult<IReadOnlyList<ZonaCoberturaDto>>(lista);
    }

    public Task<ZonaCoberturaDto?> ObtenerPorIdAsync(Guid id, CancellationToken cancelacion = default)
    {
        var zona = ZonasEnMemoria.FirstOrDefault(z => z.Id == id);
        return Task.FromResult(zona);
    }

    public async Task<ZonaCoberturaDto> CrearZonaAsync(CrearZonaCoberturaDto nuevaZona, CancellationToken cancelacion = default)
    {
        // 1. Validar nombre no repetido
        if (ZonasEnMemoria.Any(z => string.Equals(z.Nombre.Trim(), nuevaZona.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, $"Ya existe una zona de cobertura con el nombre '{nuevaZona.Nombre}'.");
        }

        // 2. Validar superposición horaria inconsistente
        var (esValido, mensajeError) = await ValidarSuperposicionFranjasAsync(nuevaZona.FranjasHorarias, cancelacion);
        if (!esValido)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, mensajeError ?? "Superposición horaria inconsistente detectada.");
        }

        var nuevoId = Guid.NewGuid();
        var franjasDto = nuevaZona.FranjasHorarias.Select(f => new FranjaHorariaDto
        {
            Id = Guid.NewGuid(),
            ZonaCoberturaId = nuevoId,
            DiaSemana = f.DiaSemana,
            HoraInicio = f.HoraInicio,
            HoraFin = f.HoraFin,
            CapacidadMaximaEnvios = f.CapacidadMaximaEnvios,
            Activa = true
        }).ToList();

        var entidad = new ZonaCoberturaDto
        {
            Id = nuevoId,
            OperadorId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Nombre = nuevaZona.Nombre.Trim(),
            PoligonoGeografico = nuevaZona.PoligonoGeografico.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(nuevaZona.ColorHex) ? "#2563eb" : nuevaZona.ColorHex.Trim(),
            Activa = true,
            FechaCreacion = DateTime.UtcNow,
            FranjasHorarias = franjasDto
        };

        ZonasEnMemoria.Insert(0, entidad);
        return entidad;
    }

    public async Task<ZonaCoberturaDto> ActualizarZonaAsync(Guid id, ActualizarZonaCoberturaDto zonaActualizada, CancellationToken cancelacion = default)
    {
        var indice = ZonasEnMemoria.FindIndex(z => z.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "La zona de cobertura especificada no fue encontrada.");
        }

        // 1. Validar nombre no repetido en otra zona
        if (ZonasEnMemoria.Any(z => z.Id != id && string.Equals(z.Nombre.Trim(), zonaActualizada.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, $"Ya existe otra zona de cobertura con el nombre '{zonaActualizada.Nombre}'.");
        }

        // 2. Validar superposición horaria inconsistente
        var (esValido, mensajeError) = await ValidarSuperposicionFranjasAsync(zonaActualizada.FranjasHorarias, cancelacion);
        if (!esValido)
        {
            throw new ApiExcepcion(HttpStatusCode.BadRequest, mensajeError ?? "Superposición horaria inconsistente detectada.");
        }

        var franjasDto = zonaActualizada.FranjasHorarias.Select(f => new FranjaHorariaDto
        {
            Id = Guid.NewGuid(),
            ZonaCoberturaId = id,
            DiaSemana = f.DiaSemana,
            HoraInicio = f.HoraInicio,
            HoraFin = f.HoraFin,
            CapacidadMaximaEnvios = f.CapacidadMaximaEnvios,
            Activa = true
        }).ToList();

        var existente = ZonasEnMemoria[indice];
        var modificada = existente with
        {
            Nombre = zonaActualizada.Nombre.Trim(),
            PoligonoGeografico = zonaActualizada.PoligonoGeografico.Trim(),
            ColorHex = zonaActualizada.ColorHex.Trim(),
            Activa = zonaActualizada.Activa,
            FranjasHorarias = franjasDto
        };

        ZonasEnMemoria[indice] = modificada;
        return modificada;
    }

    public Task CambiarEstadoZonaAsync(Guid id, bool activa, CancellationToken cancelacion = default)
    {
        var indice = ZonasEnMemoria.FindIndex(z => z.Id == id);
        if (indice < 0)
        {
            throw new ApiExcepcion(HttpStatusCode.NotFound, "La zona de cobertura especificada no fue encontrada.");
        }

        ZonasEnMemoria[indice] = ZonasEnMemoria[indice] with { Activa = activa };
        return Task.CompletedTask;
    }

    /// <summary>
    /// Valida que no haya franjas horarias con inicio posterior al fin ni solapamientos en el mismo día.
    /// Flujo principal de UC-02: "El sistema valida que no haya superposiciones inconsistentes y guarda la configuración."
    /// </summary>
    public Task<(bool EsValido, string? MensajeError)> ValidarSuperposicionFranjasAsync(
        IEnumerable<CrearFranjaHorariaDto> franjas,
        CancellationToken cancelacion = default)
    {
        var listaFranjas = franjas?.ToList() ?? new List<CrearFranjaHorariaDto>();

        // 1. Validar que la hora inicio sea menor que la hora fin
        foreach (var franja in listaFranjas)
        {
            if (franja.HoraInicio >= franja.HoraFin)
            {
                var nombreDia = ObtenerNombreDia(franja.DiaSemana);
                return Task.FromResult((false, (string?)$"La franja horaria del día {nombreDia} ({franja.HoraInicio:HH\\:mm} a {franja.HoraFin:HH\\:mm}) es inválida: la hora de inicio debe ser anterior a la hora de fin."));
            }
        }

        // 2. Agrupar por día y verificar superposiciones (solapamientos)
        var gruposPorDia = listaFranjas.GroupBy(f => f.DiaSemana);
        foreach (var grupo in gruposPorDia)
        {
            var franjasDelDia = grupo.OrderBy(f => f.HoraInicio).ToList();
            for (int i = 0; i < franjasDelDia.Count - 1; i++)
            {
                var actual = franjasDelDia[i];
                var siguiente = franjasDelDia[i + 1];

                // Si la hora de fin de la franja actual es mayor que la hora de inicio de la siguiente franja
                if (actual.HoraFin > siguiente.HoraInicio)
                {
                    var nombreDia = ObtenerNombreDia(grupo.Key);
                    return Task.FromResult((false, (string?)$"Existe una superposición inconsistente para el día {nombreDia}: la franja [{actual.HoraInicio:HH\\:mm} - {actual.HoraFin:HH\\:mm}] se superpone con la franja [{siguiente.HoraInicio:HH\\:mm} - {siguiente.HoraFin:HH\\:mm}]."));
                }
            }
        }

        return Task.FromResult((true, (string?)null));
    }

    private static string ObtenerNombreDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "Lunes",
        DayOfWeek.Tuesday => "Martes",
        DayOfWeek.Wednesday => "Miércoles",
        DayOfWeek.Thursday => "Jueves",
        DayOfWeek.Friday => "Viernes",
        DayOfWeek.Saturday => "Sábado",
        DayOfWeek.Sunday => "Domingo",
        _ => dia.ToString()
    };
}
