using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using UltimaMilla.Application.Ports;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Entities.Comercio;
using UltimaMilla.Domain.Entities.Ejecucion;
using UltimaMilla.Domain.Entities.Identidad;
using UltimaMilla.Domain.Entities.Envios;
using UltimaMilla.Domain.Entities.Incidencias;
using UltimaMilla.Domain.Entities.Mensajeria;
using UltimaMilla.Domain.Entities.Operador;
using UltimaMilla.Domain.Entities.Planificacion;
using UltimaMilla.Domain.Entities.Retiros;
using UltimaMilla.Domain.Entities.Seguimiento;

namespace UltimaMilla.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentTenant? _currentTenant;
    private readonly ITableroRealtimeNotifier? _tiempoReal;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentTenant? currentTenant = null,
        ITableroRealtimeNotifier? tiempoReal = null) : base(options)
    {
        _currentTenant = currentTenant;
        _tiempoReal = tiempoReal;
    }

    /// <summary>
    /// Operador al que se acotan las consultas (ADR-002). Es <c>null</c> cuando no hay un inquilino autenticado
    /// (Worker, semillas, migraciones), caso en que el filtro global no restringe nada.
    /// </summary>
    /// <summary>
    /// Operador al que se acotan las consultas (ADR-002). Es <c>Guid.Empty</c> cuando no hay un inquilino autenticado
    /// válido, caso en que el filtro global actúa en modo fail-closed (no devuelve registros).
    /// </summary>
    private Guid OperadorFiltro => _currentTenant is { IsAuthenticated: true } ? _currentTenant.OperadorId : Guid.Empty;

    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<Retiro> Retiros => Set<Retiro>();
    public DbSet<BanderaFuncionalidad> BanderasFuncionalidad => Set<BanderaFuncionalidad>();
    public DbSet<HistorialBandera> HistorialBanderas => Set<HistorialBandera>();
    public DbSet<BultoRetiro> BultosRetiro => Set<BultoRetiro>();
    public DbSet<EventoRetiro> EventosRetiro => Set<EventoRetiro>();
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
    public DbSet<PosicionVehiculo> PosicionesVehiculo => Set<PosicionVehiculo>();
    public DbSet<ConflictoSincronizacion> ConflictosSincronizacion => Set<ConflictoSincronizacion>();
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();
    public DbSet<CuentaCorriente> CuentasCorrientes => Set<CuentaCorriente>();
    public DbSet<MovimientoCuentaCorriente> MovimientosCuentaCorriente => Set<MovimientoCuentaCorriente>();
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
    public DbSet<MensajeFallidoCola> MensajesFallidos => Set<MensajeFallidoCola>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Perfil> Perfiles => Set<Perfil>();
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<PerfilPermiso> PerfilPermisos => Set<PerfilPermiso>();
    public DbSet<ClaveIdempotenciaConsumidor> ClavesIdempotenciaConsumidor => Set<ClaveIdempotenciaConsumidor>();
    public DbSet<SuscripcionAviso> SuscripcionesAviso => Set<SuscripcionAviso>();
    public DbSet<AvisoComercio> AvisosComercio => Set<AvisoComercio>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<IndicadorCumplimiento> IndicadoresCumplimiento => Set<IndicadorCumplimiento>();
    public DbSet<CierreDiarioOperacion> CierresDiariosOperacion => Set<CierreDiarioOperacion>();

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
            builder.Ignore(b => b.EstaEntregado);
            builder.Ignore(b => b.EstaDisponibleParaRuta);
            builder.Property(b => b.MotivoNoEntregaCodigo).HasMaxLength(50);
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
            builder.Ignore(p => p.EsRetiro);
            builder.Property(p => p.Estado).IsRequired().HasMaxLength(50);
            // Las hojas de ruta son mixtas: una parada entrega un envío o retira un retiro
            builder.Property(p => p.Tipo).IsRequired().HasMaxLength(20).HasDefaultValue(Parada.TipoEntrega);
            builder.HasIndex(p => p.RetiroId);
        });

        // Banderas de funcionalidad por operador: las escribe el administrador de la plataforma sobre cualquier operador
        modelBuilder.Entity<BanderaFuncionalidad>(builder =>
        {
            builder.ToTable("BanderasFuncionalidad");
            builder.HasKey(b => b.Id);
            builder.Property(b => b.Clave).IsRequired().HasMaxLength(50);
            builder.Property(b => b.ModificadaPorNombre).HasMaxLength(200);
            builder.HasIndex(b => new { b.OperadorId, b.Clave }).IsUnique();
        });

        modelBuilder.Entity<HistorialBandera>(builder =>
        {
            builder.ToTable("HistorialBanderas");
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Clave).IsRequired().HasMaxLength(50);
            builder.Property(h => h.UsuarioNombre).HasMaxLength(200);
            builder.HasIndex(h => new { h.OperadorId, h.Fecha });
        });

        // Retiro en domicilio (cambio de requerimientos 2.2)
        modelBuilder.Entity<Retiro>(builder =>
        {
            builder.ToTable("Retiros");
            builder.HasKey(r => r.Id);
            builder.Ignore(r => r.PesoTotal);
            builder.Ignore(r => r.VolumenTotal);
            builder.Property(r => r.OperadorId).IsRequired();
            builder.Property(r => r.ComercioOperadorId).IsRequired();
            builder.Property(r => r.SelloModificacion).IsConcurrencyToken();
            builder.Property(r => r.CodigoSeguimientoPublico).IsRequired().HasMaxLength(100);
            builder.HasIndex(r => r.CodigoSeguimientoPublico).IsUnique();
            builder.Property(r => r.ReferenciaExterna).HasMaxLength(100);
            // Una misma referencia del comercio no puede solicitar dos retiros: hace idempotente la solicitud por API
            builder.HasIndex(r => new { r.ComercioOperadorId, r.ReferenciaExterna }).IsUnique().HasFilter("\"ReferenciaExterna\" IS NOT NULL");
            builder.HasIndex(r => new { r.OperadorId, r.EstadoActual });
            builder.Property(r => r.NombreContacto).IsRequired().HasMaxLength(200);
            builder.Property(r => r.TelefonoContacto).HasMaxLength(50);
            builder.Property(r => r.EmailContacto).HasMaxLength(200);
            builder.Property(r => r.DireccionRetiro).IsRequired().HasMaxLength(300);
            builder.Property(r => r.Motivo).IsRequired().HasMaxLength(300);
            builder.Property(r => r.EstadoActual).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(r => r.TarifaCalculada).HasPrecision(18, 2);
            builder.Property(r => r.NombreEntrega).HasMaxLength(200);
            builder.Property(r => r.DocumentoEntrega).HasMaxLength(50);
            builder.Property(r => r.MotivoNoRetiroCodigo).HasMaxLength(50);
            builder.HasMany(r => r.Bultos).WithOne().HasForeignKey(b => b.RetiroId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(r => r.Eventos).WithOne().HasForeignKey(e => e.RetiroId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Bultos).AutoInclude();
            builder.Navigation(r => r.Eventos).AutoInclude();
        });

        modelBuilder.Entity<BultoRetiro>(builder =>
        {
            builder.ToTable("BultosRetiro");
            builder.HasKey(b => b.Id);
            builder.Ignore(b => b.Volumen);
            builder.Property(b => b.Identificacion).IsRequired().HasMaxLength(100);
            builder.Property(b => b.Descripcion).IsRequired().HasMaxLength(200);
            builder.HasIndex(b => b.Identificacion);
        });

        modelBuilder.Entity<EventoRetiro>(builder =>
        {
            builder.ToTable("EventosRetiro");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.TipoEvento).IsRequired().HasMaxLength(100);
            builder.Property(e => e.EstadoAnterior).HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.EstadoNuevo).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.Origen).IsRequired().HasMaxLength(50);
            builder.HasIndex(e => e.RetiroId);
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
            builder.Property(p => p.CodigosBultosEntregados)
                .HasMaxLength(2000)
                .HasConversion(
                    v => string.Join(',', v),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    new ValueComparer<IReadOnlyList<string>>(
                        (a, b) => a!.SequenceEqual(b!),
                        a => a.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
                        a => a.ToList()));
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Tipo).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<ReglaOperativa>(builder =>
        {
            builder.ToTable("ReglasOperativas");
            builder.Property(r => r.AdmiteEntregaParcial).HasDefaultValue(true);
            builder.Property(r => r.PoliticaEntregaParcial).IsRequired().HasMaxLength(50).HasDefaultValue(ReglaOperativa.PoliticaNuevaVisitaSinRecargo);
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

        modelBuilder.Entity<Liquidacion>(builder =>
        {
            builder.ToTable("Liquidaciones");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Estado).IsRequired().HasMaxLength(30);
            builder.Property(l => l.MontoTotal).HasPrecision(18, 2);
            builder.HasIndex(l => new { l.ComercioOperadorId, l.PeriodoDesde });
        });

        modelBuilder.Entity<CuentaCorriente>(builder =>
        {
            builder.ToTable("CuentasCorrientes");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Moneda).IsRequired().HasMaxLength(3);
            builder.Property(c => c.Saldo).HasPrecision(18, 2);
            builder.Property(c => c.SelloModificacion).IsConcurrencyToken();
            // Una sola cuenta por vínculo comercio-operador
            builder.HasIndex(c => c.ComercioOperadorId).IsUnique();
            builder.HasMany(c => c.Movimientos).WithOne().HasForeignKey(m => m.CuentaCorrienteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MovimientoCuentaCorriente>(builder =>
        {
            builder.ToTable("MovimientosCuentaCorriente");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Concepto).IsRequired().HasMaxLength(200);
            builder.Property(m => m.Monto).HasPrecision(18, 2);
            builder.HasIndex(m => new { m.CuentaCorrienteId, m.Fecha });
            // Un envío no puede cargarse en dos liquidaciones: protege el cierre del período ante ejecuciones simultáneas
            builder.HasIndex(m => m.EnvioId).IsUnique().HasFilter("\"EnvioId\" IS NOT NULL AND \"LiquidacionId\" IS NOT NULL");
        });

        modelBuilder.Entity<PosicionVehiculo>(builder =>
        {
            builder.ToTable("PosicionesVehiculo");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Origen).IsRequired().HasMaxLength(30);
            // La consulta del tablero pide la última posición de cada vehículo del operador
            builder.HasIndex(p => new { p.OperadorId, p.VehiculoId, p.Timestamp });
        });

        modelBuilder.Entity<ConflictoSincronizacion>(builder =>
        {
            builder.ToTable("ConflictosSincronizacion");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.EntidadAfectada).IsRequired().HasMaxLength(50);
            builder.Property(c => c.EstadoServidor).HasMaxLength(50);
            builder.Property(c => c.EstadoDispositivo).HasMaxLength(50);
            builder.Property(c => c.PoliticaAplicada).HasMaxLength(100);
            builder.Property(c => c.Resolucion).IsRequired().HasMaxLength(500);
            builder.HasIndex(c => new { c.EntidadAfectada, c.EntidadId });
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
            builder.Property(i => i.LogoUrl);
            builder.Property(i => i.SitioWeb).HasMaxLength(300);
            builder.Property(i => i.MensajeComunicaciones).HasMaxLength(500);
        });

        modelBuilder.Entity<Usuario>(builder =>
        {
            builder.ToTable("Usuarios");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Nombre).IsRequired().HasMaxLength(150);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            builder.Property(u => u.Activo).IsRequired();
            builder.Property(u => u.FechaAlta).IsRequired();
            builder.Property(u => u.IntentosFallidos).IsRequired();
            builder.Property(u => u.BloqueadoHasta);
            builder.HasIndex(u => u.OperadorId);
            builder.HasIndex(u => u.ComercioId);
            builder.HasIndex(u => u.PerfilId);
        });

        modelBuilder.Entity<Perfil>(builder =>
        {
            builder.ToTable("Perfiles");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
            builder.HasIndex(p => p.Nombre).IsUnique();
            builder.Property(p => p.Descripcion).HasMaxLength(300);
            builder.HasMany(p => p.Permisos).WithOne().HasForeignKey(pp => pp.PerfilId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(p => p.Permisos).AutoInclude();
        });

        modelBuilder.Entity<Permiso>(builder =>
        {
            builder.ToTable("Permisos");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Codigo).IsRequired().HasMaxLength(100);
            builder.HasIndex(p => p.Codigo).IsUnique();
            builder.Property(p => p.Descripcion).HasMaxLength(300);
        });

        modelBuilder.Entity<PerfilPermiso>(builder =>
        {
            builder.ToTable("PerfilPermisos");
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

        modelBuilder.Entity<ClaveIdempotenciaConsumidor>(builder =>
        {
            builder.ToTable("ClavesIdempotenciaConsumidor");
            builder.HasKey(c => c.Id);
            // La unicidad es lo que impide que dos instancias del worker apliquen los efectos del mismo mensaje
            builder.Property(c => c.MensajeId).IsRequired().HasMaxLength(100);
            builder.HasIndex(c => c.MensajeId).IsUnique();
            builder.Property(c => c.Resultado).HasMaxLength(500);
        });

        modelBuilder.Entity<SuscripcionAviso>(builder =>
        {
            builder.ToTable("SuscripcionesAviso");
            builder.Property(s => s.VersionContrato).IsRequired().HasMaxLength(10).HasDefaultValue(SuscripcionAviso.ContratoV1);
            builder.HasKey(s => s.Id);
            builder.Property(s => s.TipoEvento).IsRequired().HasMaxLength(100);
            builder.Property(s => s.UrlWebhook).IsRequired().HasMaxLength(500);
            builder.Property(s => s.ClaveFirma).IsRequired().HasMaxLength(200);
            builder.HasIndex(s => new { s.ComercioOperadorId, s.Activa });
        });

        modelBuilder.Entity<AvisoComercio>(builder =>
        {
            builder.ToTable("AvisosComercio");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Payload).IsRequired();
            builder.Property(a => a.FirmaEnviada).IsRequired().HasMaxLength(200);
            builder.Property(a => a.Estado).IsRequired().HasMaxLength(30);
            builder.Property(a => a.UltimaRespuestaHttp).HasMaxLength(1000);
            builder.HasIndex(a => new { a.Estado, a.ProximoReintento });
            builder.HasIndex(a => a.SuscripcionAvisoId);
            builder.HasIndex(a => a.EnvioId);
        });

        modelBuilder.Entity<Notificacion>(builder =>
        {
            builder.ToTable("Notificaciones");
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Canal).IsRequired().HasMaxLength(30);
            builder.Property(n => n.TipoEvento).IsRequired().HasMaxLength(100);
            builder.Property(n => n.EstadoEnvio).HasConversion<string>().HasMaxLength(50);
            builder.HasIndex(n => n.EnvioId);
        });

        modelBuilder.Entity<IndicadorCumplimiento>(builder =>
        {
            builder.ToTable("IndicadoresCumplimiento");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Periodo).IsRequired().HasMaxLength(20);
            builder.Property(i => i.PorcentajeCumplimiento).HasPrecision(5, 2);
            builder.HasIndex(i => new { i.OperadorId, i.Periodo });
        });

        modelBuilder.Entity<CierreDiarioOperacion>(builder =>
        {
            builder.ToTable("CierresDiariosOperacion");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.ResumenJson).IsRequired();
            // Un único cierre por operador y día: hace idempotente el proceso aunque corra en más de una instancia
            builder.HasIndex(c => new { c.OperadorId, c.Fecha }).IsUnique();
        });

        // Aislamiento por fila (ADR-002): toda entidad de alcance de operador queda acotada al operador del inquilino actual
        AplicarFiltroDeOperador<ComercioOperador>(modelBuilder);
        AplicarFiltroDeOperador<Envio>(modelBuilder);
        AplicarFiltroDeOperador<Retiro>(modelBuilder);
        AplicarFiltroDeOperador<HojaDeRuta>(modelBuilder);
        AplicarFiltroDeOperador<Incidencia>(modelBuilder);
        AplicarFiltroDeOperador<IndicadorCumplimiento>(modelBuilder);
        AplicarFiltroDeOperador<CierreDiarioOperacion>(modelBuilder);
        AplicarFiltroDeOperador<CuadroTarifario>(modelBuilder);
        AplicarFiltroDeOperador<IdentidadVisual>(modelBuilder);
        AplicarFiltroDeOperador<MotivoNoEntrega>(modelBuilder);
        AplicarFiltroDeOperador<ReglaOperativa>(modelBuilder);
        AplicarFiltroDeOperador<Repartidor>(modelBuilder);
        AplicarFiltroDeOperador<Vehiculo>(modelBuilder);
        AplicarFiltroDeOperador<PosicionVehiculo>(modelBuilder);
        AplicarFiltroDeOperador<ZonaCobertura>(modelBuilder);
        AplicarFiltroDeComercioDelOperador<CuentaCorriente>(modelBuilder);
        AplicarFiltroDeComercioDelOperador<Liquidacion>(modelBuilder);
        AplicarFiltroDeComercioDelOperador<LoteImportacion>(modelBuilder);
        AplicarFiltroDeComercioDelOperador<SuscripcionAviso>(modelBuilder);
        // Los identificadores se generan en el dominio: EF no debe interpretarlos como valores generados por la base
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clave = entityType.FindPrimaryKey();
            if (clave is { Properties.Count: 1 } && clave.Properties[0].ClrType == typeof(Guid))
                clave.Properties[0].ValueGenerated = ValueGenerated.Never;
        }
    }

    /// <summary>
    /// Filtro global por operador. Se lee <c>OperadorId</c> por nombre de propiedad para no depender de la interfaz en la traducción a SQL.
    /// Los flujos sin inquilino (seguimiento público, clave de API) lo omiten de forma explícita con <c>IgnoreQueryFilters()</c>.
    /// </summary>
    private void AplicarFiltroDeOperador<T>(ModelBuilder modelBuilder) where T : class, IMustHaveTenant
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            OperadorFiltro != Guid.Empty && EF.Property<Guid>(e, "OperadorId") == OperadorFiltro);
    }

    /// <summary>
    /// Filtro global para entidades de alcance de comercio-operador que no almacenan <c>OperadorId</c> directo.
    /// Acota las filas a los comercios pertenecientes al operador autenticado actual, manteniendo compatibilidad con Backoffice y Portal.
    /// </summary>
    private void AplicarFiltroDeComercioDelOperador<T>(ModelBuilder modelBuilder) where T : class, IMustHaveComercioTenant
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            OperadorFiltro != Guid.Empty
            && ComerciosOperadores.Any(co => co.Id == e.ComercioOperadorId && co.OperadorId == OperadorFiltro));
    }

    private int _alcanceEscrituraAdministrativa;

    /// <summary>
    /// Habilita de forma explícita y acotada la escritura transversal sobre entidades multitenant
    /// exclusivamente para semillas de datos o tareas administrativas autorizadas.
    /// </summary>
    public IDisposable HabilitarEscrituraAdministrativa()
    {
        _alcanceEscrituraAdministrativa++;
        return new AlcanceEscrituraAdministrativa(this);
    }

    private sealed class AlcanceEscrituraAdministrativa(AppDbContext contexto) : IDisposable
    {
        private bool _cerrado;

        public void Dispose()
        {
            if (_cerrado)
                return;

            _cerrado = true;
            contexto._alcanceEscrituraAdministrativa--;
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarAislamientoDeEscrituraAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await ValidarAislamientoDeEscrituraAsync(cancellationToken);
        var cambiosDeEstado = _tiempoReal is null ? null : CapturarCambiosDeEstadoDeEnvios();
        var cambiosDeRetiros = _tiempoReal is null ? null : CapturarCambiosDeEstadoDeRetiros();
        var cambiosDeIncidencias = _tiempoReal is null ? null : CapturarCambiosDeIncidencias();
        var guardados = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (_tiempoReal is not null && cambiosDeEstado is { Count: > 0 })
            await NotificarCambiosDeEstadoAsync(cambiosDeEstado);
        if (_tiempoReal is not null && cambiosDeRetiros is { Count: > 0 })
            await NotificarCambiosDeEstadoDeRetirosAsync(cambiosDeRetiros);
        if (_tiempoReal is not null && cambiosDeIncidencias is { Count: > 0 })
            await NotificarCambiosDeIncidenciasAsync(cambiosDeIncidencias);

        return guardados;
    }

    private sealed record CambioIncidencia(Guid OperadorId, Guid IncidenciaId, string Tipo, string Severidad, string Estado);

    /// <summary>Una incidencia nueva o que cambia de estado (abierta, en seguimiento, cerrada) se avisa al backoffice sin importar quién la origine.</summary>
    private List<CambioIncidencia> CapturarCambiosDeIncidencias()
    {
        var cambios = new List<CambioIncidencia>();
        foreach (var entrada in ChangeTracker.Entries<Incidencia>())
        {
            var huboCambio = entrada.State == EntityState.Added
                || (entrada.State == EntityState.Modified
                    && !Equals(entrada.Property(i => i.Estado).OriginalValue, entrada.Property(i => i.Estado).CurrentValue));
            if (!huboCambio)
                continue;

            var incidencia = entrada.Entity;
            cambios.Add(new CambioIncidencia(incidencia.OperadorId, incidencia.Id, incidencia.Tipo, incidencia.Severidad, incidencia.Estado));
        }

        return cambios;
    }

    private async Task NotificarCambiosDeIncidenciasAsync(List<CambioIncidencia> cambios)
    {
        foreach (var cambio in cambios)
        {
            try
            {
                await _tiempoReal!.NotificarIncidenciaCambioAsync(cambio.OperadorId, cambio.IncidenciaId, cambio.Tipo, cambio.Severidad, cambio.Estado);
            }
            catch
            {
                // Best effort: la vista se pondrá al día en el próximo refresco
            }
        }
    }

    private sealed record CambioEstadoRetiro(Guid OperadorId, Guid ComercioOperadorId, Guid RetiroId, string Codigo, string Estado);

    /// <summary>Igual que con los envíos: cualquier comando que altere el estado de un retiro (web, móvil, depósito) queda cubierto.</summary>
    private List<CambioEstadoRetiro> CapturarCambiosDeEstadoDeRetiros()
    {
        var cambios = new List<CambioEstadoRetiro>();
        foreach (var entrada in ChangeTracker.Entries<Retiro>())
        {
            var huboCambio = entrada.State == EntityState.Added
                || (entrada.State == EntityState.Modified
                    && !Equals(entrada.Property(r => r.EstadoActual).OriginalValue, entrada.Property(r => r.EstadoActual).CurrentValue));
            if (!huboCambio)
                continue;

            var retiro = entrada.Entity;
            cambios.Add(new CambioEstadoRetiro(retiro.OperadorId, retiro.ComercioOperadorId, retiro.Id, retiro.CodigoSeguimientoPublico, retiro.EstadoActual.ToString()));
        }

        return cambios;
    }

    /// <summary>El aviso es posterior al guardado y no condiciona su resultado.</summary>
    private async Task NotificarCambiosDeEstadoDeRetirosAsync(List<CambioEstadoRetiro> cambios)
    {
        foreach (var cambio in cambios)
        {
            try
            {
                await _tiempoReal!.NotificarRetiroCambioEstadoAsync(
                    cambio.OperadorId, cambio.ComercioOperadorId, cambio.RetiroId, cambio.Codigo, cambio.Estado);
            }
            catch
            {
                // Best effort: la vista se pondrá al día en el próximo refresco
            }
        }
    }

    private sealed record CambioEstadoEnvio(Guid OperadorId, Guid ComercioOperadorId, Guid EnvioId, string Codigo, string Estado);

    /// <summary>
    /// Punto único para el tiempo real de los envíos: cualquier comando que altere el estado de un envío (depósito, ruta, app móvil,
    /// incidencias) queda cubierto sin que cada caso de uso tenga que acordarse de avisar. Se captura antes de guardar porque después
    /// el seguimiento de cambios ya no distingue lo modificado.
    /// </summary>
    private List<CambioEstadoEnvio> CapturarCambiosDeEstadoDeEnvios()
    {
        var cambios = new List<CambioEstadoEnvio>();
        foreach (var entrada in ChangeTracker.Entries<Envio>())
        {
            var huboCambio = entrada.State == EntityState.Added
                || (entrada.State == EntityState.Modified
                    && !Equals(entrada.Property(e => e.EstadoActual).OriginalValue, entrada.Property(e => e.EstadoActual).CurrentValue));
            if (!huboCambio)
                continue;

            var envio = entrada.Entity;
            cambios.Add(new CambioEstadoEnvio(envio.OperadorId, envio.ComercioOperadorId, envio.Id, envio.CodigoSeguimientoPublico, envio.EstadoActual.ToString()));
        }

        return cambios;
    }

    /// <summary>El aviso es posterior al guardado y no condiciona su resultado: un fallo de transporte nunca revierte ni oculta el cambio.</summary>
    private async Task NotificarCambiosDeEstadoAsync(List<CambioEstadoEnvio> cambios)
    {
        foreach (var cambio in cambios)
        {
            try
            {
                await _tiempoReal!.NotificarEnvioCambioEstadoAsync(
                    cambio.OperadorId, cambio.ComercioOperadorId, cambio.EnvioId, cambio.Codigo, cambio.Estado);
            }
            catch
            {
                // Best effort: la vista se pondrá al día en el próximo refresco
            }
        }
    }

    /// <summary>
    /// Garantiza el aislamiento en escritura (ADR-002):
    /// 1. Exige un operador autenticado válido (<c>OperadorId != Guid.Empty</c>) o un alcance administrativo explícito
    ///    (<see cref="HabilitarEscrituraAdministrativa"/>) para escribir cualquier entidad <see cref="IMustHaveTenant"/> o <see cref="IMustHaveComercioTenant"/>.
    /// 2. Impide persistir identificadores de tenant vacíos (<c>Guid.Empty</c>).
    /// 3. Comprueba contra la base de datos que las filas modificadas o eliminadas pertenezcan realmente al propietario esperado,
    ///    bloqueando ataques por adjunción (<c>Attach</c>/<c>Update</c>/<c>Remove</c>) o transferencia de propietario.
    /// </summary>
    private async Task ValidarAislamientoDeEscrituraAsync(CancellationToken cancellationToken)
    {
        var cambiosOperador = ChangeTracker.Entries<IMustHaveTenant>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var cambiosComercio = ChangeTracker.Entries<IMustHaveComercioTenant>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (cambiosOperador.Count == 0 && cambiosComercio.Count == 0)
            return;

        var esAdministrativo = _alcanceEscrituraAdministrativa > 0;
        var operadorActual = OperadorFiltro;

        if (!esAdministrativo && operadorActual == Guid.Empty)
            throw new UnauthorizedAccessException("No se permite escribir recursos multitenant sin un operador autenticado válido o autorización administrativa explícita.");

        foreach (var entry in cambiosOperador)
        {
            if (entry.Entity.OperadorId == Guid.Empty)
                throw new UnauthorizedAccessException("No se permite persistir recursos multitenant con un OperadorId vacío.");

            if (entry.State == EntityState.Added)
            {
                if (!esAdministrativo && entry.Entity.OperadorId != operadorActual)
                    throw new UnauthorizedAccessException("No se permite insertar recursos pertenecientes a otro operador.");
            }
            else if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var operadorOriginal = entry.OriginalValues.GetValue<Guid>(nameof(IMustHaveTenant.OperadorId));
                if (operadorOriginal == Guid.Empty || entry.Entity.OperadorId != operadorOriginal)
                    throw new UnauthorizedAccessException("No se permite cambiar el operador propietario de un recurso existente.");

                if (!esAdministrativo && entry.Entity.OperadorId != operadorActual)
                    throw new UnauthorizedAccessException("No se permite modificar ni eliminar recursos pertenecientes a otro operador.");

                var valoresEnBase = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (valoresEnBase is null || valoresEnBase.GetValue<Guid>(nameof(IMustHaveTenant.OperadorId)) != entry.Entity.OperadorId)
                    throw new UnauthorizedAccessException("No se permite modificar ni eliminar recursos pertenecientes a otro operador.");

                if (entry.State == EntityState.Modified)
                    entry.Property(nameof(IMustHaveTenant.OperadorId)).IsModified = false;
            }
        }

        foreach (var entry in cambiosComercio)
        {
            if (entry.Entity.ComercioOperadorId == Guid.Empty)
                throw new UnauthorizedAccessException("No se permite persistir recursos de comercio con un ComercioOperadorId vacío.");

            if (!esAdministrativo)
            {
                var perteneceAlOperador = await ComerciosOperadores
                    .AsNoTracking()
                    .AnyAsync(co => co.Id == entry.Entity.ComercioOperadorId && co.OperadorId == operadorActual, cancellationToken);
                if (!perteneceAlOperador)
                    throw new UnauthorizedAccessException("No se permite operar sobre recursos de un comercio perteneciente a otro operador.");
            }

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var comercioOriginal = entry.OriginalValues.GetValue<Guid>(nameof(IMustHaveComercioTenant.ComercioOperadorId));
                if (comercioOriginal == Guid.Empty || entry.Entity.ComercioOperadorId != comercioOriginal)
                    throw new UnauthorizedAccessException("No se permite cambiar el comercio-operador propietario de un recurso existente.");

                var valoresEnBase = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (valoresEnBase is null || valoresEnBase.GetValue<Guid>(nameof(IMustHaveComercioTenant.ComercioOperadorId)) != entry.Entity.ComercioOperadorId)
                    throw new UnauthorizedAccessException("No se permite modificar ni eliminar recursos alterando su comercio-operador propietario.");

                if (entry.State == EntityState.Modified)
                    entry.Property(nameof(IMustHaveComercioTenant.ComercioOperadorId)).IsModified = false;
            }
        }
    }
}
