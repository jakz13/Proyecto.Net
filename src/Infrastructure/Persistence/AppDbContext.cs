using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Entities.Envios;

namespace UltimaMilla.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Envio> Envios => Set<Envio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Envio>(builder =>
        {
            builder.ToTable("Envios");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.ComercioId)
                .IsRequired();

            builder.Property(e => e.DireccionDestino)
                .IsRequired()
                .HasMaxLength(300);

            builder.Property(e => e.NombreDestinatario)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.FechaCreacion)
                .IsRequired();

            builder.Property(e => e.Estado)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);
        });
    }
}
