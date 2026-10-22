using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos;
using UltimaMilla.Frontend.Contracts.Modelos.Comercios;
using UltimaMilla.Infrastructure.Persistence;
using ComercioEntidad = UltimaMilla.Domain.Entities.Comercio.Comercio;

namespace UltimaMilla.Api.Backoffice;

/// <summary>ABM de comercios clientes del operador (UC-01).</summary>
public static class ComerciosEndpoints
{
    public static IEndpointRouteBuilder MapComerciosBackofficeEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/comercios")
            .WithTags("Backoffice · Comercios (UC-01)")
            .RequireAuthorization();

        grupo.MapGet("/", (
            [FromQuery] int? pagina,
            [FromQuery] int? tamanoPagina,
            [FromQuery] string? q,
            [FromQuery] bool? soloActivos,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var (paginaActual, tamano) = RespuestasBackoffice.NormalizarPagina(pagina, tamanoPagina);
            var consulta = ConsultarComercios(db, tenant.OperadorId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var patron = RespuestasBackoffice.PatronContiene(q);
                consulta = consulta.Where(x =>
                    EF.Functions.ILike(x.Comercio.RazonSocial, patron, "\\") ||
                    EF.Functions.ILike(x.Comercio.NombreFantasia, patron, "\\") ||
                    EF.Functions.ILike(x.Comercio.Documento, patron, "\\") ||
                    EF.Functions.ILike(x.Comercio.Email, patron, "\\"));
            }

            if (soloActivos == true)
                consulta = consulta.Where(x => x.Relacion.Estado == EstadoActivo);

            var pagina1 = await RespuestasBackoffice.PaginarAsync(
                consulta.OrderBy(x => x.Comercio.RazonSocial).ThenBy(x => x.Comercio.Id), paginaActual, tamano, ct);

            return Results.Ok(new ResultadoPaginadoDto<ComercioDto>
            {
                Elementos = pagina1.Elementos.Select(x => Mapear(x.Comercio, x.Relacion)).ToList(),
                TotalRegistros = pagina1.TotalRegistros,
                PaginaActual = pagina1.PaginaActual,
                TamanoPagina = pagina1.TamanoPagina
            });
        }))
        .WithName("ListarComercios")
        .WithSummary("Listar los comercios del operador con búsqueda y paginación");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            RespuestasBackoffice.Ejecutar(async () =>
            {
                var item = await ConsultarComercios(db, tenant.OperadorId).FirstOrDefaultAsync(x => x.Comercio.Id == id, ct);
                return item is null
                    ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el comercio solicitado.")
                    : Results.Ok(Mapear(item.Comercio, item.Relacion));
            }))
        .WithName("ObtenerComercio")
        .WithSummary("Detalle de un comercio");

        grupo.MapPost("/", (
            [FromBody] CrearComercioDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var documento = (dto.Rut ?? string.Empty).Trim();

            var comercio = await db.Comercios.FirstOrDefaultAsync(c => c.Documento == documento, ct);
            if (comercio is not null)
            {
                var yaVinculado = await db.ComerciosOperadores
                    .AnyAsync(co => co.ComercioId == comercio.Id && co.OperadorId == tenant.OperadorId, ct);
                if (yaVinculado)
                    return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe un comercio registrado con ese RUT.");
            }
            else
            {
                comercio = new ComercioEntidad(Guid.NewGuid(), dto.RazonSocial, documento, dto.Email, dto.Telefono, dto.NombreFantasia, dto.Direccion);
                await db.Comercios.AddAsync(comercio, ct);
            }

            // La clave de integración en claro no se almacena ni se devuelve aquí: el comercio la genera desde su portal
            var relacion = new ComercioOperador(
                Guid.NewGuid(), comercio.Id, tenant.OperadorId,
                ComercioOperador.CalcularHuellaClaveApi(Convert.ToHexString(RandomNumberGenerator.GetBytes(24))));
            await db.ComerciosOperadores.AddAsync(relacion, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/comercios/{comercio.Id}", Mapear(comercio, relacion));
        }))
        .WithName("CrearComercio")
        .WithSummary("Dar de alta un comercio cliente");

        grupo.MapPut("/{id:guid}", (
            Guid id,
            [FromBody] ActualizarComercioDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var item = await ConsultarComercios(db, tenant.OperadorId, seguimiento: true).FirstOrDefaultAsync(x => x.Comercio.Id == id, ct);
            if (item is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el comercio solicitado.");

            // Concurrencia optimista: si el sello no coincide, otra persona editó el comercio después de que se cargó la pantalla
            if (!Guid.TryParse(dto.SelloModificacion, out var sello) || sello != item.Comercio.SelloModificacion)
                throw new ConflictoDeConcurrenciaException("Otra persona modificó este registro, recargá y reintentá.");

            item.Comercio.ActualizarPerfil(dto.RazonSocial, dto.NombreFantasia, dto.Email, dto.Telefono, dto.Direccion);
            AplicarEstado(item.Relacion, dto.Activo);
            await db.SaveChangesAsync(ct);

            return Results.Ok(Mapear(item.Comercio, item.Relacion));
        }))
        .WithName("ActualizarComercio")
        .WithSummary("Modificar los datos de un comercio");

        grupo.MapPatch("/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var relacion = await db.ComerciosOperadores
                .FirstOrDefaultAsync(co => co.ComercioId == id && co.OperadorId == tenant.OperadorId, ct);
            if (relacion is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el comercio solicitado.");

            AplicarEstado(relacion, cuerpo.Activo);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoComercio")
        .WithSummary("Dar de alta o baja lógica a un comercio");

        return app;
    }

    private const string EstadoActivo = "Activo";

    private sealed class ComercioConRelacion
    {
        public required ComercioEntidad Comercio { get; init; }
        public required ComercioOperador Relacion { get; init; }
    }

    private static IQueryable<ComercioConRelacion> ConsultarComercios(AppDbContext db, Guid operadorId, bool seguimiento = false)
    {
        var comercios = seguimiento ? db.Comercios : db.Comercios.AsNoTracking();
        var relaciones = seguimiento ? db.ComerciosOperadores : db.ComerciosOperadores.AsNoTracking();

        return from relacion in relaciones
               join comercio in comercios on relacion.ComercioId equals comercio.Id
               where relacion.OperadorId == operadorId
               select new ComercioConRelacion { Comercio = comercio, Relacion = relacion };
    }

    private static void AplicarEstado(ComercioOperador relacion, bool activo)
    {
        if (activo)
            relacion.Activar();
        else
            relacion.Suspender();
    }

    private static ComercioDto Mapear(ComercioEntidad comercio, ComercioOperador relacion) => new()
    {
        Id = comercio.Id,
        RazonSocial = comercio.RazonSocial,
        NombreFantasia = comercio.NombreFantasia,
        Rut = comercio.Documento,
        Email = comercio.Email,
        Telefono = comercio.Telefono,
        Direccion = comercio.Direccion,
        Activo = relacion.EstaActivo,
        FechaAlta = relacion.FechaAlta,
        SelloModificacion = comercio.SelloModificacion.ToString()
    };
}
