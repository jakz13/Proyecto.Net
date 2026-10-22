using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos.Flota;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Repartidores y vehículos del operador, y la asignación entre ambos.</summary>
public static class FlotaEndpoints
{
    private static readonly string[] EstadosRepartidor = { "Disponible", "En Ruta", "Inactivo" };

    public static IEndpointRouteBuilder MapFlotaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/flota")
            .WithTags("Backoffice · Flota")
            .RequireAuthorization();

        MapVehiculos(grupo);
        MapRepartidores(grupo);
        return app;
    }

    // ───────────────────────────── Vehículos ─────────────────────────────

    private static void MapVehiculos(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/vehiculos", (
            [FromQuery] bool? soloActivos,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = db.Vehiculos.AsNoTracking().Where(v => v.OperadorId == tenant.OperadorId);
            if (soloActivos.HasValue)
                consulta = consulta.Where(v => v.Activo == soloActivos.Value);

            var vehiculos = await consulta.OrderBy(v => v.Patente).ToListAsync(ct);
            return Results.Ok(await MapearVehiculosAsync(db, tenant.OperadorId, vehiculos, ct));
        }))
        .WithName("ListarVehiculos")
        .WithSummary("Listar los vehículos del operador");

        grupo.MapGet("/vehiculos/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vehiculo = await db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.OperadorId == tenant.OperadorId, ct);
            return vehiculo is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el vehículo solicitado.")
                : Results.Ok((await MapearVehiculosAsync(db, tenant.OperadorId, new[] { vehiculo }, ct)).Single());
        }))
        .WithName("ObtenerVehiculo")
        .WithSummary("Detalle de un vehículo");

        grupo.MapPost("/vehiculos", (
            [FromBody] CrearVehiculoDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var patente = (dto.Patente ?? string.Empty).Trim().ToUpperInvariant();
            if (await db.Vehiculos.AnyAsync(v => v.OperadorId == tenant.OperadorId && v.Patente == patente, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"Ya existe un vehículo con la patente {patente}.");

            var vehiculo = new Vehiculo(Guid.NewGuid(), tenant.OperadorId, patente, dto.Tipo, dto.CapacidadPeso, dto.CapacidadVolumen);
            await db.Vehiculos.AddAsync(vehiculo, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/flota/vehiculos/{vehiculo.Id}",
                (await MapearVehiculosAsync(db, tenant.OperadorId, new[] { vehiculo }, ct)).Single());
        }))
        .WithName("CrearVehiculo")
        .WithSummary("Dar de alta un vehículo");

        grupo.MapPut("/vehiculos/{id:guid}", (
            Guid id,
            [FromBody] ActualizarVehiculoDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.Id == id && v.OperadorId == tenant.OperadorId, ct);
            if (vehiculo is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el vehículo solicitado.");

            var patente = (dto.Patente ?? string.Empty).Trim().ToUpperInvariant();
            if (await db.Vehiculos.AnyAsync(v => v.OperadorId == tenant.OperadorId && v.Patente == patente && v.Id != id, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, $"Ya existe otro vehículo con la patente {patente}.");

            vehiculo.ActualizarDatos(patente, dto.Tipo);
            vehiculo.ActualizarCapacidades(dto.CapacidadPeso, dto.CapacidadVolumen);
            if (dto.Activo) vehiculo.Activar(); else vehiculo.Desactivar();
            await db.SaveChangesAsync(ct);

            return Results.Ok((await MapearVehiculosAsync(db, tenant.OperadorId, new[] { vehiculo }, ct)).Single());
        }))
        .WithName("ActualizarVehiculo")
        .WithSummary("Modificar un vehículo");

        grupo.MapPatch("/vehiculos/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoActivoRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vehiculo = await db.Vehiculos.FirstOrDefaultAsync(v => v.Id == id && v.OperadorId == tenant.OperadorId, ct);
            if (vehiculo is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el vehículo solicitado.");

            if (cuerpo.Activo) vehiculo.Activar(); else vehiculo.Desactivar();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoVehiculo")
        .WithSummary("Dar de alta o baja lógica a un vehículo");
    }

    // ───────────────────────────── Repartidores ─────────────────────────────

    private static void MapRepartidores(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/repartidores", (
            [FromQuery] string? estado,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var consulta = db.Repartidores.AsNoTracking().Where(r => r.OperadorId == tenant.OperadorId);
            if (!string.IsNullOrWhiteSpace(estado))
            {
                var estadoNormalizado = estado.Trim().ToLower();
                consulta = consulta.Where(r => r.Estado.ToLower() == estadoNormalizado);
            }

            var repartidores = await consulta.ToListAsync(ct);
            var dtos = await MapearRepartidoresAsync(db, repartidores, ct);
            return Results.Ok(dtos.OrderBy(r => r.NombreCompleto).ToList());
        }))
        .WithName("ListarRepartidores")
        .WithSummary("Listar los repartidores del operador");

        grupo.MapGet("/repartidores/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var repartidor = await db.Repartidores.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OperadorId == tenant.OperadorId, ct);
            return repartidor is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el repartidor solicitado.")
                : Results.Ok((await MapearRepartidoresAsync(db, new[] { repartidor }, ct)).Single());
        }))
        .WithName("ObtenerRepartidor")
        .WithSummary("Detalle de un repartidor");

        grupo.MapPost("/repartidores", (
            [FromBody] CrearRepartidorDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            IPasswordHasherService hasher,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var estado = NormalizarEstado(dto.Estado);
            if (estado is null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El estado del repartidor no es válido.");

            var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe un usuario registrado con ese correo electrónico.");

            var errorVehiculo = await ValidarVehiculoAsync(db, tenant.OperadorId, dto.VehiculoAsignadoId, null, ct);
            if (errorVehiculo is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, errorVehiculo);

            var perfil = await db.Perfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Nombre == "Repartidor", ct);
            if (perfil is null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "No está configurado el perfil Repartidor.");

            // El repartidor ingresa a la app con un usuario propio; la contraseña provisional se cambia en el primer ingreso
            var usuario = new Usuario(Guid.NewGuid(), dto.NombreCompleto, email, hasher.HashearPassword(Guid.NewGuid().ToString("N")),
                perfil.Id, tenant.OperadorId);
            var repartidor = new Repartidor(Guid.NewGuid(), usuario.Id, tenant.OperadorId, dto.Documento, dto.Telefono, dto.VehiculoAsignadoId, estado);

            await db.Usuarios.AddAsync(usuario, ct);
            await db.Repartidores.AddAsync(repartidor, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/flota/repartidores/{repartidor.Id}",
                (await MapearRepartidoresAsync(db, new[] { repartidor }, ct)).Single());
        }))
        .WithName("CrearRepartidor")
        .WithSummary("Dar de alta un repartidor");

        grupo.MapPut("/repartidores/{id:guid}", (
            Guid id,
            [FromBody] ActualizarRepartidorDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var repartidor = await db.Repartidores.FirstOrDefaultAsync(r => r.Id == id && r.OperadorId == tenant.OperadorId, ct);
            if (repartidor is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el repartidor solicitado.");

            var estado = NormalizarEstado(dto.Estado);
            if (estado is null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El estado del repartidor no es válido.");

            var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == repartidor.UsuarioId, ct);
            var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (usuario is not null && await db.Usuarios.AnyAsync(u => u.Email == email && u.Id != usuario.Id, ct))
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Ya existe otro usuario con ese correo electrónico.");

            var errorVehiculo = await ValidarVehiculoAsync(db, tenant.OperadorId, dto.VehiculoAsignadoId, repartidor.Id, ct);
            if (errorVehiculo is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, errorVehiculo);

            usuario?.ActualizarDatos(dto.NombreCompleto, email);
            repartidor.ActualizarDocumento(dto.Documento);
            repartidor.ActualizarContacto(dto.Telefono);
            repartidor.CambiarEstado(estado);
            if (dto.VehiculoAsignadoId.HasValue) repartidor.AsignarVehiculo(dto.VehiculoAsignadoId.Value); else repartidor.DesasignarVehiculo();
            await db.SaveChangesAsync(ct);

            return Results.Ok((await MapearRepartidoresAsync(db, new[] { repartidor }, ct)).Single());
        }))
        .WithName("ActualizarRepartidor")
        .WithSummary("Modificar un repartidor");

        grupo.MapPut("/repartidores/{id:guid}/vehiculo", (
            Guid id,
            [FromBody] AsignarVehiculoRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var repartidor = await db.Repartidores.FirstOrDefaultAsync(r => r.Id == id && r.OperadorId == tenant.OperadorId, ct);
            if (repartidor is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el repartidor solicitado.");

            var errorVehiculo = await ValidarVehiculoAsync(db, tenant.OperadorId, cuerpo.VehiculoId, repartidor.Id, ct);
            if (errorVehiculo is not null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, errorVehiculo);

            if (cuerpo.VehiculoId.HasValue) repartidor.AsignarVehiculo(cuerpo.VehiculoId.Value); else repartidor.DesasignarVehiculo();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("AsignarVehiculoARepartidor")
        .WithSummary("Asignar o quitar el vehículo de un repartidor");

        grupo.MapPatch("/repartidores/{id:guid}/estado", (
            Guid id,
            [FromBody] CambioEstadoRepartidorRequest cuerpo,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var repartidor = await db.Repartidores.FirstOrDefaultAsync(r => r.Id == id && r.OperadorId == tenant.OperadorId, ct);
            if (repartidor is null)
                return RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró el repartidor solicitado.");

            var estado = NormalizarEstado(cuerpo.NuevoEstado);
            if (estado is null)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "El estado del repartidor no es válido.");

            repartidor.CambiarEstado(estado);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }))
        .WithName("CambiarEstadoRepartidor")
        .WithSummary("Cambiar el estado operativo de un repartidor");
    }

    private static string? NormalizarEstado(string? estado) =>
        EstadosRepartidor.FirstOrDefault(e => e.Equals((estado ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>El vehículo debe existir, estar activo y no estar asignado a otro repartidor.</summary>
    private static async Task<string?> ValidarVehiculoAsync(AppDbContext db, Guid operadorId, Guid? vehiculoId, Guid? repartidorId, CancellationToken ct)
    {
        if (!vehiculoId.HasValue)
            return null;

        var vehiculo = await db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehiculoId.Value && v.OperadorId == operadorId, ct);
        if (vehiculo is null)
            return "El vehículo seleccionado no existe.";
        if (!vehiculo.Activo)
            return $"El vehículo {vehiculo.Patente} está dado de baja y no se puede asignar.";

        var ocupado = await db.Repartidores.AnyAsync(r => r.VehiculoAsignadoId == vehiculoId.Value && r.Id != repartidorId, ct);
        return ocupado ? $"El vehículo {vehiculo.Patente} ya está asignado a otro repartidor." : null;
    }

    private static async Task<List<VehiculoDto>> MapearVehiculosAsync(AppDbContext db, Guid operadorId, IReadOnlyCollection<Vehiculo> vehiculos, CancellationToken ct)
    {
        var ids = vehiculos.Select(v => v.Id).ToList();
        var asignaciones = await db.Repartidores.AsNoTracking()
            .Where(r => r.OperadorId == operadorId && r.VehiculoAsignadoId != null && ids.Contains(r.VehiculoAsignadoId.Value))
            .ToListAsync(ct);
        var nombres = await NombresDeUsuariosAsync(db, asignaciones.Select(a => a.UsuarioId), ct);

        return vehiculos.Select(v =>
        {
            var asignado = asignaciones.FirstOrDefault(a => a.VehiculoAsignadoId == v.Id);
            return new VehiculoDto
            {
                Id = v.Id,
                OperadorId = v.OperadorId.ToString(),
                Patente = v.Patente,
                Tipo = v.Tipo,
                CapacidadPeso = v.CapacidadPeso,
                CapacidadVolumen = v.CapacidadVolumen,
                Activo = v.Activo,
                RepartidorAsignadoId = asignado?.Id,
                RepartidorAsignadoNombre = asignado is null ? null : nombres.GetValueOrDefault(asignado.UsuarioId)?.Nombre
            };
        }).ToList();
    }

    private static async Task<List<RepartidorDto>> MapearRepartidoresAsync(AppDbContext db, IReadOnlyCollection<Repartidor> repartidores, CancellationToken ct)
    {
        var usuarios = await NombresDeUsuariosAsync(db, repartidores.Select(r => r.UsuarioId), ct);
        var vehiculoIds = repartidores.Where(r => r.VehiculoAsignadoId.HasValue).Select(r => r.VehiculoAsignadoId!.Value).Distinct().ToList();
        var vehiculos = await db.Vehiculos.AsNoTracking().Where(v => vehiculoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, ct);

        return repartidores.Select(r =>
        {
            var usuario = usuarios.GetValueOrDefault(r.UsuarioId);
            var vehiculo = r.VehiculoAsignadoId.HasValue ? vehiculos.GetValueOrDefault(r.VehiculoAsignadoId.Value) : null;
            return new RepartidorDto
            {
                Id = r.Id,
                UsuarioId = r.UsuarioId,
                OperadorId = r.OperadorId.ToString(),
                NombreCompleto = usuario?.Nombre ?? string.Empty,
                Email = usuario?.Email ?? string.Empty,
                Documento = r.Documento,
                Telefono = r.Telefono,
                Estado = r.Estado,
                VehiculoAsignadoId = r.VehiculoAsignadoId,
                VehiculoPatente = vehiculo?.Patente,
                VehiculoTipo = vehiculo?.Tipo,
                VehiculoCapacidad = vehiculo is null ? null : $"{vehiculo.CapacidadPeso:0.##} kg · {vehiculo.CapacidadVolumen:0.##} m³"
            };
        }).ToList();
    }

    private static async Task<Dictionary<Guid, Usuario>> NombresDeUsuariosAsync(AppDbContext db, IEnumerable<Guid> usuarioIds, CancellationToken ct)
    {
        var ids = usuarioIds.Distinct().ToList();
        return await db.Usuarios.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }
}

public record AsignarVehiculoRequest(Guid? VehiculoId);

public record CambioEstadoRepartidorRequest(string? NuevoEstado);
