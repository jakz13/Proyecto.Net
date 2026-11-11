using System.Security.Cryptography;
using System.Text.Json;
using UltimaMilla.Mobile.Models.Sync;
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
    Task MarcarComoSincronizadosAsync(IEnumerable<int> ids);
    Task RegistrarErrorAsync(int id, string error);
    Task<int> ObtenerCantidadPendientesAsync();

    // Persistencia y consulta de hoja de ruta local (UC-17, UC-18, UC-19, UC-20, UC-21)
    Task GuardarRutaDescargadaAsync(Guid hojaDeRutaId, List<ParadaNavegacionDto> paradas, List<MotivoNoEntregaLocal>? motivos = null, string? estadoRuta = null, string? numeroRuta = null, DateTime? fechaRuta = null);
    Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync(Guid? hojaDeRutaId = null);
    Task<List<HojaDeRutaLocal>> ListarHojasDeRutaLocalesAsync();
    Task FinalizarHojaDeRutaLocalAsync(Guid hojaDeRutaId);
    Task ActualizarEstadoParadaLocalAsync(Guid paradaId, string nuevoEstado);
    Task MarcarEntregaParcialLocalAsync(Guid paradaId, IReadOnlyCollection<string> codigosEntregados);
    Task ActualizarEstadoHojaDeRutaAsync(Guid hojaDeRutaId, string nuevoEstado);
    Task MarcarBultoVerificadoCargaAsync(Guid bultoId, bool verificado);
    Task MarcarBultoVerificadoEntregaAsync(Guid bultoId, bool verificado);
    Task MarcarBultosParadaVerificadosEntregaAsync(Guid paradaId, bool verificado);
    Task ConfirmarCargaLocalAsync(Guid hojaDeRutaId, IReadOnlyCollection<string> codigosCargados);
    Task LimpiarDatosDeRutaAsync();
    Task ActualizarSecuenciaParadasAsync(IReadOnlyList<ParadaItemViewModel> paradasOrdenadas);
    Task<List<MotivoNoEntregaLocal>> ObtenerMotivosNoEntregaAsync();
    Task<EventoGeocercaLocal> RegistrarEventoGeocercaLocalAsync(EventoGeocercaLocal evento);
    Task<List<EventoGeocercaLocal>> ObtenerEventosGeocercaAsync(int limite = 50);
    Task MarcarEventoGeocercaSincronizadoAsync(int eventoLocalId);
    Task<EstadoCifradoDiagnosticoDto> VerificarCifradoActivoAsync();
}

public record EstadoCifradoDiagnosticoDto(
    bool CifradoActivo,
    string CipherVersion,
    bool ArchivoEsCifradoEnDisco,
    string MensajeDiagnostico);

public class LocalDatabaseService : ILocalDatabaseService
{
    private const string KeyStorageAlias = "sqlite_db_encryption_key";
    private const string DatabaseFileName = "ultimamilla_cipher_v1.db3";

#if DEBUG
    private const string ClaveDesarrolloPreferencia = "sqlite_db_dev_key";
#endif

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
    /// En modo DEBUG usa una clave aleatoria local si el entorno de ejecución no soporta SecureStorage.
    /// En modo RELEASE se elimina la clave fija y se exige SecureStorage bajo excepción explícita impidiendo claves débiles.
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

            var keyBytes = RandomNumberGenerator.GetBytes(32);
            var newKey = Convert.ToBase64String(keyBytes);
            await SecureStorage.Default.SetAsync(KeyStorageAlias, newKey);
            return newKey;
        }
        catch (Exception ex)
        {
#if DEBUG
            // Solo desarrollo (por ejemplo Windows sin empaquetar, donde SecureStorage puede no estar disponible):
            // clave aleatoria propia del equipo, nunca un valor fijo incluido en el código.
            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] SecureStorage no disponible en DEBUG, usando clave aleatoria local: {ex.Message}");
            var claveDev = Preferences.Default.Get<string?>(ClaveDesarrolloPreferencia, null);
            if (string.IsNullOrWhiteSpace(claveDev))
            {
                claveDev = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                Preferences.Default.Set(ClaveDesarrolloPreferencia, claveDev);
            }
            return claveDev;
