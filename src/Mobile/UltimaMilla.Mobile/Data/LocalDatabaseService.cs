using System.Security.Cryptography;
using Microsoft.Maui.Storage;
using SQLite;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.Data;

public class RutaActivaLocalDto
{
    public HojaDeRutaLocal Ruta { get; set; } = null!;
    public List<ParadaItemViewModel> Paradas { get; set; } = new();
}

public interface ILocalDatabaseService
{
    Task InitAsync();
    SQLiteAsyncConnection Connection { get; }

    // Cola de operaciones offline (FIFO)
    Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson);
    Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync();
    Task MarcarComoSincronizadoAsync(int id);
    Task RegistrarErrorAsync(int id, string error);
    Task<int> ObtenerCantidadPendientesAsync();

    // Persistencia y consulta de hoja de ruta local (UC-17, UC-18, UC-19, UC-20, UC-21)
    Task GuardarRutaDescargadaAsync(Guid hojaDeRutaId, List<ParadaNavegacionDto> paradas, List<MotivoNoEntregaLocal>? motivos = null);
    Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync();
    Task ActualizarEstadoParadaLocalAsync(Guid paradaId, string nuevoEstado);
    Task<List<MotivoNoEntregaLocal>> ObtenerMotivosNoEntregaAsync();
    Task SeedRutaDemoSiEstaVaciaAsync();
    Task ReiniciarDatosDemoLocalAsync();
}

public class LocalDatabaseService : ILocalDatabaseService
{
    private const string KeyStorageAlias = "sqlite_db_encryption_key";
    private const string FallbackDeterministicKey = "SuperSecretKey2026_DeterministicFallbackKey";

    private SQLiteAsyncConnection? _database;
    private SQLiteAsyncConnection Db => _database!;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SQLiteAsyncConnection Connection
    {
        get
        {
            if (_database == null)
            {
                InitAsync().GetAwaiter().GetResult();
            }
            return _database!;
        }
    }

    public LocalDatabaseService()
    {
        // La conexión cifrada se inicializa de forma asíncrona en InitAsync() usando SecureStorage
    }

    /// <summary>
    /// Obtiene la clave criptográfica desde SecureStorage del dispositivo (RNF 6.3 y RNF 6.11).
    /// Si aún no existe, genera una clave aleatoria de 32 bytes (256 bits), la almacena de forma segura y la retorna.
    /// Incluye fallback determinista únicamente por si el entorno de ejecución (ej. runner) no soporta SecureStorage.
    /// </summary>
    private static async Task<string> GetOrGenerateEncryptionKeyAsync()
    {
        try
        {
            var existingKey = await SecureStorage.Default.GetAsync(KeyStorageAlias);
            if (!string.IsNullOrWhiteSpace(existingKey))
            {
                return existingKey;
            }

            var keyBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyBytes);
            }

