using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Exceptions;
using UltimaMilla.Domain.Exceptions;
using UltimaMilla.Frontend.Contracts.Modelos;

namespace UltimaMilla.Api.Backoffice;

/// <summary>
/// Utilidades comunes de los endpoints de administración del backoffice.
/// Los errores se responden como texto plano porque los clientes HTTP del frontend muestran el cuerpo tal cual al usuario.
/// </summary>
internal static class RespuestasBackoffice
{
    public const int TamanoPaginaMaximo = 100;

    public static IResult Error(int codigo, string mensaje) =>
        Results.Text(mensaje, "text/plain; charset=utf-8", System.Text.Encoding.UTF8, codigo);

    /// <summary>Ejecuta la acción traduciendo las excepciones de dominio y de concurrencia a respuestas HTTP.</summary>
    public static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (DomainValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (NotFoundException ex)
        {
            return Error(StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ConflictoDeConcurrenciaException ex)
        {
            return Error(StatusCodes.Status409Conflict, ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error(StatusCodes.Status409Conflict, "Otra persona modificó este registro, recargá y reintentá.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Error(StatusCodes.Status409Conflict, "Ya existe un registro con esos datos o fue creado al mismo tiempo por otra persona, recargá y reintentá.");
        }
    }

    /// <summary>PostgreSQL solo acepta fechas UTC: las que llegan sin zona horaria se interpretan como UTC.</summary>
    public static DateTime ComoUtc(DateTime fecha) => fecha.Kind switch
    {
        DateTimeKind.Utc => fecha,
        DateTimeKind.Local => fecha.ToUniversalTime(),
        _ => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
    };

    public static (int Pagina, int Tamano) NormalizarPagina(int? pagina, int? tamanoPagina)
    {
        var tamano = Math.Clamp(tamanoPagina ?? 10, 1, TamanoPaginaMaximo);
        return (Math.Max(pagina ?? 1, 1), tamano);
    }

    public static async Task<ResultadoPaginadoDto<T>> PaginarAsync<T>(IQueryable<T> consulta, int pagina, int tamano, CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return new ResultadoPaginadoDto<T>
        {
            Elementos = elementos,
            TotalRegistros = total,
            PaginaActual = pagina,
            TamanoPagina = tamano
        };
    }

    /// <summary>Escapa los comodines de LIKE para que la búsqueda del usuario se trate como texto literal.</summary>
    public static string PatronContiene(string texto) =>
        "%" + texto.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}

public record CambioEstadoActivoRequest(bool Activo);

public record CambioEstadoActivaRequest(bool Activa);
