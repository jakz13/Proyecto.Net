using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Planificacion;

namespace UltimaMilla.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<Bulto> Bultos => Set<Bulto>();
    public DbSet<HojaDeRuta> HojasDeRuta => Set<HojaDeRuta>();
    public DbSet<Parada> Paradas => Set<Parada>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Envio>(builder =>
        {
            builder.ToTable("Envios");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ComercioId).IsRequired();
            builder.Property(e => e.DireccionDestino).IsRequired().HasMaxLength(300);
            builder.Property(e => e.NombreDestinatario).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FechaCreacion).IsRequired();
            builder.Property(e => e.Estado).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Navigation(e => e.Bultos).AutoInclude();
        });

        modelBuilder.Entity<HojaDeRuta>(builder =>
        {
            builder.ToTable("HojasDeRuta");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Estado).IsRequired().HasMaxLength(50);
            builder.Navigation(h => h.Paradas).AutoInclude();
            builder.Navigation(h => h.Asignaciones).AutoInclude();
        });

        modelBuilder.Entity<Parada>(builder =>
        {
            builder.ToTable("Paradas");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Estado).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("OutboxMessages");
            builder.HasKey(o => o.Id);
        });
    }
}
