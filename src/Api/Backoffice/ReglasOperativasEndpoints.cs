using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Api.Backoffice;

/// <summary>Reglas operativas versionadas del operador (SLA, reintentos, prueba de entrega y política de devolución).</summary>
public static class ReglasOperativasEndpoints
{
    public static IEndpointRouteBuilder MapReglasOperativasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/reglas-operativas")
            .WithTags("Backoffice · Reglas operativas")
            .RequireAuthorization();

        grupo.MapGet("/", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var reglas = await db.ReglasOperativas.AsNoTracking()
                .Where(r => r.OperadorId == tenant.OperadorId)
                .OrderByDescending(r => r.Version)
                .ToListAsync(ct);
            return Results.Ok(reglas.Select(Mapear).ToList());
        }))
        .WithName("ListarReglasOperativas")
        .WithSummary("Historial de versiones de las reglas operativas");

        grupo.MapGet("/vigente", (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vigente = await ObtenerVigenteAsync(db, tenant.OperadorId, ct);
            return vigente is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No hay una versión de reglas operativas vigente.")
                : Results.Ok(Mapear(vigente));
        }))
        .WithName("ObtenerReglaOperativaVigente")
        .WithSummary("Versión vigente de las reglas operativas");

        grupo.MapGet("/{id:guid}", (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var regla = await db.ReglasOperativas.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.OperadorId == tenant.OperadorId, ct);
            return regla is null
                ? RespuestasBackoffice.Error(StatusCodes.Status404NotFound, "No se encontró la versión de reglas operativas.")
                : Results.Ok(Mapear(regla));
        }))
        .WithName("ObtenerReglaOperativa")
        .WithSummary("Detalle de una versión de reglas operativas");

        grupo.MapPost("/", (
            [FromBody] CrearReglaOperativaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            if (dto.HorasEntreIntentos < 0 || dto.HorasPlazoComprometidoEstandar <= 0 || dto.HorasPlazoComprometidoUrgente <= 0)
                return RespuestasBackoffice.Error(StatusCodes.Status400BadRequest, "Los plazos deben ser mayores a cero (las horas entre intentos pueden ser cero).");

            var vigenteDesde = RespuestasBackoffice.ComoUtc(dto.VigenteDesde);

            var abiertas = await db.ReglasOperativas
                .Where(r => r.OperadorId == tenant.OperadorId && r.VigenteHasta == null)
                .ToListAsync(ct);
            var anterior = abiertas.OrderByDescending(r => r.Version).FirstOrDefault();
            foreach (var abierta in abiertas)
                abierta.FinalizarVigencia(vigenteDesde < abierta.VigenteDesde ? abierta.VigenteDesde : vigenteDesde);

            var ultimaVersion = await db.ReglasOperativas
                .Where(r => r.OperadorId == tenant.OperadorId)
                .MaxAsync(r => (int?)r.Version, ct) ?? 0;

            var nueva = new ReglaOperativa(
                Guid.NewGuid(), tenant.OperadorId, ultimaVersion + 1, vigenteDesde,
                dto.MaxIntentosEntrega,
                TimeSpan.FromHours(dto.HorasEntreIntentos),
                dto.TipoPruebaEntregaExigida,
                dto.PoliticaDevolucion,
                TimeSpan.FromHours(dto.HorasPlazoComprometidoEstandar),
                TimeSpan.FromHours(dto.HorasPlazoComprometidoUrgente),
                maxParadasPorRuta: anterior?.MaxParadasPorRuta ?? 50);
            nueva.RegistrarComentario(dto.Comentario);

            await db.ReglasOperativas.AddAsync(nueva, ct);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/reglas-operativas/{nueva.Id}", Mapear(nueva));
        }))
        .WithName("PublicarReglaOperativa")
        .WithSummary("Publicar una nueva versión de las reglas operativas");

        grupo.MapPost("/simular", (
            [FromBody] SimularReglaOperativaDto dto,
            AppDbContext db,
            ICurrentTenant tenant,
            CancellationToken ct) => RespuestasBackoffice.Ejecutar(async () =>
        {
            var vigente = await ObtenerVigenteAsync(db, tenant.OperadorId, ct);
            if (vigente is null)
            {
                return Results.Ok(new ResultadoSimulacionReglasDto
                {
                    Exitoso = false,
                    AccionSugerida = "No hay versión de reglas operativas vigente configurada."
                });
            }

            var modalidad = dto.ModalidadServicio ?? string.Empty;
            var esUrgente = modalidad.Contains("Express", StringComparison.OrdinalIgnoreCase)
                || modalidad.Contains("Urgente", StringComparison.OrdinalIgnoreCase)
                || modalidad.Contains("Mismo Día", StringComparison.OrdinalIgnoreCase);

            var ahora = DateTime.UtcNow;
            var plazo = esUrgente ? vigente.PlazoComprometidoUrgente : vigente.PlazoComprometidoEstandar;
            var limiteSla = dto.FechaDespacho.Add(plazo);
            var superaMaximo = vigente.SuperaMaximoIntentos(dto.IntentosRealizados);
            var restantes = vigente.CalcularIntentosRestantes(dto.IntentosRealizados);
            var horasEntreIntentos = (int)vigente.PlazoEntreIntentos.TotalHours;

            DateTime? proximaFecha = null;
            var puedeReintentar = false;
            string accion;

            if (superaMaximo)
            {
                accion = $"Se alcanzó el máximo de {vigente.MaxIntentosEntrega} intentos fallidos. Debe activarse la política de devolución: {vigente.PoliticaDevolucion}.";
            }
            else if (dto.FechaUltimoIntento.HasValue)
            {
                proximaFecha = dto.FechaUltimoIntento.Value.Add(vigente.PlazoEntreIntentos);
                // Las fechas de la simulación se ingresan sin zona horaria: se comparan con la hora actual del servidor en la misma escala
                puedeReintentar = dto.FechaUltimoIntento.Value.Kind == DateTimeKind.Utc
                    ? ahora >= proximaFecha.Value
                    : DateTime.Now >= proximaFecha.Value;
                accion = puedeReintentar
                    ? $"Habilitado para nuevo intento de entrega ({restantes} restantes). Se cumplió el plazo de {horasEntreIntentos}h entre intentos."
                    : $"En espera reglamentaria. Próximo intento habilitado el {proximaFecha.Value:dd/MM/yyyy HH:mm} (regla de {horasEntreIntentos}h entre intentos).";
            }
            else
            {
                puedeReintentar = true;
                accion = $"Envío en curso para primer intento. Entrega comprometida antes de {limiteSla:dd/MM/yyyy HH:mm}.";
            }

            var referencia = dto.FechaDespacho.Kind == DateTimeKind.Utc ? ahora : DateTime.Now;

            return Results.Ok(new ResultadoSimulacionReglasDto
            {
                Exitoso = true,
                VersionAplicada = vigente.Version,
                MaxIntentosPermitidos = vigente.MaxIntentosEntrega,
                IntentosRestantes = restantes,
                SuperaMaximoIntentos = superaMaximo,
                ProximaFechaReintentoPermitida = proximaFecha,
                PuedeReintentarAhora = puedeReintentar,
                FechaLimiteSla = limiteSla,
                SlaVencido = referencia > limiteSla,
                TipoPruebaExigida = vigente.TipoPruebaEntregaExigida,
                PoliticaDevolucion = vigente.PoliticaDevolucion,
                AccionSugerida = accion
            });
        }))
        .WithName("SimularReglasOperativas")
        .WithSummary("Simular el comportamiento de un envío bajo la versión vigente");

        return app;
    }

    private static async Task<ReglaOperativa?> ObtenerVigenteAsync(AppDbContext db, Guid operadorId, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        return await db.ReglasOperativas.AsNoTracking()
            .Where(r => r.OperadorId == operadorId && r.VigenteDesde <= ahora && (r.VigenteHasta == null || r.VigenteHasta > ahora))
            .OrderByDescending(r => r.Version)
            .FirstOrDefaultAsync(ct);
    }

    private static ReglaOperativaDto Mapear(ReglaOperativa r) => new()
    {
        Id = r.Id,
        OperadorId = r.OperadorId,
        Version = r.Version,
        VigenteDesde = r.VigenteDesde,
        VigenteHasta = r.VigenteHasta,
        MaxIntentosEntrega = r.MaxIntentosEntrega,
        PlazoEntreIntentos = r.PlazoEntreIntentos,
        TipoPruebaEntregaExigida = r.TipoPruebaEntregaExigida,
        PoliticaDevolucion = r.PoliticaDevolucion,
        PlazoComprometidoEstandar = r.PlazoComprometidoEstandar,
        PlazoComprometidoUrgente = r.PlazoComprometidoUrgente,
        Comentario = r.Comentario
    };
}