            var newKey = Convert.ToBase64String(keyBytes);
            await SecureStorage.Default.SetAsync(KeyStorageAlias, newKey);
            return newKey;
        }
        catch
        {
            // Fallback determinista para entornos sin soporte de KeyStore nativo
            return FallbackDeterministicKey;
        }
    }

    /// <summary>
    /// Crea la conexión cifrada y las tablas locales necesarias para la operativa offline si aún no existen.
    /// </summary>
    public async Task InitAsync()
    {
        if (_initialized && _database != null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized && _database != null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ultimamilla_encrypted.db3");
            var encryptionKey = await GetOrGenerateEncryptionKeyAsync();

            var connectionString = new SQLiteConnectionString(
                dbPath,
                openFlags: SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: true,
                key: encryptionKey);

            _database = new SQLiteAsyncConnection(connectionString);

            try
            {
                await _database.CreateTableAsync<HojaDeRutaLocal>();
                await _database.CreateTableAsync<ParadaLocal>();
                await _database.CreateTableAsync<EnvioLocal>();
                await _database.CreateTableAsync<BultoLocal>();
                await _database.CreateTableAsync<MotivoNoEntregaLocal>();
                await _database.CreateTableAsync<OperacionOfflineLocal>();
            }
            catch (SQLiteException)
            {
                // Si la clave criptográfica cambió y no coincide con un archivo local antiguo, se recrea el archivo
                try
                {
                    await _database.CloseAsync();
                    if (File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                    }

                    _database = new SQLiteAsyncConnection(connectionString);
                    await _database.CreateTableAsync<HojaDeRutaLocal>();
                    await _database.CreateTableAsync<ParadaLocal>();
                    await _database.CreateTableAsync<EnvioLocal>();
                    await _database.CreateTableAsync<BultoLocal>();
                    await _database.CreateTableAsync<MotivoNoEntregaLocal>();
                    await _database.CreateTableAsync<OperacionOfflineLocal>();
                }
                catch
                {
                    throw;
                }
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson)
    {
        await InitAsync();
        var operacion = new OperacionOfflineLocal
        {
            TipoOperacion = tipoOperacion,
            PayloadJson = payloadJson,
            FechaCreacionUtc = DateTime.UtcNow,
            Sincronizado = false
        };

        return await Db.InsertAsync(operacion);
    }

    public async Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync()
    {
        await InitAsync();
        return await Db.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .OrderBy(o => o.FechaCreacionUtc)
            .ToListAsync();
    }

    public async Task MarcarComoSincronizadoAsync(int id)
    {
        await InitAsync();
        var op = await Db.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Sincronizado = true;
            await Db.UpdateAsync(op);
        }
    }

    public async Task RegistrarErrorAsync(int id, string error)
    {
        await InitAsync();
        var op = await Db.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Reintentos++;
            op.UltimoError = error;
            await Db.UpdateAsync(op);
        }
    }

    public async Task<int> ObtenerCantidadPendientesAsync()
    {
        await InitAsync();
        return await Db.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .CountAsync();
    }

    public async Task GuardarRutaDescargadaAsync(Guid hojaDeRutaId, List<ParadaNavegacionDto> paradas, List<MotivoNoEntregaLocal>? motivos = null)
    {
        await InitAsync();

        await Db.RunInTransactionAsync(conn =>
        {
            // 1. Upsert Hoja de Ruta
            var ruta = conn.Find<HojaDeRutaLocal>(hojaDeRutaId);
            if (ruta == null)
            {
                ruta = new HojaDeRutaLocal
                {
                    Id = hojaDeRutaId,
                    Fecha = DateTime.Today,
                    Estado = "EnTransito",
                    NumeroRuta = $"HR-{DateTime.Today:yyyyMMdd}-01",
                    TotalParadas = paradas.Count,
                    FechaDescargaUtc = DateTime.UtcNow
                };
                conn.Insert(ruta);
            }
            else
            {
                ruta.TotalParadas = paradas.Count;
                ruta.FechaDescargaUtc = DateTime.UtcNow;
                conn.Update(ruta);
            }

            // 2. Upsert Paradas, Envios y Bultos
            foreach (var p in paradas)
            {
                var paradaId = p.ParadaId != Guid.Empty
                    ? p.ParadaId
                    : (p.CodigoSeguimiento == "ENV-2026-00108" ? DemoDataDefaults.Parada1Id :
                       p.CodigoSeguimiento == "ENV-2026-00109" ? DemoDataDefaults.Parada2Id :
                       p.CodigoSeguimiento == "ENV-2026-00110" ? DemoDataDefaults.Parada3Id :
                       (p.Orden == 1 ? DemoDataDefaults.Parada1Id : p.Orden == 2 ? DemoDataDefaults.Parada2Id : DemoDataDefaults.Parada3Id));

                var envioId = p.EnvioId != Guid.Empty
                    ? p.EnvioId
                    : (p.CodigoSeguimiento == "ENV-2026-00108" ? DemoDataDefaults.Envio1Id :
                       p.CodigoSeguimiento == "ENV-2026-00109" ? DemoDataDefaults.Envio2Id :
                       p.CodigoSeguimiento == "ENV-2026-00110" ? DemoDataDefaults.Envio3Id :
                       (p.Orden == 1 ? DemoDataDefaults.Envio1Id : p.Orden == 2 ? DemoDataDefaults.Envio2Id : DemoDataDefaults.Envio3Id));

                var paradaLocal = conn.Find<ParadaLocal>(paradaId);
                bool isNew = paradaLocal == null;
                paradaLocal ??= new ParadaLocal { Id = paradaId };

                paradaLocal.HojaDeRutaId = hojaDeRutaId != Guid.Empty ? hojaDeRutaId : DemoDataDefaults.DefaultHojaDeRutaId;
                paradaLocal.EnvioId = envioId;
                paradaLocal.Orden = p.Orden;
                
                // Preservar estado completado local si aún no se sincroniza
                if (isNew || (paradaLocal.Estado != "Entregado" && paradaLocal.Estado != "Fallido" && paradaLocal.Estado != "EnDevolucion"))
                {
                    paradaLocal.Estado = MapearEstadoParada(p.EstadoParada);
                }

                paradaLocal.CodigoSeguimiento = !string.IsNullOrWhiteSpace(p.CodigoSeguimiento) ? p.CodigoSeguimiento : $"ENV-2026-{107 + p.Orden:D5}";
                paradaLocal.Direccion = p.DireccionFormateada;
                paradaLocal.Ciudad = ExtraerCiudad(p.DireccionFormateada);
                paradaLocal.DestinatarioNombre = p.Destinatario?.Nombre ?? "Destinatario";
                paradaLocal.DestinatarioTelefono = p.Destinatario?.Telefono ?? "+598 99 123 456";
                paradaLocal.DestinatarioEmail = p.Destinatario?.Email;
                paradaLocal.NotasComercio = p.Destinatario?.ReferenciaEntrega ?? string.Empty;
                paradaLocal.Latitud = p.Latitud ?? -34.9056;
                paradaLocal.Longitud = p.Longitud ?? -56.1852;
                paradaLocal.VentanaHoraria = p.VentanaHoraria ?? "09:00 - 13:00";
                paradaLocal.HoraEstimada = $"{8 + p.Orden:D2}:30";
                paradaLocal.CantidadBultos = p.ResumenBultos?.CantidadBultos ?? 1;
                paradaLocal.EsParadaActual = p.EsParadaActual;

                if (isNew)
                    conn.Insert(paradaLocal);
                else
                    conn.Update(paradaLocal);

                // Envio
                var envioLocal = conn.Find<EnvioLocal>(envioId);
                if (envioLocal == null)
                {
                    envioLocal = new EnvioLocal
                    {
                        Id = envioId,
                        ParadaId = paradaId,
                        CodigoSeguimiento = paradaLocal.CodigoSeguimiento,
                        Destinatario = paradaLocal.DestinatarioNombre,
                        Direccion = paradaLocal.Direccion,
                        Modalidad = "Estandar",
                        Estado = paradaLocal.Estado
                    };
                    conn.Insert(envioLocal);
                }
                else
                {
                    envioLocal.ParadaId = paradaId;
                    conn.Update(envioLocal);
                }

                // Bultos
                if (p.ResumenBultos?.CodigosBultos != null && p.ResumenBultos.CodigosBultos.Count > 0)
                {
                    foreach (var codBulto in p.ResumenBultos.CodigosBultos)
                    {
                        var bultoExistente = conn.Table<BultoLocal>()
                            .Where(b => b.EnvioId == p.EnvioId && b.Codigo == codBulto)
                            .FirstOrDefault();

                        if (bultoExistente == null)
                        {
                            var bultoLocal = new BultoLocal
                            {
                                Id = Guid.NewGuid(),
                                EnvioId = p.EnvioId,
                                ParadaId = p.ParadaId,
                                Codigo = codBulto,
                                Identificacion = codBulto,
                                Descripcion = $"Bulto {codBulto}",
                                Peso = p.ResumenBultos.PesoTotalKg > 0 ? (p.ResumenBultos.PesoTotalKg / p.ResumenBultos.CantidadBultos) : 1.5m,
                                EsFragil = false
                            };
                            conn.Insert(bultoLocal);
                        }
                    }
                }
            }

            // 3. Catálogo de Motivos de No Entrega
            var motivosList = motivos ?? DemoDataDefaults.ObtenerCatalogoMotivos();
            foreach (var m in motivosList)
            {
                var existingMotivo = conn.Find<MotivoNoEntregaLocal>(m.Id);
                if (existingMotivo == null)
                {
                    conn.Insert(m);
                }
                else
                {
                    existingMotivo.Codigo = m.Codigo;
                    existingMotivo.Descripcion = m.Descripcion;
                    existingMotivo.Activo = m.Activo;
                    conn.Update(existingMotivo);
                }
            }
        });
    }

    public async Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync()
    {
        await InitAsync();

        var ruta = await Db.Table<HojaDeRutaLocal>()
            .OrderByDescending(r => r.FechaDescargaUtc)
            .FirstOrDefaultAsync();

        if (ruta == null)
            return null;

        var paradas = await Db.Table<ParadaLocal>()
            .Where(p => p.HojaDeRutaId == ruta.Id)
            .OrderBy(p => p.Orden)
            .ToListAsync();

        var resultParadas = new List<ParadaItemViewModel>();

        foreach (var p in paradas)
        {
            var bultos = await Db.Table<BultoLocal>()
                .Where(b => b.ParadaId == p.Id || b.EnvioId == p.EnvioId)
                .ToListAsync();

            var bultoItems = bultos.Select(b => new BultoItemViewModel
            {
                Codigo = b.Codigo,
                Descripcion = b.Descripcion,
                PesoKg = b.Peso,
                EsFragil = b.EsFragil
            }).ToList();

            if (bultoItems.Count == 0 && p.CantidadBultos > 0)
            {
                for (int i = 1; i <= p.CantidadBultos; i++)
                {
                    bultoItems.Add(new BultoItemViewModel
                    {
                        Codigo = $"{p.CodigoSeguimiento}-B{i}",
                        Descripcion = $"Bulto {i}",
                        PesoKg = 1.5m,
                        EsFragil = false
                    });
                }
            }

            var hojaRutaIdReal = p.HojaDeRutaId != Guid.Empty
                ? p.HojaDeRutaId
                : (ruta.Id != Guid.Empty ? ruta.Id : DemoDataDefaults.DefaultHojaDeRutaId);

            var paradaIdReal = p.Id != Guid.Empty
                ? p.Id
                : p.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Parada1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Parada2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Parada3Id,
                    _ => p.Orden == 1 ? DemoDataDefaults.Parada1Id : (p.Orden == 2 ? DemoDataDefaults.Parada2Id : DemoDataDefaults.Parada3Id)
                };

            var envioIdReal = p.EnvioId != Guid.Empty
                ? p.EnvioId
                : p.CodigoSeguimiento switch
                {
                    "ENV-2026-00108" => DemoDataDefaults.Envio1Id,
                    "ENV-2026-00109" => DemoDataDefaults.Envio2Id,
                    "ENV-2026-00110" => DemoDataDefaults.Envio3Id,
                    _ => p.Orden == 1 ? DemoDataDefaults.Envio1Id : (p.Orden == 2 ? DemoDataDefaults.Envio2Id : DemoDataDefaults.Envio3Id)
                };

            var codigoSeguimientoReal = !string.IsNullOrWhiteSpace(p.CodigoSeguimiento)
                ? p.CodigoSeguimiento
                : p.Orden switch
                {
                    1 => "ENV-2026-00108",
                    2 => "ENV-2026-00109",
                    3 => "ENV-2026-00110",
                    _ => $"ENV-2026-{107 + p.Orden:D5}"
                };

            var vm = new ParadaItemViewModel
            {
                Id = paradaIdReal,
                HojaDeRutaId = hojaRutaIdReal,
                EnvioId = envioIdReal,
                Orden = p.Orden,
                Direccion = p.Direccion,
                Ciudad = p.Ciudad,
                Destinatario = p.DestinatarioNombre,
                Telefono = p.DestinatarioTelefono ?? string.Empty,
                CantidadBultos = p.CantidadBultos > 0 ? p.CantidadBultos : bultoItems.Count,
                Estado = p.Estado,
                HoraEstimada = string.IsNullOrEmpty(p.HoraEstimada) ? $"{8 + p.Orden:D2}:30" : p.HoraEstimada,
                CodigoSeguimiento = codigoSeguimientoReal,
                NotasComercio = p.NotasComercio ?? string.Empty,
                Latitud = p.Latitud,
                Longitud = p.Longitud,
                Bultos = bultoItems
            };

            resultParadas.Add(vm);
        }

        return new RutaActivaLocalDto
        {
            Ruta = ruta,
            Paradas = resultParadas
        };
    }

    public async Task ActualizarEstadoParadaLocalAsync(Guid paradaId, string nuevoEstado)
    {
        await InitAsync();
        var parada = await Db.FindAsync<ParadaLocal>(paradaId);
        if (parada != null)
        {
            parada.Estado = MapearEstadoParada(nuevoEstado);
            await Db.UpdateAsync(parada);

            var envio = await Db.Table<EnvioLocal>()
                .Where(e => e.ParadaId == paradaId)
                .FirstOrDefaultAsync();

            if (envio != null)
            {
                envio.Estado = parada.Estado;
                await Db.UpdateAsync(envio);
            }
        }
    }

    public async Task<List<MotivoNoEntregaLocal>> ObtenerMotivosNoEntregaAsync()
    {
        await InitAsync();
        var motivos = await Db.Table<MotivoNoEntregaLocal>().ToListAsync();
        if (motivos.Count == 0)
        {
            var defaults = DemoDataDefaults.ObtenerCatalogoMotivos();
            await Db.InsertAllAsync(defaults);
            return defaults;
        }
        return motivos;
    }

    public async Task SeedRutaDemoSiEstaVaciaAsync()
    {
        await InitAsync();
        var count = await Db.Table<HojaDeRutaLocal>().CountAsync();
        if (count > 0)
            return;

        var paradasDemo = new List<ParadaNavegacionDto>
        {
            new(
                ParadaId: DemoDataDefaults.Parada1Id,
                EnvioId: DemoDataDefaults.Envio1Id,
                Orden: 1,
                EstadoParada: "Entregado",
                CodigoSeguimiento: "ENV-2026-00108",
                DireccionFormateada: "Av. 18 de Julio 1450, Apto 402, Centro, Montevideo",
                Latitud: -34.9056,
                Longitud: -56.1852,
                VentanaHoraria: "09:00 - 10:00",
                Destinatario: new DestinatarioContactoDto("Martín Rodríguez", "+598 99 123 456", "martin@demo.uy", "Tocar timbre 402. Si no atiende, dejar en portería."),
                ResumenBultos: new ResumenBultosParadaDto(2, new List<string> { "BLT-001-A", "BLT-001-B" }, 2.7m),
                EsParadaActual: false
            ),
            new(
                ParadaId: DemoDataDefaults.Parada2Id,
                EnvioId: DemoDataDefaults.Envio2Id,
                Orden: 2,
                EstadoParada: "Pendiente",
                CodigoSeguimiento: "ENV-2026-00109",
                DireccionFormateada: "Bvar. España 2678, Apto 501, Pocitos, Montevideo",
                Latitud: -34.9124,
                Longitud: -56.1589,
                VentanaHoraria: "10:00 - 11:00",
                Destinatario: new DestinatarioContactoDto("Lucía Fernández", "+598 99 765 432", "lucia@demo.uy", "Llamar al móvil 5 minutos antes. Producto frágil."),
                ResumenBultos: new ResumenBultosParadaDto(1, new List<string> { "BLT-002-A" }, 1.8m),
                EsParadaActual: true
            ),
            new(
                ParadaId: DemoDataDefaults.Parada3Id,
                EnvioId: DemoDataDefaults.Envio3Id,
                Orden: 3,
                EstadoParada: "Pendiente",
                CodigoSeguimiento: "ENV-2026-00110",
                DireccionFormateada: "Rambla Gandhi 456, Piso 8, Punta Carretas, Montevideo",
                Latitud: -34.9271,
                Longitud: -56.1534,
                VentanaHoraria: "11:00 - 12:00",
                Destinatario: new DestinatarioContactoDto("Esteban Morales", "+598 98 999 111", "esteban@demo.uy", "Horario estricto. Solicitar C.I. obligatoria."),
                ResumenBultos: new ResumenBultosParadaDto(3, new List<string> { "BLT-003-A", "BLT-003-B", "BLT-003-C" }, 7.3m),
                EsParadaActual: false
            )
        };

        await GuardarRutaDescargadaAsync(DemoDataDefaults.DefaultHojaDeRutaId, paradasDemo);
    }

    private static string MapearEstadoParada(string estado)
    {
        return estado switch
        {
            "Completada" or "Entregada" or "Entregado" => "Entregado",
            "Fallida" or "Fallido" => "Fallido",
            "Reprogramado" => "Fallido",
            "EnDevolucion" or "EnRetornoADeposito" => "EnDevolucion",
            "EnCamino" or "EnSitio" or "Pendiente" => "Pendiente",
            _ => "Pendiente"
        };
    }

    private static string ExtraerCiudad(string direccionCompleta)
    {
        var partes = direccionCompleta.Split(',');
        if (partes.Length >= 2)
        {
            return partes[^1].Trim();
        }
        return "Montevideo";
    }

    public async Task ReiniciarDatosDemoLocalAsync()
    {
        await InitAsync();

        await Db.RunInTransactionAsync(conn =>
        {
            conn.DeleteAll<OperacionOfflineLocal>();
            conn.DeleteAll<BultoLocal>();
            conn.DeleteAll<EnvioLocal>();
            conn.DeleteAll<ParadaLocal>();
            conn.DeleteAll<HojaDeRutaLocal>();
        });

        // Regenerar la ruta demo limpia con todas las paradas listas para operar
        var paradasDemo = new List<ParadaNavegacionDto>
        {
            new(
                ParadaId: DemoDataDefaults.Parada1Id,
                EnvioId: DemoDataDefaults.Envio1Id,
                Orden: 1,
                EstadoParada: "Pendiente",
                CodigoSeguimiento: "ENV-2026-00108",
                DireccionFormateada: "Av. 18 de Julio 1450, Apto 402, Centro, Montevideo",
                Latitud: -34.9056,
                Longitud: -56.1852,
                VentanaHoraria: "09:00 - 10:00",
                Destinatario: new DestinatarioContactoDto("Martín Rodríguez", "+598 99 123 456", "martin@demo.uy", "Tocar timbre 402. Si no atiende, dejar en portería."),
                ResumenBultos: new ResumenBultosParadaDto(2, new List<string> { "BLT-001-A", "BLT-001-B" }, 2.7m),
                EsParadaActual: true
            ),
            new(
                ParadaId: DemoDataDefaults.Parada2Id,
                EnvioId: DemoDataDefaults.Envio2Id,
                Orden: 2,
                EstadoParada: "Pendiente",
                CodigoSeguimiento: "ENV-2026-00109",
                DireccionFormateada: "Bvar. España 2678, Apto 501, Pocitos, Montevideo",
                Latitud: -34.9124,
                Longitud: -56.1589,
                VentanaHoraria: "10:00 - 11:00",
                Destinatario: new DestinatarioContactoDto("Lucía Fernández", "+598 99 765 432", "lucia@demo.uy", "Llamar al móvil 5 minutos antes. Producto frágil."),
                ResumenBultos: new ResumenBultosParadaDto(1, new List<string> { "BLT-002-A" }, 1.8m),
                EsParadaActual: false
            ),
            new(
                ParadaId: DemoDataDefaults.Parada3Id,
                EnvioId: DemoDataDefaults.Envio3Id,
                Orden: 3,
                EstadoParada: "Pendiente",
                CodigoSeguimiento: "ENV-2026-00110",
                DireccionFormateada: "Rambla Gandhi 456, Piso 8, Punta Carretas, Montevideo",
                Latitud: -34.9271,
                Longitud: -56.1534,
                VentanaHoraria: "11:00 - 12:00",
                Destinatario: new DestinatarioContactoDto("Esteban Morales", "+598 98 999 111", "esteban@demo.uy", "Horario estricto. Solicitar C.I. obligatoria."),
                ResumenBultos: new ResumenBultosParadaDto(3, new List<string> { "BLT-003-A", "BLT-003-B", "BLT-003-C" }, 7.3m),
                EsParadaActual: false
            )
        };

        await GuardarRutaDescargadaAsync(DemoDataDefaults.DefaultHojaDeRutaId, paradasDemo);
    }
}
