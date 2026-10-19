using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Entities.Comercio;
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
    public DbSet<AsignacionEnvioRuta> AsignacionesEnvioRuta => Set<AsignacionEnvioRuta>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IntentoEntrega> IntentosEntrega => Set<IntentoEntrega>();
    public DbSet<PruebaDeEntrega> PruebasDeEntrega => Set<PruebaDeEntrega>();
    public DbSet<ReglaOperativa> ReglasOperativas => Set<ReglaOperativa>();
    public DbSet<MotivoNoEntrega> MotivosNoEntrega => Set<MotivoNoEntrega>();
    public DbSet<EventoEnvio> EventosEnvio => Set<EventoEnvio>();
    public DbSet<Operador> Operadores => Set<Operador>();
    public DbSet<Comercio> Comercios => Set<Comercio>();
    public DbSet<ComercioOperador> ComerciosOperadores => Set<ComercioOperador>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<Repartidor> Repartidores => Set<Repartidor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Envio>(builder =>
        {
            builder.ToTable("Envios");
            builder.HasKey(e => e.Id);
            builder.Ignore(e => e.ComercioId);
            builder.Ignore(e => e.FechaCreacion);
            builder.Ignore(e => e.Estado);
            builder.Ignore(e => e.Discrepancias);
            builder.Ignore(e => e.CantidadIntentos);
            builder.Property(e => e.ComercioOperadorId).IsRequired();
            builder.Property(e => e.DireccionDestino).IsRequired().HasMaxLength(300);
            builder.Property(e => e.NombreDestinatario).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FechaAlta).IsRequired();
            builder.Property(e => e.EstadoActual).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.CodigoSeguimientoPublico).IsRequired().HasMaxLength(100);
            builder.Navigation(e => e.Bultos).AutoInclude();
            builder.Navigation(e => e.Eventos).AutoInclude();
            builder.Navigation(e => e.IntentosEntrega).AutoInclude();
        });

        modelBuilder.Entity<Bulto>(builder =>
        {
            builder.ToTable("Bultos");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Identificacion).IsRequired().HasMaxLength(100);
            builder.Property(b => b.EstadoBulto).IsRequired().HasMaxLength(50);
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

        modelBuilder.Entity<AsignacionEnvioRuta>(builder =>
        {
            builder.ToTable("AsignacionesEnvioRuta");
            builder.HasKey(a => a.Id);
        });

        modelBuilder.Entity<Operador>(builder =>
        {
            builder.ToTable("Operadores");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.RazonSocial).IsRequired().HasMaxLength(200);
            builder.Property(o => o.NombreComercial).IsRequired().HasMaxLength(200);
            builder.Property(o => o.Estado).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<Comercio>(builder =>
        {
            builder.ToTable("Comercios");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.RazonSocial).IsRequired().HasMaxLength(200);
            builder.Property(c => c.Documento).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Email).IsRequired().HasMaxLength(200);
            builder.Property(c => c.Telefono).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<ComercioOperador>(builder =>
        {
            builder.ToTable("ComerciosOperadores");
            builder.HasKey(co => co.Id);
            builder.Property(co => co.ClaveApi).IsRequired().HasMaxLength(200);
            builder.Property(co => co.Estado).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<Vehiculo>(builder =>
        {
            builder.ToTable("Vehiculos");
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Patente).IsRequired().HasMaxLength(20);
            builder.Property(v => v.Tipo).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<Repartidor>(builder =>
        {
            builder.ToTable("Repartidores");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Documento).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Telefono).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Estado).IsRequired().HasMaxLength(50);
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
