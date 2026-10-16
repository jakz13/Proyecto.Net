using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
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
    public DbSet<IntentoEntrega> IntentosEntrega => Set<IntentoEntrega>();
    public DbSet<PruebaDeEntrega> PruebasDeEntrega => Set<PruebaDeEntrega>();
    public DbSet<ReglaOperativa> ReglasOperativas => Set<ReglaOperativa>();
    public DbSet<MotivoNoEntrega> MotivosNoEntrega => Set<MotivoNoEntrega>();
    public DbSet<EventoEnvio> EventosEnvio => Set<EventoEnvio>();

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
            builder.Navigation(e => e.Eventos).AutoInclude();
            builder.Navigation(e => e.IntentosEntrega).AutoInclude();
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

        modelBuilder.Entity<IntentoEntrega>(builder =>
        {
            builder.ToTable("IntentosEntrega");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Resultado).IsRequired().HasMaxLength(50);
            builder.HasOne(i => i.PruebaDeEntrega)
                   .WithOne()
                   .HasForeignKey<PruebaDeEntrega>(p => p.IntentoEntregaId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.PruebaDeEntrega).AutoInclude();
        });

        modelBuilder.Entity<PruebaDeEntrega>(builder =>
        {
            builder.ToTable("PruebasDeEntrega");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Tipo).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<ReglaOperativa>(builder =>
        {
            builder.ToTable("ReglasOperativas");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.TipoPruebaEntregaExigida).IsRequired().HasMaxLength(200);
            builder.Property(r => r.PoliticaDevolucion).IsRequired().HasMaxLength(500);
        });

        modelBuilder.Entity<MotivoNoEntrega>(builder =>
        {
            builder.ToTable("MotivosNoEntrega");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Codigo).IsRequired().HasMaxLength(50);
            builder.Property(m => m.Descripcion).IsRequired().HasMaxLength(250);
        });

        modelBuilder.Entity<EventoEnvio>(builder =>
        {
            builder.ToTable("EventosEnvio");
            builder.HasKey(ev => ev.Id);
            builder.Property(ev => ev.TipoEvento).IsRequired().HasMaxLength(100);
        });
    }
}
