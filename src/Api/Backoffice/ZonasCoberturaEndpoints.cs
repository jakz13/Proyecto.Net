using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos.Zonas;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Zonas de cobertura y franjas horarias de entrega (UC-02, RF-02).</summary>
public static class ZonasCoberturaEndpoints
{
    public static IEndpointRouteBuilder MapZonasCoberturaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/zonas-cobertura")
            .WithTags("Backoffice · Zonas y franjas (UC-02)")
            .RequireAuthorization();

        grupo.MapGet("/", (
            [FromQuery] string? q,
            [FromQuery] bool? soloActivas,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = db.ZonasCobertura.AsNoTracking().Where(z => z.OperadorId == tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = RespuestasBackoffice.PatronContiene(q);
                consulta = consulta.Where(z => EF.Functions.ILike(z.Nombre, patron, "\\"));
            }

            if (soloActivas == true)
                consulta = consulta.Where(z => z.Activa);

            var zonas = await consulta.OrderBy(z => z.Nombre).ToListAsync(ct);
            return Results.Ok(zonas.Select(Mapear).ToList());
        }))
        .WithName("ListarZonasCobertura")
        .WithSummary("Listar las zonas de cobertura con sus franjas");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var zona = await db.ZonasCobertura.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id && z.OperadorId == tenant.OperadorId, ct);
                return zona is null
                    ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la zona de cobertura solicitada.")
                    : Results.Ok(Mapear(zona));
            }))
        .WithName("ObtenerZonaCobertura")
        .WithSummary("Detalle de una zona con sus franjas horarias");

        grupo.MapPost("/", (
            [FromBody] CrearZonaCoberturaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var franjas = dto.FranjasHorarias ?? new List<CrearFranjaHorariaDto>();
            var error = ValidarFranjas(franjas);
            if (error is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, error);

            if (await ExisteNombreAsync(db, tenant.OperadorId, dto.Nombre, null, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe una zona de cobertura con ese nombre.");

            var zona = new ZonaCobertura(Guid.NewGuid(), tenant.OperadorId, dto.Nombre, dto.PoligonoGeografico);
            zona.ActualizarDatos(dto.Nombre, dto.ColorHex);
            AgregarFranjas(db, zona, franjas);

            await db.ZonasCobertura.AddAsync(zona, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/zonas-cobertura/{zona.Id}", Mapear(zona));
        }))
        .WithName("CrearZonaCobertura")
        .WithSummary("Definir una zona de cobertura y sus franjas horarias");

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarZonaCoberturaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var zona = await db.ZonasCobertura.FirstOrDefaultAsync(z => z.Id == id && z.OperadorId == tenant.OperadorId, ct);
            if (zona is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la zona de cobertura solicitada.");

            var franjas = dto.FranjasHorarias ?? new List<CrearFranjaHorariaDto>();
            var error = ValidarFranjas(franjas);
            if (error is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, error);

            if (await ExisteNombreAsync(db, tenant.OperadorId, dto.Nombre, id, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe otra zona de cobertura con ese nombre.");

            zona.ActualizarDatos(dto.Nombre, dto.ColorHex);
            zona.ActualizarPoligono(dto.PoligonoGeografico);
            if (dto.Activa) zona.Activar(); else zona.Desactivar();

            // Las franjas se reemplazan por completo con las enviadas: las anteriores se eliminan por ser huérfanas
            db.FranjasHorarias.RemoveRange(zona.FranjasHorarias.ToList());
            zona.QuitarFranjasHorarias();
            AgregarFranjas(db, zona, franjas);

            await db.SaveChangesAsync(ct);
            return Results.Ok(Mapear(zona));
        }))
        .WithName("ActualizarZonaCobertura")
        .WithSummary("Modificar el polígono y las franjas de una zona");

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivaRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var zona = await db.ZonasCobertura.FirstOrDefaultAsync(z => z.Id == id && z.OperadorId == tenant.OperadorId, ct);
            if (zona is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la zona de cobertura solicitada.");

            if (cuerpo.Activa) zona.Activar(); else zona.Desactivar();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoZonaCobertura")
        .WithSummary("Dar de alta o baja lógica a una zona");

        grupo.MapPost("/validar-franjas", ([FromBody] List<CrearFranjaHorariaDto> franjas) =>
        {
            var error = ValidarFranjas(franjas ?? new List<CrearFranjaHorariaDto>());
            return error is null ? Results.Ok() : RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, error);
        })
        .WithName("ValidarFranjasHorarias")
        .WithSummary("Detectar franjas horarias que se superponen en un mismo día");

        return app;
    }

    private static Task<bool> ExisteNombreAsync(AppDbContext db, Guid operadorId, string nombre, Guid? excluirId, CancellationToken ct)
    {
        var normalizado = (nombre ?? string.Empty).Trim().ToLower();
        return db.ZonasCobertura.AnyAsync(z => z.OperadorId == operadorId && z.Nombre.ToLower() == normalizado && z.Id != excluirId, ct);
    }

    private static void AgregarFranjas(AppDbContext db, ZonaCobertura zona, IEnumerable<CrearFranjaHorariaDto> franjas)
    {
        foreach (var f in franjas)
        {
            var franja = new FranjaHoraria(Guid.NewGuid(), zona.Id, f.DiaSemana, f.HoraInicio, f.HoraFin, f.CapacidadMaximaEnvios);
            zona.AgregarFranjaHoraria(franja);
            db.FranjasHorarias.Add(franja);
        }
    }

    /// <summary>Verifica rangos válidos y que no haya dos franjas superpuestas el mismo día; devuelve el mensaje de error o null.</summary>
    internal static string? ValidarFranjas(IEnumerable<CrearFranjaHorariaDto> franjas)
    {
        foreach (var f in franjas)
        {
            if (f.HoraInicio >= f.HoraFin)
                return $"La franja del {NombreDia(f.DiaSemana)} es inválida: la hora de inicio ({f.HoraInicio:HH\\:mm}) debe ser anterior a la de fin ({f.HoraFin:HH\\:mm}).";
            if (f.CapacidadMaximaEnvios <= 0)
                return $"La capacidad máxima de la franja del {NombreDia(f.DiaSemana)} debe ser mayor a cero.";
        }

        foreach (var dia in franjas.GroupBy(f => f.DiaSemana))
        {
            var ordenadas = dia.OrderBy(f => f.HoraInicio).ToList();
            for (var i = 1; i < ordenadas.Count; i++)
            {
                var anterior = ordenadas[i - 1];
                var actual = ordenadas[i];
                if (actual.HoraInicio < anterior.HoraFin)
                    return $"Las franjas del {NombreDia(dia.Key)} se superponen: {anterior.HoraInicio:HH\\:mm}-{anterior.HoraFin:HH\\:mm} y {actual.HoraInicio:HH\\:mm}-{actual.HoraFin:HH\\:mm}.";
            }
        }

        return null;
    }

    private static string NombreDia(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        _ => "domingo"
    };

    private static ZonaCoberturaDto Mapear(ZonaCobertura zona) => new()
    {
        Id = zona.Id,
        OperadorId = zona.OperadorId,
        Nombre = zona.Nombre,
        PoligonoGeografico = zona.PoligonoGeografico,
        ColorHex = zona.ColorHex,
        Activa = zona.Activa,
        FechaCreacion = zona.FechaCreacion,
        FranjasHorarias = zona.FranjasHorarias
            .OrderBy(f => f.DiaSemana == DayOfWeek.Sunday ? 7 : (int)f.DiaSemana)
            .ThenBy(f => f.HoraInicio)
            .Select(f => new FranjaHorariaDto
            {
                Id = f.Id,
                ZonaCoberturaId = f.ZonaCoberturaId,
                DiaSemana = f.DiaSemana,
                HoraInicio = f.HoraInicio,
                HoraFin = f.HoraFin,
                CapacidadMaximaEnvios = f.CapacidadMaximaEnvios,
                Activa = f.Activa
            }).ToList()
    };
}
