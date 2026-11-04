using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Entities.Operador;

namespace UltimaMilla.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de Entity Framework Core para el repositorio de Zonas de Cobertura y sus Franjas.
/// </summary>
public class EfZonaCoberturaRepository : IZonaCoberturaRepository
{
    private readonly AppDbContext _contexto;

    public EfZonaCoberturaRepository(AppDbContext contexto)
    {
        _contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
    }

    public async Task<ZonaCobertura?> ObtenerPorIdAsync(Guid id, Guid operadorId, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || operadorId == Guid.Empty)
            return null;

        return await _contexto.ZonasCobertura
            .Include(z => z.FranjasHorarias)
            .FirstOrDefaultAsync(z => z.Id == id && z.OperadorId == operadorId, cancellationToken);
    }

    public async Task<bool> ExisteNombreAsync(string nombre, Guid operadorId, Guid? excluirZonaId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nombre) || operadorId == Guid.Empty)
            return false;

        var normalizado = nombre.Trim().ToLowerInvariant();
        var consulta = _contexto.ZonasCobertura.AsNoTracking()
            .Where(z => z.OperadorId == operadorId && z.Nombre.ToLower() == normalizado);

        if (excluirZonaId.HasValue && excluirZonaId.Value != Guid.Empty)
        {
            consulta = consulta.Where(z => z.Id != excluirZonaId.Value);
        }

        return await consulta.AnyAsync(cancellationToken);
    }

    public async Task AgregarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zona);
        await _contexto.ZonasCobertura.AddAsync(zona, cancellationToken);
    }

    public Task ActualizarAsync(ZonaCobertura zona, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zona);
        _contexto.ZonasCobertura.Update(zona);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ZonaCobertura>> ListarZonasDelOperadorAsync(
        Guid operadorId,
        string? busqueda,
        bool? soloActivas,
        CancellationToken cancellationToken = default)
    {
        var consulta = _contexto.ZonasCobertura
            .Include(z => z.FranjasHorarias)
            .AsNoTracking()
            .Where(z => z.OperadorId == operadorId);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var texto = busqueda.Trim();
            var patron = "%" + texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(z => EF.Functions.ILike(z.Nombre, patron, "\\"));
        }

        if (soloActivas.HasValue)
        {
            consulta = consulta.Where(z => z.Activa == soloActivas.Value);
        }

        return await consulta
            .OrderBy(z => z.Nombre)
            .ToListAsync(cancellationToken);
    }

    public Task ReemplazarFranjasAsync(
        ZonaCobertura zona,
        IEnumerable<FranjaHoraria> nuevasFranjas,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zona);
        ArgumentNullException.ThrowIfNull(nuevasFranjas);

        var franjasExistentes = zona.FranjasHorarias.ToList();
        if (franjasExistentes.Count > 0)
        {
            _contexto.FranjasHorarias.RemoveRange(franjasExistentes);
        }

        zona.QuitarFranjasHorarias();

        foreach (var franja in nuevasFranjas)
        {
            zona.AgregarFranjaHoraria(franja);
            _contexto.FranjasHorarias.Add(franja);
        }

        return Task.CompletedTask;
    }
}

