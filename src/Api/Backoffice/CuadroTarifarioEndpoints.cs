using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos.Tarifas;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Cuadro tarifario versionado del operador: publicación de versiones y simulación de precios.</summary>
public static class CuadroTarifarioEndpoints
{
    public static IEndpointRouteBuilder MapCuadroTarifarioEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/cuadros-tarifarios")
            .WithTags("Backoffice · Cuadro tarifario")
            .RequireAuthorization();

        grupo.MapGet("/", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var cuadros = await db.CuadrosTarifarios.AsNoTracking()
                .Where(c => c.OperadorId == tenant.OperadorId)
                .OrderByDescending(c => c.Version)
                .ToListAsync(ct);
            var zonas = await NombresDeZonasAsync(db, tenant.OperadorId, ct);
            return Results.Ok(cuadros.Select(c => Mapear(c, zonas)).ToList());
        }))
        .WithName("ListarCuadrosTarifarios")
        .WithSummary("Historial de versiones del cuadro tarifario");

        grupo.MapGet("/vigente", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vigente = await ObtenerVigenteAsync(db, tenant.OperadorId, ct);
            if (vigente is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No hay un cuadro tarifario vigente.");

            return Results.Ok(Mapear(vigente, await NombresDeZonasAsync(db, tenant.OperadorId, ct)));
        }))
        .WithName("ObtenerCuadroTarifarioVigente")
        .WithSummary("Versión vigente del cuadro tarifario");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var cuadro = await db.CuadrosTarifarios.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.OperadorId == tenant.OperadorId, ct);
            if (cuadro is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la versión del cuadro tarifario.");

            return Results.Ok(Mapear(cuadro, await NombresDeZonasAsync(db, tenant.OperadorId, ct)));
        }))
        .WithName("ObtenerCuadroTarifario")
        .WithSummary("Detalle de una versión del cuadro tarifario");

        grupo.MapPost("/", (
            [FromBody] CrearVersionCuadroTarifarioDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var reglas = dto.ReglasTarifa ?? new List<CrearReglaTarifaDto>();
            if (reglas.Count == 0)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "La nueva versión debe incluir al menos una regla de tarifa.");

            var zonas = await NombresDeZonasAsync(db, tenant.OperadorId, ct);
            var zonaInvalida = reglas.FirstOrDefault(r => r.ZonaCoberturaId.HasValue && !zonas.ContainsKey(r.ZonaCoberturaId.Value));
            if (zonaInvalida is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Una de las reglas hace referencia a una zona de cobertura que no existe.");

            var vigenteDesde = RespuestasBackoffice.ComoUtc(dto.VigenteDesde);

            // Se cierran las versiones abiertas: la nueva pasa a regir desde su fecha de inicio
            var abiertas = await db.CuadrosTarifarios
                .Where(c => c.OperadorId == tenant.OperadorId && c.VigenteHasta == null)
                .ToListAsync(ct);
            foreach (var abierta in abiertas)
                abierta.CerrarVigencia(vigenteDesde < abierta.VigenteDesde ? abierta.VigenteDesde : vigenteDesde);

            var ultimaVersion = await db.CuadrosTarifarios
                .Where(c => c.OperadorId == tenant.OperadorId)
                .MaxAsync(c => (int?)c.Version, ct) ?? 0;

            var cuadro = new CuadroTarifario(Guid.NewGuid(), tenant.OperadorId, ultimaVersion + 1, vigenteDesde);
            cuadro.RegistrarComentario(dto.Comentario);
            foreach (var r in reglas)
            {
                var regla = new ReglaTarifa(Guid.NewGuid(), cuadro.Id, r.ModalidadServicio, r.PesoDesde, r.PesoHasta,
                    r.VolumenDesde, r.VolumenHasta, r.PrecioBase, r.Recargo, r.Bonificacion, r.ZonaCoberturaId);
                cuadro.AgregarRegla(regla);
                db.ReglasTarifa.Add(regla);
            }

            await db.CuadrosTarifarios.AddAsync(cuadro, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/cuadros-tarifarios/{cuadro.Id}", Mapear(cuadro, zonas));
        }))
        .WithName("PublicarCuadroTarifario")
        .WithSummary("Publicar una nueva versión del cuadro tarifario");

        grupo.MapPost("/simular", (
            [FromBody] SimularTarifaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vigente = await ObtenerVigenteAsync(db, tenant.OperadorId, ct);
            if (vigente is null || vigente.ReglasTarifa.Count == 0)
            {
                return Results.Ok(new CotizacionSimuladaDto
                {
                    Exitoso = false,
                    Mensaje = "No hay ningún cuadro tarifario vigente disponible para calcular precios."
                });
            }

            var regla = vigente.ObtenerReglaAplicable(dto.PesoKg, dto.VolumenM3, dto.ModalidadServicio, dto.ZonaCoberturaId);
            if (regla is null)
            {
                return Results.Ok(new CotizacionSimuladaDto
                {
                    Exitoso = false,
                    VersionCuadroAplicada = vigente.Version,
                    Mensaje = "Ninguna regla de la versión vigente cubre ese peso, volumen y modalidad de servicio."
                });
            }

            var zonas = await NombresDeZonasAsync(db, tenant.OperadorId, ct);
            var ambito = regla.ZonaCoberturaId.HasValue && zonas.TryGetValue(regla.ZonaCoberturaId.Value, out var nombreZona) ? nombreZona : "Todas las zonas";

            return Results.Ok(new CotizacionSimuladaDto
            {
                Exitoso = true,
                PrecioFinal = regla.CalcularPrecioFinal(),
                PrecioBase = regla.PrecioBase,
                Recargo = regla.Recargo,
                Bonificacion = regla.Bonificacion,
                VersionCuadroAplicada = vigente.Version,
                ReglaAplicada = $"{regla.ModalidadServicio} ({ambito}) — Rango {regla.PesoDesde}kg a {regla.PesoHasta}kg",
                Mensaje = "Tarifa calculada con éxito bajo la versión vigente."
            });
        }))
        .WithName("SimularTarifa")
        .WithSummary("Cotizar un envío con la versión vigente del cuadro");

        return app;
    }

    private static async Task<CuadroTarifario?> ObtenerVigenteAsync(AppDbContext db, Guid operadorId, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        return await db.CuadrosTarifarios.AsNoTracking()
            .Where(c => c.OperadorId == operadorId && c.VigenteDesde <= ahora && (c.VigenteHasta == null || c.VigenteHasta > ahora))
            .OrderByDescending(c => c.Version)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<Dictionary<Guid, string>> NombresDeZonasAsync(AppDbContext db, Guid operadorId, CancellationToken ct) =>
        await db.ZonasCobertura.AsNoTracking().Where(z => z.OperadorId == operadorId).ToDictionaryAsync(z => z.Id, z => z.Nombre, ct);

    private static CuadroTarifarioDto Mapear(CuadroTarifario cuadro, IReadOnlyDictionary<Guid, string> zonas) => new()
    {
        Id = cuadro.Id,
        OperadorId = cuadro.OperadorId,
        Version = cuadro.Version,
        VigenteDesde = cuadro.VigenteDesde,
        VigenteHasta = cuadro.VigenteHasta,
        Comentario = cuadro.Comentario,
        ReglasTarifa = cuadro.ReglasTarifa.Select(r => new ReglaTarifaDto
        {
            Id = r.Id,
            CuadroTarifarioId = r.CuadroTarifarioId,
            ZonaCoberturaId = r.ZonaCoberturaId,
            NombreZona = r.ZonaCoberturaId.HasValue && zonas.TryGetValue(r.ZonaCoberturaId.Value, out var nombre) ? nombre : null,
            ModalidadServicio = r.ModalidadServicio,
            PesoDesde = r.PesoDesde,
            PesoHasta = r.PesoHasta,
            VolumenDesde = r.VolumenDesde,
            VolumenHasta = r.VolumenHasta,
            PrecioBase = r.PrecioBase,
            Recargo = r.Recargo,
            Bonificacion = r.Bonificacion
        }).ToList()
    };
}