#else
            throw new InvalidOperationException(
                "Fallo crítico de seguridad: No se pudo acceder a SecureStorage para obtener o almacenar la clave de cifrado de la base de datos local en producción.",
                ex);
#endif
        }
    }

    /// <summary>
    /// Crea la conexión cifrada con SQLCipher y las tablas locales necesarias para la operativa offline si aún no existen.
    /// </summary>
    public async Task InitAsync()
    {
        // Inicializar proveedor nativo de SQLCipher antes de abrir cualquier conexión SQLite
        SQLitePCL.Batteries_V2.Init();

        if (_initialized && _database != null)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized && _database != null)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
            var encryptionKey = await GetOrGenerateEncryptionKeyAsync();

            var connectionString = new SQLiteConnectionString(
                dbPath,
                openFlags: SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
                storeDateTimeAsTicks: true,
                key: encryptionKey);

            _database = new SQLiteAsyncConnection(connectionString);

            try
            {
                await CrearTablasAsync(_database);
            }
            catch (SQLiteException ex) when (ex.Result == SQLite3.Result.NonDBFile || ex.Result == SQLite3.Result.Corrupt)
            {
                // Si la base en disco es incompatible (ej. base previa en texto plano o clave corrupta),
                // resguardamos el archivo corrupto como respaldo antes de recrear para no destruir datos de forma silenciosa.
                try
                {
                    await _database.CloseAsync();
                    if (File.Exists(dbPath))
                    {
                        var backupCorruptPath = $"{dbPath}.corrupt.bak";
                        File.Move(dbPath, backupCorruptPath, overwrite: true);
                    }

                    _database = new SQLiteAsyncConnection(connectionString);
                    await CrearTablasAsync(_database);
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

    private static async Task CrearTablasAsync(SQLiteAsyncConnection database)
    {
        await database.CreateTableAsync<HojaDeRutaLocal>();
        await database.CreateTableAsync<ParadaLocal>();
        await database.CreateTableAsync<EnvioLocal>();
        await database.CreateTableAsync<BultoLocal>();
        await database.CreateTableAsync<MotivoNoEntregaLocal>();
        await database.CreateTableAsync<OperacionOfflineLocal>();
        await database.CreateTableAsync<EventoGeocercaLocal>();
    }

    /// <summary>
    /// Verifica que SQLCipher esté activo mediante PRAGMA cipher_version y que el archivo .db3 en disco
    /// no contenga la cabecera estándar en texto plano 'SQLite format 3'.
    /// </summary>
    public async Task<EstadoCifradoDiagnosticoDto> VerificarCifradoActivoAsync()
    {
        await InitAsync();

        string cipherVersion = string.Empty;
        bool cipherActivo = false;
        string mensaje;
        bool archivoEsCifradoEnDisco = false;

        try
        {
            cipherVersion = await Db.ExecuteScalarAsync<string>("PRAGMA cipher_version;") ?? string.Empty;
            cipherActivo = !string.IsNullOrWhiteSpace(cipherVersion);
        }
        catch (Exception ex)
        {
            cipherActivo = false;
            cipherVersion = string.Empty;
            mensaje = $"Fallo al consultar PRAGMA cipher_version: {ex.Message}";
        }

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        if (File.Exists(dbPath))
        {
            try
            {
                using var stream = File.OpenRead(dbPath);
                var headerBytes = new byte[16];
                int bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

                // La cabecera estándar de SQLite en texto plano es "SQLite format 3\0"
                var sqlitePlaintextHeader = "SQLite format 3\0"u8.ToArray();
                bool tieneCabeceraTextoPlano = bytesRead >= 16 && headerBytes.SequenceEqual(sqlitePlaintextHeader);

                archivoEsCifradoEnDisco = bytesRead > 0 && !tieneCabeceraTextoPlano;

                if (cipherActivo && archivoEsCifradoEnDisco)
                {
                    mensaje = $"Cifrado activo con SQLCipher v{cipherVersion}. Base de datos cifrada en disco (sin cabecera de texto plano).";
                }
                else if (tieneCabeceraTextoPlano)
                {
                    mensaje = "Advertencia de seguridad: El archivo contiene cabecera en texto plano 'SQLite format 3'.";
                }
                else if (!cipherActivo)
                {
                    mensaje = "Advertencia de seguridad: PRAGMA cipher_version no devolvió versión de SQLCipher.";
                }
                else
                {
                    mensaje = "Estado de cifrado verificado con observaciones.";
                }
            }
            catch (Exception ex)
            {
                mensaje = $"Error al leer cabecera del archivo en disco: {ex.Message}";
            }
        }
        else
        {
            mensaje = "El archivo de base de datos aún no ha sido creado en disco.";
        }

        return new EstadoCifradoDiagnosticoDto(
            CifradoActivo: cipherActivo && archivoEsCifradoEnDisco,
            CipherVersion: cipherVersion,
            ArchivoEsCifradoEnDisco: archivoEsCifradoEnDisco,
            MensajeDiagnostico: mensaje);
    }

    public async Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson)
    {
        await InitAsync();

        // Control de tope máximo para telemetría GPS (1000 posiciones pendientes como máximo)
        // para evitar crecimiento desmedido en jornadas largas sin conexión, sin afectar operaciones de negocio.
        if (tipoOperacion == TiposOperacionOffline.TelemetriaGps)
        {
            const int maxGpsPendientes = 1000;
            var conteoGps = await Db.Table<OperacionOfflineLocal>()
                .Where(o => !o.Sincronizado && o.TipoOperacion == TiposOperacionOffline.TelemetriaGps)
                .CountAsync();

            if (conteoGps >= maxGpsPendientes)
            {
                int aEliminar = (conteoGps - maxGpsPendientes) + 1;
                var viejos = await Db.Table<OperacionOfflineLocal>()
                    .Where(o => !o.Sincronizado && o.TipoOperacion == TiposOperacionOffline.TelemetriaGps)
                    .OrderBy(o => o.FechaCreacionUtc)
                    .Take(aEliminar)
                    .ToListAsync();

                foreach (var viejo in viejos)
                {
                    await Db.DeleteAsync(viejo);
                }
            }
        }

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

    public async Task MarcarComoSincronizadosAsync(IEnumerable<int> ids)
    {
        await InitAsync();
        foreach (var id in ids)
        {
            var op = await Db.FindAsync<OperacionOfflineLocal>(id);
            if (op != null)
            {
                op.Sincronizado = true;
                await Db.UpdateAsync(op);
            }
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

    public async Task GuardarRutaDescargadaAsync(
        Guid hojaDeRutaId,
        List<ParadaNavegacionDto> paradas,
        List<MotivoNoEntregaLocal>? motivos = null,
        string? estadoRuta = null,
        string? numeroRuta = null,
        DateTime? fechaRuta = null)
    {
        if (hojaDeRutaId == Guid.Empty)
            throw new ArgumentException("La hoja de ruta descargada no trae identificador.", nameof(hojaDeRutaId));

        await InitAsync();

        await Db.RunInTransactionAsync(conn =>
        {
            // 1. Hoja de ruta: el estado y el número los manda siempre el servidor
            var ruta = conn.Find<HojaDeRutaLocal>(hojaDeRutaId);
            if (ruta == null)
            {
                ruta = new HojaDeRutaLocal
                {
                    Id = hojaDeRutaId,
                    Fecha = fechaRuta ?? DateTime.Today,
                    Estado = !string.IsNullOrWhiteSpace(estadoRuta) ? estadoRuta : "Liberada",
                    NumeroRuta = !string.IsNullOrWhiteSpace(numeroRuta) ? numeroRuta : $"HR-{(fechaRuta ?? DateTime.Today):yyyyMMdd}-01",
                    TotalParadas = paradas.Count,
                    FechaDescargaUtc = DateTime.UtcNow
                };
                conn.Insert(ruta);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(estadoRuta))
                    ruta.Estado = estadoRuta;
                if (!string.IsNullOrWhiteSpace(numeroRuta))
                    ruta.NumeroRuta = numeroRuta;
                if (fechaRuta.HasValue)
                    ruta.Fecha = fechaRuta.Value;
                ruta.TotalParadas = paradas.Count;
                ruta.FechaDescargaUtc = DateTime.UtcNow;
                conn.Update(ruta);
            }

            bool rutaYaDespachada = ruta.Estado is "Despachada" or "EnTransito" or "Finalizada";

            // 2. Paradas que el servidor ya no incluye (reasignadas o retiradas de la ruta) y que no tienen nada pendiente de enviar
            var idsServidor = paradas.Where(p => p.ParadaId != Guid.Empty).Select(p => p.ParadaId).ToHashSet();
            var obsoletas = conn.Table<ParadaLocal>()
                .Where(p => p.HojaDeRutaId == hojaDeRutaId)
                .ToList()
                .Where(p => !idsServidor.Contains(p.Id) && p.Estado == "Pendiente")
                .ToList();
            foreach (var obsoleta in obsoletas)
            {
                foreach (var bulto in conn.Table<BultoLocal>().Where(b => b.ParadaId == obsoleta.Id).ToList())
                    conn.Delete(bulto);
                foreach (var envioObsoleto in conn.Table<EnvioLocal>().Where(e => e.ParadaId == obsoleta.Id).ToList())
                    conn.Delete(envioObsoleto);
                conn.Delete(obsoleta);
            }

            // 3. Paradas, envíos y bultos de lo que manda el servidor
            foreach (var p in paradas)
            {
                if (p.ParadaId == Guid.Empty)
                    continue; // una parada sin identificador no se puede operar ni sincronizar

                var paradaId = p.ParadaId;
                var esRetiro = string.Equals(p.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase);
                // Una parada de retiro no tiene envío: se identifica por el retiro
                var envioId = esRetiro ? (p.RetiroId ?? p.ParadaId) : p.EnvioId;

                var paradaLocal = conn.Find<ParadaLocal>(paradaId);
                bool isNew = paradaLocal == null;
                paradaLocal ??= new ParadaLocal { Id = paradaId };

                paradaLocal.HojaDeRutaId = hojaDeRutaId;
                paradaLocal.EnvioId = envioId;
                paradaLocal.Tipo = esRetiro ? "Retiro" : "Entrega";
                paradaLocal.RetiroId = esRetiro ? p.RetiroId : null;
                paradaLocal.Orden = p.Orden;
                paradaLocal.OrdenVisita = isNew || paradaLocal.OrdenVisita <= 0 ? p.Orden : paradaLocal.OrdenVisita;

                // Lo completado en el dispositivo y todavía no sincronizado no se pisa con el estado viejo del servidor
                if (isNew || paradaLocal.Estado is not ("Entregado" or "Fallido" or "EnDevolucion"))
                    paradaLocal.Estado = MapearEstadoParada(p.EstadoParada);

                paradaLocal.CodigoSeguimiento = p.CodigoSeguimiento ?? string.Empty;
                paradaLocal.Direccion = p.DireccionFormateada ?? string.Empty;
                paradaLocal.Ciudad = ExtraerCiudad(paradaLocal.Direccion);
                paradaLocal.DestinatarioNombre = p.Destinatario?.Nombre ?? string.Empty;
                paradaLocal.DestinatarioTelefono = p.Destinatario?.Telefono;
                paradaLocal.DestinatarioEmail = p.Destinatario?.Email;
                paradaLocal.NotasComercio = p.Destinatario?.ReferenciaEntrega ?? string.Empty;
                paradaLocal.Latitud = p.Latitud ?? 0;
                paradaLocal.Longitud = p.Longitud ?? 0;
                paradaLocal.VentanaHoraria = p.VentanaHoraria;
                paradaLocal.HoraEstimada = string.Empty;
                paradaLocal.CantidadBultos = p.ResumenBultos?.CantidadBultos ?? 0;
                paradaLocal.EsParadaActual = p.EsParadaActual;

                if (isNew)
                    conn.Insert(paradaLocal);
                else
                    conn.Update(paradaLocal);

                bool bultoYaCargado = rutaYaDespachada || paradaLocal.Estado is "Entregado" or "Fallido" or "EnDevolucion";

                // Envío (solo de las entregas)
                if (!esRetiro)
                {
                    var envioLocal = conn.Find<EnvioLocal>(envioId);
                    if (envioLocal == null)
                    {
                        conn.Insert(new EnvioLocal
                        {
                            Id = envioId,
                            ParadaId = paradaId,
                            CodigoSeguimiento = paradaLocal.CodigoSeguimiento,
                            Destinatario = paradaLocal.DestinatarioNombre,
                            Direccion = paradaLocal.Direccion,
                            Modalidad = "Estandar",
                            Estado = paradaLocal.Estado != "Pendiente" ? paradaLocal.Estado : (rutaYaDespachada ? "EnTransito" : "AsignadoARuta")
                        });
                    }
                    else
                    {
                        envioLocal.ParadaId = paradaId;
                        if (paradaLocal.Estado != "Pendiente")
                            envioLocal.Estado = paradaLocal.Estado;
                        else if (rutaYaDespachada)
                            envioLocal.Estado = "EnTransito";
                        else
                            envioLocal.Estado = "AsignadoARuta";
                        conn.Update(envioLocal);
                    }
                }

                // Bultos: los que manda el servidor; los que ya no vienen (y no se entregaron) se eliminan
                var codigos = p.ResumenBultos?.CodigosBultos ?? new List<string>();
                var existentes = conn.Table<BultoLocal>().Where(b => b.ParadaId == paradaId).ToList();
                foreach (var sobrante in existentes.Where(b => !codigos.Contains(b.Codigo) && !b.VerificadoEnEntrega))
                    conn.Delete(sobrante);

                var pesoPorBulto = codigos.Count > 0 && p.ResumenBultos is { PesoTotalKg: > 0 }
                    ? p.ResumenBultos.PesoTotalKg / codigos.Count
                    : 0m;

                foreach (var codBulto in codigos)
                {
                    var bultoExistente = existentes.FirstOrDefault(b => b.Codigo == codBulto);
                    if (bultoExistente == null)
                    {
                        conn.Insert(new BultoLocal
                        {
                            Id = Guid.NewGuid(),
                            EnvioId = envioId,
                            ParadaId = paradaId,
                            Codigo = codBulto,
                            Identificacion = codBulto,
                            Descripcion = codBulto,
                            Peso = pesoPorBulto,
                            EsFragil = false,
                            VerificadoEnCarga = bultoYaCargado,
                            VerificadoEnEntrega = paradaLocal.Estado == "Entregado"
                        });
                    }
                    else
                    {
                        bultoExistente.EnvioId = envioId;
                        // Lo escaneado en el dispositivo se conserva; solo se sube la marca si el servidor ya despachó la ruta
                        if (bultoYaCargado)
                            bultoExistente.VerificadoEnCarga = true;
                        conn.Update(bultoExistente);
                    }
                }
            }

            // 4. Catálogo de motivos de no entrega: el del operador, tal como lo manda el servidor
            if (motivos != null)
            {
                foreach (var m in motivos)
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
            }
        });
    }

    public async Task<List<HojaDeRutaLocal>> ListarHojasDeRutaLocalesAsync()
    {
        await InitAsync();
        var rutas = await Db.Table<HojaDeRutaLocal>()
            .OrderByDescending(r => r.FechaDescargaUtc)
            .ToListAsync();

        int PrioridadEstado(string estado) => estado switch
        {
            "EnTransito" => 1,
            "Despachada" => 2,
            "EnCarga" => 3,
            "Liberada" => 4,
            "Borrador" => 5,
            _ => 10
        };

        return rutas
            .OrderBy(r => PrioridadEstado(r.Estado))
            .ThenByDescending(r => r.FechaDescargaUtc)
            .ToList();
    }

    public async Task FinalizarHojaDeRutaLocalAsync(Guid hojaDeRutaId)
    {
        await InitAsync();
        var ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId);
        if (ruta == null)
        {
            ruta = await Db.Table<HojaDeRutaLocal>()
                .OrderByDescending(r => r.FechaDescargaUtc)
                .FirstOrDefaultAsync();
        }

        if (ruta != null)
        {
            ruta.Estado = "Finalizada";
            await Db.UpdateAsync(ruta);
        }
    }

    public async Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync(Guid? hojaDeRutaId = null)
    {
        await InitAsync();

        HojaDeRutaLocal? ruta = null;

        if (hojaDeRutaId.HasValue && hojaDeRutaId.Value != Guid.Empty)
        {
            ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId.Value);
        }
        else
        {
            var todas = await Db.Table<HojaDeRutaLocal>()
                .OrderByDescending(r => r.FechaDescargaUtc)
                .ToListAsync();

            // Prioridad de ruta activa: EnTransito > Despachada > EnCarga > Liberada
            ruta = todas.FirstOrDefault(r => r.Estado == "EnTransito")
                ?? todas.FirstOrDefault(r => r.Estado == "Despachada")
                ?? todas.FirstOrDefault(r => r.Estado == "EnCarga")
                ?? todas.FirstOrDefault(r => r.Estado == "Liberada");
        }

        if (ruta == null)
            return null;

        bool rutaYaDespachada = ruta.Estado is "Despachada" or "EnTransito" or "Finalizada";
        bool enPrecarga = ruta.Estado is "Liberada" or "EnCarga" or "Borrador";

        var paradas = await Db.Table<ParadaLocal>()
            .Where(p => p.HojaDeRutaId == ruta.Id && p.Estado != "Cancelada")
            .OrderBy(p => p.Orden)
            .ToListAsync();

        var resultParadas = new List<ParadaItemViewModel>();

        foreach (var p in paradas)
        {
            var bultos = await Db.Table<BultoLocal>()
                .Where(b => b.ParadaId == p.Id)
                .ToListAsync();

            bool bultoDebeEstarCargado = rutaYaDespachada || p.Estado is "Entregado" or "Fallido" or "EnDevolucion";

            foreach (var b in bultos)
            {
                if (bultoDebeEstarCargado && !b.VerificadoEnCarga)
                {
                    b.VerificadoEnCarga = true;
                    await Db.UpdateAsync(b);
                }
            }

            var bultoItems = bultos.Select(b => new BultoItemViewModel
            {
                Id = b.Id,
                ParadaId = p.Id,
                EnvioId = p.EnvioId,
                Codigo = b.Codigo,
                Descripcion = b.Descripcion,
                PesoKg = b.Peso,
                EsFragil = b.EsFragil,
                VerificadoEnCarga = b.VerificadoEnCarga,
                VerificadoEnEntrega = b.VerificadoEnEntrega
            }).ToList();

            resultParadas.Add(new ParadaItemViewModel
            {
                Id = p.Id,
                HojaDeRutaId = ruta.Id,
                EnvioId = p.EnvioId,
                Tipo = string.IsNullOrWhiteSpace(p.Tipo) ? "Entrega" : p.Tipo,
                RetiroId = p.RetiroId,
                Orden = p.Orden,
                Direccion = p.Direccion,
                Ciudad = p.Ciudad,
                Destinatario = p.DestinatarioNombre,
                Telefono = p.DestinatarioTelefono ?? string.Empty,
                CantidadBultos = p.CantidadBultos > 0 ? p.CantidadBultos : bultoItems.Count,
                Estado = p.Estado,
                EnPrecarga = enPrecarga,
                HoraEstimada = p.HoraEstimada,
                VentanaHoraria = !string.IsNullOrWhiteSpace(p.VentanaHoraria) ? p.VentanaHoraria : "Sin franja asignada",
                CodigoSeguimiento = p.CodigoSeguimiento,
                NotasComercio = p.NotasComercio ?? string.Empty,
                Latitud = p.Latitud,
                Longitud = p.Longitud,
                Bultos = bultoItems
            });
        }

        return new RutaActivaLocalDto
        {
            Ruta = ruta,
            Paradas = resultParadas
        };
    }

    /// <summary>Borra la ruta y todo lo derivado de ella (al cerrar sesión: el siguiente repartidor no debe ver datos del anterior). La cola de operaciones sin enviar se conserva.</summary>
    public async Task LimpiarDatosDeRutaAsync()
    {
        await InitAsync();
        await Db.RunInTransactionAsync(conn =>
        {
            conn.DeleteAll<BultoLocal>();
            conn.DeleteAll<EnvioLocal>();
            conn.DeleteAll<ParadaLocal>();
            conn.DeleteAll<HojaDeRutaLocal>();
            conn.DeleteAll<MotivoNoEntregaLocal>();
        });
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

            // Al confirmarse la entrega, se consideran implícitamente entregados todos los bultos asociados
            if (parada.Estado == "Entregado")
            {
                var bultos = await Db.Table<BultoLocal>()
                    .Where(b => b.ParadaId == paradaId)
                    .ToListAsync();

                foreach (var b in bultos)
                {
                    b.VerificadoEnEntrega = true;
                    await Db.UpdateAsync(b);
                }
            }
        }
    }

    /// <summary>
    /// Entrega parcial: la parada se da por atendida y solo los bultos entregados quedan marcados como tales;
    /// los demás siguen pendientes (el servidor aplica la política del operador: nueva visita o devolución).
    /// </summary>
    public async Task MarcarEntregaParcialLocalAsync(Guid paradaId, IReadOnlyCollection<string> codigosEntregados)
    {
        await InitAsync();
        var entregados = new HashSet<string>(codigosEntregados.Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase);

        var parada = await Db.FindAsync<ParadaLocal>(paradaId);
        if (parada == null)
            return;

        parada.Estado = "Entregado";
        await Db.UpdateAsync(parada);

        foreach (var bulto in await Db.Table<BultoLocal>().Where(b => b.ParadaId == paradaId).ToListAsync())
        {
            bulto.VerificadoEnEntrega = entregados.Contains(bulto.Codigo.Trim());
            await Db.UpdateAsync(bulto);
        }
    }

    public async Task ActualizarEstadoHojaDeRutaAsync(Guid hojaDeRutaId, string nuevoEstado)
    {
        await InitAsync();
        var ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId);
        if (ruta == null)
        {
            ruta = await Db.Table<HojaDeRutaLocal>().OrderByDescending(r => r.FechaDescargaUtc).FirstOrDefaultAsync();
        }
        if (ruta != null)
        {
            ruta.Estado = nuevoEstado;
            await Db.UpdateAsync(ruta);
        }
    }

    public async Task MarcarBultoVerificadoCargaAsync(Guid bultoId, bool verificado)
    {
        await InitAsync();
        var bulto = await Db.FindAsync<BultoLocal>(bultoId);
        if (bulto != null)
        {
            bulto.VerificadoEnCarga = verificado;
            await Db.UpdateAsync(bulto);
        }
    }

    public async Task MarcarBultoVerificadoEntregaAsync(Guid bultoId, bool verificado)
    {
        await InitAsync();
        var bulto = await Db.FindAsync<BultoLocal>(bultoId);
        if (bulto != null)
        {
            bulto.VerificadoEnEntrega = verificado;
            await Db.UpdateAsync(bulto);
        }
    }

    public async Task MarcarBultosParadaVerificadosEntregaAsync(Guid paradaId, bool verificado)
    {
        await InitAsync();
        var bultos = await Db.Table<BultoLocal>()
            .Where(b => b.ParadaId == paradaId)
            .ToListAsync();

        foreach (var b in bultos)
        {
            b.VerificadoEnEntrega = verificado;
            await Db.UpdateAsync(b);
        }
    }

    /// <summary>
    /// Registra en el dispositivo los bultos escaneados al cargar el vehículo. La hoja de ruta queda "EnCarga": quien la despacha es el
    /// despachador desde el depósito, y el estado "Despachada" solo lo fija el servidor (la app lo recibe al descargar la ruta).
    /// </summary>
    public async Task ConfirmarCargaLocalAsync(Guid hojaDeRutaId, IReadOnlyCollection<string> codigosCargados)
    {
        await InitAsync();

        var setCodigos = new HashSet<string>(
            (codigosCargados ?? Array.Empty<string>()).Select(c => c.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId);
        if (ruta == null)
            return;

        var paradas = await Db.Table<ParadaLocal>().Where(p => p.HojaDeRutaId == ruta.Id).ToListAsync();
        foreach (var parada in paradas)
        {
            var bultosParada = await Db.Table<BultoLocal>().Where(b => b.ParadaId == parada.Id).ToListAsync();
            foreach (var b in bultosParada)
            {
                b.VerificadoEnCarga = setCodigos.Contains(b.Codigo.Trim());
                await Db.UpdateAsync(b);
            }
        }

        if (ruta.Estado is "Liberada" or "Borrador")
        {
            ruta.Estado = "EnCarga";
            await Db.UpdateAsync(ruta);
        }
    }

    public async Task ActualizarSecuenciaParadasAsync(IReadOnlyList<ParadaItemViewModel> paradasOrdenadas)
    {
        if (paradasOrdenadas == null || paradasOrdenadas.Count == 0)
            return;

        await InitAsync();

        await Db.RunInTransactionAsync(conn =>
        {
            for (int i = 0; i < paradasOrdenadas.Count; i++)
            {
                var item = paradasOrdenadas[i];
                int nuevoOrden = i + 1;

                var paradaLocal = conn.Find<ParadaLocal>(item.Id);
                if (paradaLocal == null && !string.IsNullOrWhiteSpace(item.CodigoSeguimiento))
                {
                    paradaLocal = conn.Table<ParadaLocal>()
                        .Where(p => p.CodigoSeguimiento == item.CodigoSeguimiento)
                        .FirstOrDefault();
                }

                if (paradaLocal != null)
                {
                    paradaLocal.Orden = nuevoOrden;
                    paradaLocal.OrdenVisita = nuevoOrden;
                    paradaLocal.HoraEstimada = $"{8 + nuevoOrden:D2}:30";
                    paradaLocal.EsParadaActual = nuevoOrden == 1;
                    conn.Update(paradaLocal);
                }
            }
        });
    }

    public async Task<List<MotivoNoEntregaLocal>> ObtenerMotivosNoEntregaAsync()
    {
        await InitAsync();
        // El catálogo es del operador y llega con la ruta; sin ruta descargada no hay motivos que ofrecer
        return await Db.Table<MotivoNoEntregaLocal>().Where(m => m.Activo).ToListAsync();
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
        return string.Empty;
    }

    public async Task<EventoGeocercaLocal> RegistrarEventoGeocercaLocalAsync(EventoGeocercaLocal evento)
    {
        await InitAsync();
        evento.Sincronizado = false;
        await Db.InsertAsync(evento);

        var payload = new EventoGeocercaOfflinePayload
        {
            EventoLocalId = evento.Id,
            HojaDeRutaId = evento.HojaDeRutaId,
            ParadaId = evento.ParadaId,
            EnvioId = evento.EnvioId,
            CodigoSeguimiento = evento.CodigoSeguimiento,
            TipoEvento = evento.TipoEvento,
            Latitud = evento.Latitud,
            Longitud = evento.Longitud,
            DistanciaMetros = evento.DistanciaMetros,
            TiempoPermanenciaSegundos = evento.TiempoPermanenciaSegundos,
            TimestampUtc = evento.TimestampUtc,
            IdempotencyKey = $"GEO-{evento.TipoEvento}-{evento.ParadaId:N}-{evento.TimestampUtc.Ticks}"
        };

        await EncolarOperacionAsync(TiposOperacionOffline.EventoGeocerca, JsonSerializer.Serialize(payload));
        return evento;
    }

    public async Task<List<EventoGeocercaLocal>> ObtenerEventosGeocercaAsync(int limite = 50)
    {
        await InitAsync();
        return await Db.Table<EventoGeocercaLocal>()
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limite)
            .ToListAsync();
    }

    public async Task MarcarEventoGeocercaSincronizadoAsync(int eventoLocalId)
    {
        await InitAsync();
        var item = await Db.FindAsync<EventoGeocercaLocal>(eventoLocalId);
        if (item != null)
        {
            item.Sincronizado = true;
            await Db.UpdateAsync(item);
        }
    }
}
