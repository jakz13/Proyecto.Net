using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Seguimiento;

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
    public DbSet<RendicionDeposito> RendicionesDeposito => Set<RendicionDeposito>();
    public DbSet<Devolucion> Devoluciones => Set<Devolucion>();
    public DbSet<Repartidor> Repartidores => Set<Repartidor>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<FranjaHoraria> FranjasHorarias => Set<FranjaHoraria>();
    public DbSet<EscaneoCargaVehiculo> EscaneosCargaVehiculo => Set<EscaneoCargaVehiculo>();
    public DbSet<Destinatario> Destinatarios => Set<Destinatario>();
    public DbSet<Direccion> Direcciones => Set<Direccion>();
    public DbSet<CuadroTarifario> CuadrosTarifarios => Set<CuadroTarifario>();
    public DbSet<ReglaTarifa> ReglasTarifa => Set<ReglaTarifa>();
    public DbSet<Operador> Operadores => Set<Operador>();
    public DbSet<Comercio> Comercios => Set<Comercio>();
    public DbSet<ComercioOperador> ComerciosOperadores => Set<ComercioOperador>();
    public DbSet<LoteImportacion> LotesImportacion => Set<LoteImportacion>();
    public DbSet<SolicitudReprogramacion> SolicitudesReprogramacion => Set<SolicitudReprogramacion>();
    public DbSet<Incidencia> Incidencias => Set<Incidencia>();
    public DbSet<NotaIncidencia> NotasIncidencia => Set<NotaIncidencia>();
    public DbSet<ZonaCobertura> ZonasCobertura => Set<ZonaCobertura>();
    public DbSet<IdentidadVisual> IdentidadesVisuales => Set<IdentidadVisual>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfiles => Set<Perfil>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<MensajeFallidoCola> MensajesFallidos => Set<MensajeFallidoCola>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Envio>(builder =>
        {
            builder.ToTable("Envios");
            builder.HasKey(e => e.Id);
            // Propiedades de compatibilidad calculadas: no se persisten
            builder.Ignore(e => e.ComercioId);
            builder.Ignore(e => e.FechaCreacion);
            builder.Ignore(e => e.Estado);
            builder.Ignore(e => e.CantidadIntentos);
            builder.Ignore(e => e.PesoTotal);
            builder.Ignore(e => e.VolumenTotal);
            builder.Property(e => e.OperadorId).IsRequired();
            builder.HasIndex(e => e.OperadorId);
            builder.Property(e => e.SelloModificacion).IsConcurrencyToken();
            builder.Property(e => e.ComercioOperadorId).IsRequired();
            builder.Property(e => e.DireccionDestino).IsRequired().HasMaxLength(300);
            builder.Property(e => e.NombreDestinatario).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FechaAlta).IsRequired();
            builder.Property(e => e.EstadoActual).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.CodigoSeguimientoPublico).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.CodigoSeguimientoPublico).IsUnique();
            builder.Property(e => e.ModalidadServicio).IsRequired().HasMaxLength(50);
            builder.Property(e => e.TarifaCalculada).HasPrecision(18, 2);
            builder.Property(e => e.ReferenciaExterna).HasMaxLength(100);
            // Una misma referencia del comercio no puede dar de alta dos envíos: garantiza la idempotencia de las altas masivas y por API
            builder.HasIndex(e => new { e.ComercioOperadorId, e.ReferenciaExterna }).IsUnique().HasFilter("\"ReferenciaExterna\" IS NOT NULL");
            builder.Navigation(e => e.Bultos).AutoInclude();
            builder.Navigation(e => e.Eventos).AutoInclude();
            builder.Navigation(e => e.IntentosEntrega).AutoInclude();
        });

        modelBuilder.Entity<Bulto>(builder =>
        {
            builder.ToTable("Bultos");
            builder.HasKey(b => b.Id);
            builder.Ignore(b => b.Volumen);
            builder.Ignore(b => b.EstaClasificado);
            builder.Property(b => b.Identificacion).IsRequired().HasMaxLength(100);
            builder.Property(b => b.EstadoBulto).IsRequired().HasMaxLength(50);
            builder.Property(b => b.Prioridad).HasConversion<string>().HasMaxLength(20);
            builder.Property(b => b.TipoClasificacion).HasMaxLength(50);
        });

        modelBuilder.Entity<HojaDeRuta>(builder =>
        {
            builder.ToTable("HojasDeRuta");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Estado).IsRequired().HasMaxLength(50);
            builder.Property(h => h.SelloModificacion).IsConcurrencyToken();
            builder.HasIndex(h => new { h.OperadorId, h.Fecha });
            builder.Navigation(h => h.Paradas).AutoInclude();
            builder.Navigation(h => h.Asignaciones).AutoInclude();
            builder.HasMany(h => h.EscaneosCarga).WithOne().HasForeignKey(e => e.HojaDeRutaId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(h => h.EscaneosCarga).AutoInclude();
        });

        modelBuilder.Entity<Parada>(builder =>
        {
            builder.ToTable("Paradas");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Estado).IsRequired().HasMaxLength(50);
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
            builder.Property(c => c.NombreFantasia).IsRequired().HasMaxLength(200).HasDefaultValue(string.Empty);
            builder.Property(c => c.Direccion).IsRequired().HasMaxLength(300).HasDefaultValue(string.Empty);
            builder.Property(c => c.SelloModificacion).IsConcurrencyToken();
        });

        modelBuilder.Entity<ComercioOperador>(builder =>
        {
            builder.ToTable("ComerciosOperadores");
            builder.HasKey(co => co.Id);
            builder.Property(co => co.ClaveApi).IsRequired().HasMaxLength(200);
            builder.Property(co => co.Estado).IsRequired().HasMaxLength(50);
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

        modelBuilder.Entity<Destinatario>(builder =>
        {
            builder.ToTable("Destinatarios");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Nombre).IsRequired().HasMaxLength(200);
            builder.Property(d => d.Documento).HasMaxLength(50);
            builder.Property(d => d.Email).HasMaxLength(200);
            builder.Property(d => d.Telefono).HasMaxLength(50);
        });

        modelBuilder.Entity<Direccion>(builder =>
        {
            builder.ToTable("Direcciones");
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Calle).IsRequired().HasMaxLength(200);
            builder.Property(d => d.Numero).IsRequired().HasMaxLength(30);
            builder.Property(d => d.Ciudad).IsRequired().HasMaxLength(100);
            builder.Property(d => d.Referencia).HasMaxLength(300);
        });

        modelBuilder.Entity<CuadroTarifario>(builder =>
        {
            builder.ToTable("CuadrosTarifarios");
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => new { c.OperadorId, c.Version }).IsUnique();
            builder.HasMany(c => c.ReglasTarifa).WithOne().HasForeignKey(r => r.CuadroTarifarioId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(c => c.ReglasTarifa).AutoInclude();
        });

        modelBuilder.Entity<ReglaTarifa>(builder =>
        {
            builder.ToTable("ReglasTarifa");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.ModalidadServicio).IsRequired().HasMaxLength(50);
            builder.Property(r => r.PesoDesde).HasPrecision(18, 3);
            builder.Property(r => r.PesoHasta).HasPrecision(18, 3);
            builder.Property(r => r.VolumenDesde).HasPrecision(18, 3);
            builder.Property(r => r.VolumenHasta).HasPrecision(18, 3);
            builder.Property(r => r.PrecioBase).HasPrecision(18, 2);
            builder.Property(r => r.Recargo).HasPrecision(18, 2);
            builder.Property(r => r.Bonificacion).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Devolucion>(builder =>
        {
            builder.ToTable("Devoluciones");
            builder.HasKey(d => d.Id);
            // Un envío tiene una sola devolución
            builder.HasIndex(d => d.EnvioId).IsUnique();
            builder.Property(d => d.Estado).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<RendicionDeposito>(builder =>
        {
            builder.ToTable("RendicionesDeposito");
            builder.HasKey(r => r.Id);
            // Una hoja de ruta se rinde una sola vez
            builder.HasIndex(r => r.HojaDeRutaId).IsUnique();
            builder.Property(r => r.Observaciones).HasMaxLength(500);
        });

        modelBuilder.Entity<EscaneoCargaVehiculo>(builder =>
        {
            builder.ToTable("EscaneosCargaVehiculo");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Resultado).IsRequired().HasMaxLength(50);
            builder.HasIndex(e => new { e.HojaDeRutaId, e.BultoId }).IsUnique();
        });

        modelBuilder.Entity<AsignacionEnvioRuta>(builder =>
        {
            builder.ToTable("AsignacionesEnvioRuta");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.SelloModificacion).IsConcurrencyToken();
            // Un envío no puede tener dos asignaciones vigentes a la vez: garantiza que dos despachadores no lo tomen simultáneamente
            builder.HasIndex(a => a.EnvioId).IsUnique().HasFilter("\"Vigente\" = true");
        });

        modelBuilder.Entity<Repartidor>(builder =>
        {
            builder.ToTable("Repartidores");
            builder.HasKey(r => r.Id);
            builder.HasIndex(r => r.OperadorId);
            builder.Property(r => r.Documento).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Telefono).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Estado).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<Vehiculo>(builder =>
        {
            builder.ToTable("Vehiculos");
            builder.HasKey(v => v.Id);
            builder.HasIndex(v => new { v.OperadorId, v.Patente }).IsUnique();
            builder.Property(v => v.Patente).IsRequired().HasMaxLength(20);
            builder.Property(v => v.Tipo).IsRequired().HasMaxLength(50);
            builder.Property(v => v.CapacidadPeso).HasPrecision(18, 3);
            builder.Property(v => v.CapacidadVolumen).HasPrecision(18, 3);
        });

        modelBuilder.Entity<FranjaHoraria>(builder =>
        {
            builder.ToTable("FranjasHorarias");
            builder.HasKey(f => f.Id);
            builder.HasIndex(f => f.ZonaCoberturaId);
        });

        modelBuilder.Entity<SolicitudReprogramacion>(builder =>
        {
            builder.ToTable("SolicitudesReprogramacion");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.FranjaHorariaSolicitada).IsRequired().HasMaxLength(100);
            builder.Property(r => r.Estado).IsRequired().HasMaxLength(30);
            builder.Property(r => r.OrigenIp).HasMaxLength(64);
            builder.Property(r => r.MotivoRechazo).HasMaxLength(300);
            builder.HasIndex(r => r.EnvioId);
        });

        modelBuilder.Entity<LoteImportacion>(builder =>
        {
            builder.ToTable("LotesImportacion");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.ClaveIdempotencia).IsRequired().HasMaxLength(200);
            builder.HasIndex(l => new { l.ComercioOperadorId, l.ClaveIdempotencia }).IsUnique();
        });

        modelBuilder.Entity<Incidencia>(builder =>
        {
            builder.ToTable("Incidencias");
            builder.HasKey(i => i.Id);
            builder.Ignore(i => i.EstaCerrada);
            builder.Property(i => i.Tipo).IsRequired().HasMaxLength(50);
            builder.Property(i => i.Severidad).IsRequired().HasMaxLength(20);
            builder.Property(i => i.Descripcion).IsRequired().HasMaxLength(1000);
            builder.Property(i => i.Estado).IsRequired().HasMaxLength(30);
            builder.Property(i => i.Resolucion).HasMaxLength(1000);
            builder.HasIndex(i => new { i.OperadorId, i.Estado });
            builder.HasMany(i => i.Notas).WithOne().HasForeignKey(n => n.IncidenciaId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(i => i.Notas).AutoInclude();
        });

        modelBuilder.Entity<NotaIncidencia>(builder =>
        {
            builder.ToTable("NotasIncidencia");
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Comentario).IsRequired().HasMaxLength(1000);
        });

        modelBuilder.Entity<ZonaCobertura>(builder =>
        {
            builder.ToTable("ZonasCobertura");
            builder.HasKey(z => z.Id);
            builder.HasIndex(z => z.OperadorId);
            builder.Property(z => z.Nombre).IsRequired().HasMaxLength(150);
            builder.Property(z => z.PoligonoGeografico).IsRequired();
            builder.Property(z => z.ColorHex).IsRequired().HasMaxLength(20);
            builder.HasMany(z => z.FranjasHorarias).WithOne().HasForeignKey(f => f.ZonaCoberturaId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(z => z.FranjasHorarias).AutoInclude();
        });

        modelBuilder.Entity<IdentidadVisual>(builder =>
        {
            builder.ToTable("IdentidadesVisuales");
            builder.HasKey(i => i.Id);
            // Cada operador tiene una única identidad visual
            builder.HasIndex(i => i.OperadorId).IsUnique();
            builder.Property(i => i.ColorPrimario).IsRequired().HasMaxLength(20);
            builder.Property(i => i.ColorSecundario).IsRequired().HasMaxLength(20);
            builder.Property(i => i.EmailContacto).IsRequired().HasMaxLength(200);
            builder.Property(i => i.TelefonoContacto).IsRequired().HasMaxLength(50);
            builder.Property(i => i.LogoUrl).HasMaxLength(500);
            builder.Property(i => i.SitioWeb).HasMaxLength(300);
            builder.Property(i => i.MensajeComunicaciones).HasMaxLength(500);
        });

        modelBuilder.Entity<Usuario>(builder =>
        {
            builder.ToTable("Usuarios");
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.Nombre).IsRequired().HasMaxLength(150);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
            builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Perfil>(builder =>
        {
            builder.ToTable("Perfiles");
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.Nombre).IsUnique();
            builder.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Descripcion).IsRequired().HasMaxLength(300);
            builder.HasMany(p => p.Permisos).WithOne().HasForeignKey(pp => pp.PerfilId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(p => p.Permisos).AutoInclude();
        });

        modelBuilder.Entity<Permiso>(builder =>
        {
            builder.ToTable("Permisos");
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.Codigo).IsUnique();
            builder.Property(p => p.Codigo).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Descripcion).IsRequired().HasMaxLength(300);
        });

        modelBuilder.Entity<PerfilPermiso>(builder =>
        {
            builder.ToTable("PerfilesPermisos");
            builder.HasKey(pp => new { pp.PerfilId, pp.PermisoId });
        });

        modelBuilder.Entity<MensajeFallidoCola>(builder =>
        {
            builder.ToTable("MensajesFallidosCola");
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.Estado);
            builder.Property(m => m.TipoMensaje).IsRequired().HasMaxLength(100);
            builder.Property(m => m.ColaOrigen).IsRequired().HasMaxLength(100);
            builder.Property(m => m.Estado).IsRequired().HasMaxLength(30);
        });

        // Los identificadores se generan en el dominio: EF no debe interpretarlos como valores generados por la base
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clave = entityType.FindPrimaryKey();
            if (clave is { Properties.Count: 1 } && clave.Properties[0].ClrType == typeof(Guid))
                clave.Properties[0].ValueGenerated = ValueGenerated.Never;
        }
    }
}
