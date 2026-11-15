using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Api.Auth;
using UltimaMilla.Application.Common;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Api.Hubs;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Endpoints;

/// <summary>
/// Banderas de funcionalidad por operador. Cualquier cliente autenticado consulta las de su operador; solo el administrador de la
/// plataforma (quien ofrece el servicio) las cambia, y cada cambio queda registrado con quién y cuándo.
/// </summary>
public static class FuncionalidadesEndpoints
{
    /// <summary>Evento del hub de tablero: una bandera de funcionalidad del operador cambió.</summary>
    public const string EventoFuncionalidadesCambiaron = "FuncionalidadesCambiaron";

    public static IEndpointRouteBuilder MapFuncionalidadesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/funcionalidades", async (
            [FromServices] IFuncionalidadesService funcionalidades,
            [FromServices] ICurrentTenant tenant,
            CancellationToken ct) => Results.Ok(await funcionalidades.ObtenerDelOperadorAsync(tenant.OperadorId, ct)))
        .RequireAuthorization()
        .WithTags("Funcionalidades por operador")
        .WithName("ObtenerFuncionalidadesDelOperador")
        .WithSummary("Funcionalidades habilitadas para el operador del usuario (la app móvil y las web ocultan lo deshabilitado)")
        .WithOpenApi();

        var plataforma = app.MapGroup("/api/plataforma/operadores")
            .WithTags("Plataforma · Funcionalidades por operador")
            .RequireAuthorization(PoliticasApi.Plataforma);

        plataforma.MapGet("/", async (AppDbContext db, [FromServices] IFuncionalidadesService funcionalidades, CancellationToken ct) =>
        {
            var operadores = await db.Operadores.IgnoreQueryFilters().AsNoTracking().OrderBy(o => o.NombreComercial).ToListAsync(ct);
            var filas = await db.BanderasFuncionalidad.AsNoTracking().ToListAsync(ct);

            return Results.Ok(operadores.Select(o => new
            {
                operadorId = o.Id,
                nombre = o.NombreComercial,
                funcionalidades = Funcionalidad.Catalogo.Select(c =>
                {
                    var fila = filas.FirstOrDefault(f => f.OperadorId == o.Id && f.Clave == c.Key);
                    return new
                    {
                        clave = c.Key,
                        nombre = c.Value,
                        habilitada = fila?.Habilitada ?? true,
                        modificadaPor = fila?.ModificadaPorNombre,
                        modificadaEn = fila?.ModificadaEn
                    };
                })
            }));
        })
        .WithName("ListarFuncionalidadesPorOperador")
        .WithOpenApi();

        // Alta de una empresa de envíos (operador). Nace siempre con su administrador inicial: con esa cuenta entra al Backoffice y crea los demás perfiles.
        plataforma.MapPost("/", async (
            [FromBody] CrearOperadorRequest request,
            AppDbContext db,
            [FromServices] IPasswordHasherService hasher,
            [FromServices] IFuncionalidadesService funcionalidades,
            CancellationToken ct) =>
        {
            var razonSocial = (request.RazonSocial ?? string.Empty).Trim();
            var nombreComercial = (request.NombreComercial ?? string.Empty).Trim();
            var admin = request.Administrador;
            if (razonSocial.Length == 0 || nombreComercial.Length == 0)
                return Results.BadRequest(new { error = "La razón social y el nombre comercial de la empresa son obligatorios." });

            // El administrador inicial es obligatorio: sin él nadie podría entrar a configurar la empresa
            if (admin is null || string.IsNullOrWhiteSpace(admin.Nombre) || string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
                return Results.BadRequest(new { error = "El administrador inicial (nombre, correo y contraseña) es obligatorio." });

            var email = admin.Email.Trim().ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                return Results.BadRequest(new { error = "El correo del administrador no es válido." });

            if (PoliticaDePassword.Validar(admin.Password) is { } motivoPassword)
                return Results.BadRequest(new { error = motivoPassword });

            if (await db.Operadores.IgnoreQueryFilters().AnyAsync(o => o.NombreComercial.ToLower() == nombreComercial.ToLower(), ct))
                return Results.BadRequest(new { error = "Ya existe una empresa de envíos con ese nombre comercial." });

            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
                return Results.BadRequest(new { error = "Ya existe un usuario registrado con ese correo electrónico." });

            var perfilAdministrador = await db.Perfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Nombre == "Administrador", ct);
            if (perfilAdministrador is null)
                return Results.BadRequest(new { error = "No está configurado el perfil Administrador." });

            try
            {
                var operador = new Operador(Guid.NewGuid(), razonSocial, nombreComercial);
                var usuario = new Usuario(Guid.NewGuid(), admin.Nombre.Trim(), email, hasher.HashearPassword(admin.Password.Trim()), perfilAdministrador.Id, operador.Id);

                // La plataforma escribe sobre un operador que no es el suyo: escritura administrativa explícita
                using var _ = db.HabilitarEscrituraAdministrativa();
                await db.Operadores.AddAsync(operador, ct);
                await db.Usuarios.AddAsync(usuario, ct);
                await db.SaveChangesAsync(ct);   // empresa y administrador se guardan juntos o no se guarda nada

                return Results.Created($"/api/plataforma/operadores/{operador.Id}", new
                {
                    operadorId = operador.Id,
                    nombre = operador.NombreComercial,
                    administrador = new { usuarioId = usuario.Id, email = usuario.Email, nombre = usuario.Nombre }
                });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (DbUpdateException)
            {
                // Otra alta simultánea con el mismo nombre o correo ganó la carrera (índices únicos)
                return Results.BadRequest(new { error = "Ya existe una empresa o un usuario con esos datos." });
            }
        })
        .WithName("CrearOperador")
        .WithSummary("Alta de una empresa de envíos con su administrador inicial obligatorio")
        .WithOpenApi();

        plataforma.MapPut("/{operadorId:guid}/funcionalidades/{clave}", async (
            [FromRoute] Guid operadorId,
            [FromRoute] string clave,
            [FromBody] CambiarFuncionalidadRequest request,
            HttpContext http,
            [FromServices] IFuncionalidadesService funcionalidades,
            [FromServices] ICurrentTenant tenant,
            [FromServices] IHubContext<TableroHub> hub,
            [FromServices] IHubContext<SeguimientoHub> hubSeguimiento,
            CancellationToken ct) =>
        {
            try
            {
                var nombre = http.User.FindFirstValue(ClaimTypes.Name) ?? http.User.FindFirstValue("email");
                var cambio = await funcionalidades.EstablecerAsync(operadorId, clave, request.Habilitada, tenant.UsuarioId, nombre, ct);

                // Las sesiones abiertas del operador afectado (personal y comercios) se enteran en el momento: aviso visible y pantalla bloqueada o liberada
                if (cambio)
                {
                    try
                    {
                        await hub.Clients.Group(TableroHub.GrupoIdentidadOperador(operadorId)).SendAsync(
                            EventoFuncionalidadesCambiaron,
                            new
                            {
                                clave,
                                nombre = Funcionalidad.Catalogo.GetValueOrDefault(clave, clave),
                                habilitada = request.Habilitada,
                                timestamp = DateTime.UtcNow
                            },
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Un fallo del transporte no deshace un cambio que ya se guardó: las pantallas lo verán en la próxima consulta
                    }

                    // El seguimiento público (sin sesión) vuelve a consultar y muestra u oculta el botón de reprogramar sin recargar
                    try
                    {
                        await hubSeguimiento.Clients.Group(SeguimientoHub.GrupoOperador(operadorId)).SendAsync(
                            UltimaMilla.Api.Services.SignalRTableroNotifier.EventoSeguimientoActualizado, new { timestamp = DateTime.UtcNow }, CancellationToken.None);
                    }
                    catch
                    {
                        // Idem: no deshace el cambio
                    }
                }

                return Results.Ok(new { operadorId, clave, habilitada = request.Habilitada, cambio });
            }
            catch (DomainValidationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CambiarFuncionalidadDelOperador")
        .WithSummary("Activa o desactiva una funcionalidad para un operador, sin reiniciar nada")
        .WithOpenApi();

        plataforma.MapGet("/{operadorId:guid}/funcionalidades/historial", async (
            [FromRoute] Guid operadorId,
            [FromServices] IFuncionalidadesService funcionalidades,
            CancellationToken ct) => Results.Ok(await funcionalidades.HistorialAsync(operadorId, ct)))
        .WithName("HistorialFuncionalidadesDelOperador")
        .WithOpenApi();

        return app;
    }
}

public record CambiarFuncionalidadRequest(bool Habilitada);

public record AdministradorInicialRequest(string? Nombre, string? Email, string? Password);

public record CrearOperadorRequest(string? RazonSocial, string? NombreComercial, AdministradorInicialRequest? Administrador);
