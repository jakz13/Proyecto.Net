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
    Task PurgarOperacionOfflineAsync(int id);
    Task<int> PurgarOperacionesPorRutaAsync(Guid hojaDeRutaId);
    Task MarcarRutasAusentesComoRendidasAsync(IEnumerable<Guid> idsRutasActivasServidor);
    Task<int> ObtenerCantidadPendientesAsync();

    // Persistencia y consulta de hoja de ruta local
    Task GuardarRutaDescargadaAsync(Guid hojaDeRutaId, List<ParadaNavegacionDto> paradas, List<MotivoNoEntregaLocal>? motivos = null, string? estadoRuta = null, string? numeroRuta = null, DateTime? fechaRuta = null, string? patenteVehiculo = null);
    Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync(Guid? hojaDeRutaId = null);
    Task<HojaDeRutaLocal?> ObtenerHojaDeRutaLocalPorIdAsync(Guid hojaDeRutaId);
    Task<List<ParadaLocal>> ObtenerParadasLocalesPorRutaAsync(Guid hojaDeRutaId);
    Task<string?> ObtenerFirmaContenidoRutaLocalAsync(Guid hojaDeRutaId);
    void GuardarFirmaContenidoRutaEnPreferencias(Guid hojaDeRutaId, string firma);
    string? ObtenerFirmaContenidoRutaDePreferencias(Guid hojaDeRutaId);
    Task<List<HojaDeRutaLocal>> ListarHojasDeRutaLocalesAsync();
    Task<List<HojaDeRutaSelectorItem>> ListarHojasDeRutaParaSelectorAsync(Guid? seleccionadaId = null);
    Task FinalizarHojaDeRutaLocalAsync(Guid hojaDeRutaId);
    Task ActualizarEstadoParadaLocalAsync(Guid paradaId, string nuevoEstado);
    Task RevertirEstadoParadaLocalAsync(Guid paradaId, string estadoPrevio = "Pendiente");
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

    // Envíos admitidos para recepción física en depósito (offline-first)
    Task GuardarEnviosAdmitidosDepositoAsync(IEnumerable<EnvioAdmitidoDepositoLocal> envios);
    Task<List<EnvioAdmitidoDepositoLocal>> ObtenerEnviosAdmitidosDepositoAsync();
    Task MarcarEnvioAdmitidoRecepcionadoLocalAsync(Guid envioId);
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
    /// Obtiene la clave criptográfica desde SecureStorage del dispositivo.
    /// Si aún no existe, genera una clave aleatoria de 32 bytes (256 bits), la almacena de forma segura y la retorna.
    /// Si SecureStorage falla en Android (por ejemplo tras reinstalación o inconsistencia transitoria del Keystore),
    /// intenta limpiar y reintentar, o aplica un fallback resiliente con clave de 256 bits generada por el dispositivo
    /// para asegurar que SQLCipher mantenga siempre el cifrado AES-256 sin que la aplicación se cierre abruptamente.
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
            System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] Error al acceder a SecureStorage: {ex.Message}");

            // Intentar reintento tras purgar posible entrada corrupta previa en el Keystore
            try
            {
                SecureStorage.Default.Remove(KeyStorageAlias);
                var retryBytes = RandomNumberGenerator.GetBytes(32);
                var retryKey = Convert.ToBase64String(retryBytes);
                await SecureStorage.Default.SetAsync(KeyStorageAlias, retryKey);
                return retryKey;
            }
            catch (Exception exRetry)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalDatabaseService] Reintento en SecureStorage no disponible: {exRetry.Message}");
            }

            // Fallback resiliente a Preferences con clave de 256 bits única y criptográficamente segura
            const string ClaveSeguraPreferencia = "sqlite_db_safe_entropy_key";
            var claveSegura = Preferences.Default.Get<string?>(ClaveSeguraPreferencia, null);
            if (string.IsNullOrWhiteSpace(claveSegura))
            {
                claveSegura = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                Preferences.Default.Set(ClaveSeguraPreferencia, claveSegura);
            }
            return claveSegura;
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
        await database.CreateTableAsync<EnvioAdmitidoDepositoLocal>();
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
            Sincronizado = false,
            CorrelationId = Guid.NewGuid().ToString("N")
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

    public async Task PurgarOperacionOfflineAsync(int id)
    {
        await InitAsync();
        var op = await Db.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            await Db.DeleteAsync(op);
        }
    }

    public async Task<int> PurgarOperacionesPorRutaAsync(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return 0;

        await InitAsync();
        var idConGuiones = hojaDeRutaId.ToString("D");
        var idSinGuiones = hojaDeRutaId.ToString("N");
        var pendientes = await Db.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .ToListAsync();

        var aEliminar = pendientes
            .Where(o => !string.IsNullOrWhiteSpace(o.PayloadJson) &&
                        (o.PayloadJson.Contains(idConGuiones, StringComparison.OrdinalIgnoreCase) ||
                         o.PayloadJson.Contains(idSinGuiones, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var op in aEliminar)
        {
            await Db.DeleteAsync(op);
        }

        return aEliminar.Count;
    }

    public async Task MarcarRutasAusentesComoRendidasAsync(IEnumerable<Guid> idsRutasActivasServidor)
    {
        await InitAsync();
        var activas = idsRutasActivasServidor.Where(id => id != Guid.Empty).ToHashSet();
        var rutasLocales = await Db.Table<HojaDeRutaLocal>().ToListAsync();
        var ausentes = rutasLocales
            .Where(r => !activas.Contains(r.Id) && r.Estado is not ("Rendida" or "Cerrada"))
            .ToList();

        if (ausentes.Count == 0)
            return;

        var pendientes = await Db.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .ToListAsync();

        foreach (var ruta in ausentes)
        {
            var idConGuiones = ruta.Id.ToString("D");
            var idSinGuiones = ruta.Id.ToString("N");
            var opsDeRuta = pendientes
                .Where(o => !string.IsNullOrWhiteSpace(o.PayloadJson) &&
                            (o.PayloadJson.Contains(idConGuiones, StringComparison.OrdinalIgnoreCase) ||
                             o.PayloadJson.Contains(idSinGuiones, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Si quedan entregas/incidencias/retiros pendientes de sincronizar, no cerrar localmente
            // hasta que la cola termine de procesarse contra el backend.
            bool tieneOperacionesCriticasPendientes = opsDeRuta.Any(o =>
                o.TipoOperacion is TiposOperacionOffline.EntregaExitosa
                    or TiposOperacionOffline.EntregaParcial
                    or TiposOperacionOffline.FalloEntrega
                    or TiposOperacionOffline.RetiroRealizado
                    or TiposOperacionOffline.RetiroNoRealizado
                    or TiposOperacionOffline.RetiroDevolucion
                    or TiposOperacionOffline.ConfirmarCarga
                    or TiposOperacionOffline.FinalizarHojaDeRuta);

            if (tieneOperacionesCriticasPendientes)
                continue;

            ruta.Estado = "Rendida";
            await Db.UpdateAsync(ruta);

            foreach (var opResidual in opsDeRuta)
            {
                await Db.DeleteAsync(opResidual);
            }
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
        DateTime? fechaRuta = null,
        string? patenteVehiculo = null)
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
                    PatenteVehiculo = string.IsNullOrWhiteSpace(patenteVehiculo) ? null : patenteVehiculo.Trim(),
                    FechaDescargaUtc = DateTime.UtcNow
                };
                conn.Insert(ruta);
            }
            else
            {
                // "Cerrada" es el cierre que hizo el repartidor en el dispositivo: el servidor solo dice "Finalizada" (todas las paradas
                // completadas) o "Rendida", y eso no debe reabrir una ruta que él ya cerró.
                var yaCerradaPorElRepartidor = ruta.Estado == "Cerrada" && estadoRuta is "Finalizada" or "Rendida";
                var yaEnTransitoPorElRepartidor = ruta.Estado == "EnTransito" && estadoRuta is "Liberada" or "EnCarga" or "Despachada";
                if (!string.IsNullOrWhiteSpace(estadoRuta) && !yaCerradaPorElRepartidor && !yaEnTransitoPorElRepartidor)
                    ruta.Estado = estadoRuta;
                if (!string.IsNullOrWhiteSpace(numeroRuta))
                    ruta.NumeroRuta = numeroRuta;
                if (fechaRuta.HasValue)
                    ruta.Fecha = fechaRuta.Value;
                if (!string.IsNullOrWhiteSpace(patenteVehiculo))
                    ruta.PatenteVehiculo = patenteVehiculo.Trim();
                ruta.TotalParadas = paradas.Count;
                ruta.FechaDescargaUtc = DateTime.UtcNow;
                conn.Update(ruta);
            }

            if (ruta.Estado is "Rendida" or "Cerrada")
            {
                var idConGuiones = hojaDeRutaId.ToString("D");
                var idSinGuiones = hojaDeRutaId.ToString("N");
                var obsoletasOps = conn.Table<OperacionOfflineLocal>()
                    .Where(o => !o.Sincronizado)
                    .ToList()
                    .Where(o => !string.IsNullOrWhiteSpace(o.PayloadJson) &&
                                (o.PayloadJson.Contains(idConGuiones, StringComparison.OrdinalIgnoreCase) ||
                                 o.PayloadJson.Contains(idSinGuiones, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                foreach (var op in obsoletasOps)
                {
                    conn.Delete(op);
                }
            }

            bool rutaYaDespachada = ruta.Estado is "Despachada" or "EnTransito" or "Finalizada" or "Cerrada" or "Rendida";
            bool ordenOptimizadoLocalmente = Preferences.Default.Get($"RutaOrdenOptimizadoLocal_{hojaDeRutaId:N}", false);

            // 2. Paradas que el servidor ya no incluye (reasignadas o retiradas de la ruta) y que no tienen nada pendiente de enviar
            var operacionesPendientesCola = conn.Table<OperacionOfflineLocal>()
                .Where(o => !o.Sincronizado)
                .ToList();

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
                var esRetiro = string.Equals(p.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase)
                    || (p.RetiroId.HasValue && p.RetiroId.Value != Guid.Empty);
                // Una parada de retiro no tiene envío: se identifica por el retiro
                var envioId = esRetiro ? (p.RetiroId ?? p.ParadaId) : p.EnvioId;

                var paradaLocal = conn.Find<ParadaLocal>(paradaId);
                bool isNew = paradaLocal == null;
                paradaLocal ??= new ParadaLocal { Id = paradaId };

                paradaLocal.HojaDeRutaId = hojaDeRutaId;
                paradaLocal.EnvioId = envioId;
                paradaLocal.Tipo = esRetiro ? "Retiro" : "Entrega";
                paradaLocal.RetiroId = esRetiro ? p.RetiroId : null;
                if (isNew || !ordenOptimizadoLocalmente || paradaLocal.Orden <= 0)
                    paradaLocal.Orden = p.Orden;
                paradaLocal.OrdenVisita = isNew || paradaLocal.OrdenVisita <= 0 ? p.Orden : paradaLocal.OrdenVisita;

                var paradaIdConGuiones = paradaId.ToString("D");
                var paradaIdSinGuiones = paradaId.ToString("N");
                bool tieneOperacionPendienteLocal = operacionesPendientesCola.Any(o =>
                    !string.IsNullOrWhiteSpace(o.PayloadJson) &&
                    (o.PayloadJson.Contains(paradaIdConGuiones, StringComparison.OrdinalIgnoreCase) ||
                     o.PayloadJson.Contains(paradaIdSinGuiones, StringComparison.OrdinalIgnoreCase)));

                // Reconciliación autoritativa: si no existe ninguna operación pendiente en OperacionOfflineLocal para esta parada,
                // se sobrescribe siempre con el estado real del servidor (destrabando cualquier estado optimista huérfano).
                if (isNew || !tieneOperacionPendienteLocal || paradaLocal.Estado is not ("Entregado" or "EntregadaParcialmente" or "Fallido" or "EnDevolucion" or "Cancelada"))
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
                var faltantesDesdeServidor = p.ResumenBultos?.CantidadBultosFaltantesEnCarga ?? 0;
                paradaLocal.CantidadBultosFaltantesEnCarga = tieneOperacionPendienteLocal
                    ? Math.Max(faltantesDesdeServidor, paradaLocal.CantidadBultosFaltantesEnCarga)
                    : faltantesDesdeServidor;
                paradaLocal.EsParadaActual = p.EsParadaActual;

                if (isNew)
                    conn.Insert(paradaLocal);
                else
                    conn.Update(paradaLocal);

                bool bultoYaCargado = rutaYaDespachada || paradaLocal.Estado is "Entregado" or "EntregadaParcialmente" or "Fallido" or "EnDevolucion";

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

                // Bultos: los que manda el servidor; soporta múltiples bultos con igual código en un mismo envío
                var codigos = (p.ResumenBultos?.CodigosBultos ?? new List<string>())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToList();

                if (codigos.Count == 0 && (p.ResumenBultos?.CantidadBultos ?? 0) > 0)
                {
                    var baseCod = !string.IsNullOrWhiteSpace(paradaLocal.CodigoSeguimiento)
                        ? paradaLocal.CodigoSeguimiento.Trim()
                        : $"PAR-{p.Orden:D2}";
                    for (int idx = 1; idx <= p.ResumenBultos!.CantidadBultos; idx++)
                    {
                        codigos.Add($"{baseCod}-B{idx:00}");
                    }
                }

                var existentes = conn.Table<BultoLocal>().Where(b => b.ParadaId == paradaId).ToList();
                var disponibles = existentes
                    .OrderByDescending(b => b.VerificadoEnCarga)
                    .ThenByDescending(b => b.VerificadoEnEntrega)
                    .ToList();

                var pesoPorBulto = codigos.Count > 0 && p.ResumenBultos is { PesoTotalKg: > 0 }
                    ? p.ResumenBultos.PesoTotalKg / codigos.Count
                    : 0m;

                foreach (var codBulto in codigos)
                {
                    var bultoExistente = disponibles.FirstOrDefault(b =>
                        string.Equals(b.Codigo?.Trim(), codBulto, StringComparison.OrdinalIgnoreCase));

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
                        disponibles.Remove(bultoExistente);
                        bultoExistente.EnvioId = envioId;
                        bultoExistente.ParadaId = paradaId;
                        bultoExistente.Codigo = codBulto;
                        bultoExistente.Identificacion = codBulto;
                        if (pesoPorBulto > 0)
                            bultoExistente.Peso = pesoPorBulto;

                        // Si existe una confirmación de carga local pendiente de sincronizar y este bulto se marcó faltante en carga,
                        // no sobrescribirlo como verificado en carga ni borrarlo hasta que el servidor procese la cola.
                        if (!(tieneOperacionPendienteLocal && bultoExistente.FaltanteEnCarga))
                        {
                            bultoExistente.FaltanteEnCarga = false;
                            if (bultoYaCargado)
                                bultoExistente.VerificadoEnCarga = true;
                        }
                        if (!tieneOperacionPendienteLocal)
                            bultoExistente.VerificadoEnEntrega = paradaLocal.Estado == "Entregado";
                        conn.Update(bultoExistente);
                    }
                }

                foreach (var sobrante in disponibles.Where(b => !b.VerificadoEnEntrega && (!b.VerificadoEnCarga || b.FaltanteEnCarga)))
                    conn.Delete(sobrante);
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
            "Cerrada" or "Rendida" => 10,
            _ => 10
        };

        return rutas
            .OrderBy(r => PrioridadEstado(r.Estado))
            .ThenByDescending(r => r.FechaDescargaUtc)
            .ToList();
    }

    public async Task<List<HojaDeRutaSelectorItem>> ListarHojasDeRutaParaSelectorAsync(Guid? seleccionadaId = null)
    {
        var rutasLocales = await ListarHojasDeRutaLocalesAsync();
        var resultado = new List<HojaDeRutaSelectorItem>(rutasLocales.Count);

        foreach (var ruta in rutasLocales)
        {
            var paradas = await ObtenerParadasLocalesPorRutaAsync(ruta.Id);
            resultado.Add(HojaDeRutaSelectorItem.Crear(
                ruta,
                paradas,
                esSeleccionada: seleccionadaId.HasValue && ruta.Id == seleccionadaId.Value));
        }

        return resultado;
    }

    public async Task<HojaDeRutaLocal?> ObtenerHojaDeRutaLocalPorIdAsync(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return null;

        await InitAsync();
        return await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId);
    }

    public async Task<List<ParadaLocal>> ObtenerParadasLocalesPorRutaAsync(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return new List<ParadaLocal>();

        await InitAsync();
        return await Db.Table<ParadaLocal>()
            .Where(p => p.HojaDeRutaId == hojaDeRutaId)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Id)
            .ToListAsync();
    }

    public async Task<string?> ObtenerFirmaContenidoRutaLocalAsync(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return null;

        var ruta = await ObtenerHojaDeRutaLocalPorIdAsync(hojaDeRutaId);
        if (ruta is null)
            return null;

        var paradas = await ObtenerParadasLocalesPorRutaAsync(hojaDeRutaId);
        return CalcularFirmaContenidoRutaLocal(ruta, paradas);
    }

    public void GuardarFirmaContenidoRutaEnPreferencias(Guid hojaDeRutaId, string firma)
    {
        if (hojaDeRutaId == Guid.Empty || string.IsNullOrWhiteSpace(firma))
            return;

        try
        {
            Preferences.Default.Set($"FirmaRuta_{hojaDeRutaId:N}", firma);
        }
        catch
        {
        }
    }

    public string? ObtenerFirmaContenidoRutaDePreferencias(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return null;

        try
        {
            var valor = Preferences.Default.Get($"FirmaRuta_{hojaDeRutaId:N}", string.Empty);
            return string.IsNullOrWhiteSpace(valor) ? null : valor;
        }
        catch
        {
            return null;
        }
    }

    private async Task ActualizarFirmaRutaLocalEnPreferenciasAsync(Guid hojaDeRutaId)
    {
        if (hojaDeRutaId == Guid.Empty)
            return;

        var firma = await ObtenerFirmaContenidoRutaLocalAsync(hojaDeRutaId);
        if (!string.IsNullOrWhiteSpace(firma))
        {
            GuardarFirmaContenidoRutaEnPreferencias(hojaDeRutaId, firma);
        }
    }

    public static string NormalizarEstadoParadaFirma(string? estado) =>
        MapearEstadoParada(estado ?? string.Empty);

    public static string CalcularFirmaContenidoRuta(
        Guid hojaDeRutaId,
        string? estadoRuta,
        IEnumerable<(Guid ParadaId, int Orden, string? Estado, string? Direccion)> paradas)
    {
        var listaOrdenada = paradas
            .Where(p => p.ParadaId != Guid.Empty)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.ParadaId)
            .Select(p =>
            {
                var estadoNorm = NormalizarEstadoParadaFirma(p.Estado);
                var direccionNorm = (p.Direccion ?? string.Empty).Trim().ToUpperInvariant();
                return $"{p.ParadaId:N}:{p.Orden}:{estadoNorm}:{direccionNorm}";
            })
            .ToList();

        var estadoRutaNorm = (estadoRuta ?? string.Empty).Trim().ToUpperInvariant();
        return $"{hojaDeRutaId:N}|{estadoRutaNorm}|{listaOrdenada.Count}|{string.Join("|", listaOrdenada)}";
    }

    public static string CalcularFirmaContenidoRutaLocal(
        HojaDeRutaLocal ruta,
        IEnumerable<ParadaLocal> paradas) =>
        CalcularFirmaContenidoRuta(
            ruta.Id,
            ruta.Estado,
            paradas.Select(p => (p.Id, p.Orden, (string?)p.Estado, (string?)p.Direccion)));

    public static string CalcularFirmaContenidoRutaServidor(
        Guid hojaDeRutaId,
        string? estadoRuta,
        IEnumerable<ParadaNavegacionDto> paradas) =>
        CalcularFirmaContenidoRuta(
            hojaDeRutaId,
            estadoRuta,
            paradas.Select(p => (p.ParadaId, p.Orden, (string?)p.EstadoParada, (string?)p.DireccionFormateada)));

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
            // Cierre explícito del repartidor (botón "Finalizar Hoja de Ruta"): recién entonces la ruta sale de la pantalla del día
            ruta.Estado = "Cerrada";
            await Db.UpdateAsync(ruta);
            await ActualizarFirmaRutaLocalEnPreferenciasAsync(ruta.Id);
        }
    }

    public async Task<RutaActivaLocalDto?> ObtenerRutaActivaLocalAsync(Guid? hojaDeRutaId = null)
    {
        await InitAsync();

        HojaDeRutaLocal? ruta = null;

        if (hojaDeRutaId.HasValue && hojaDeRutaId.Value != Guid.Empty)
        {
            ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId.Value);
            if (ruta != null && ruta.Estado is "Rendida" or "Cerrada")
            {
                ruta = null;
            }
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
                ?? todas.FirstOrDefault(r => r.Estado == "Liberada")
                // Con todas las paradas completadas el servidor la marca "Finalizada": sigue visible hasta que el repartidor la cierre
                ?? todas.FirstOrDefault(r => r.Estado == "Finalizada");
        }

        if (ruta == null)
            return null;

        bool rutaYaDespachada = ruta.Estado is "Despachada" or "EnTransito" or "Finalizada" or "Cerrada";
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

            bool esRetiro = string.Equals(p.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase)
                || (p.RetiroId.HasValue && p.RetiroId.Value != Guid.Empty);

            bool bultoDebeEstarCargado = !esRetiro && (rutaYaDespachada || p.Estado is "Entregado" or "EntregadaParcialmente" or "Fallido" or "EnDevolucion");

            foreach (var b in bultos)
            {
                if (bultoDebeEstarCargado && !b.VerificadoEnCarga && !b.FaltanteEnCarga)
                {
                    b.VerificadoEnCarga = true;
                    await Db.UpdateAsync(b);
                }
            }

            var bultosVisibles = (!esRetiro && !enPrecarga)
                ? bultos.Where(b => !b.FaltanteEnCarga).ToList()
                : bultos;

            var bultoItems = bultosVisibles.Select(b => new BultoItemViewModel
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

            var faltantesCargaParada = p.CantidadBultosFaltantesEnCarga > 0
                ? p.CantidadBultosFaltantesEnCarga
                : bultos.Count(b => b.FaltanteEnCarga);

            resultParadas.Add(new ParadaItemViewModel
            {
                Id = p.Id,
                HojaDeRutaId = ruta.Id,
                EnvioId = p.EnvioId,
                Tipo = esRetiro ? "Retiro" : (string.IsNullOrWhiteSpace(p.Tipo) ? "Entrega" : p.Tipo),
                RetiroId = p.RetiroId,
                Orden = p.Orden,
                Direccion = p.Direccion,
                Ciudad = p.Ciudad,
                Destinatario = p.DestinatarioNombre,
                Telefono = p.DestinatarioTelefono ?? string.Empty,
                CantidadBultos = bultoItems.Count > 0 ? bultoItems.Count : p.CantidadBultos,
                CantidadBultosFaltantesEnCarga = faltantesCargaParada,
                Estado = p.Estado,
                EnPrecarga = !esRetiro && enPrecarga,
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

            // Al confirmarse la entrega o retiro, se consideran implícitamente verificados todos los bultos asociados
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

            bool esRetiroParada = string.Equals(parada.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase)
                || (parada.RetiroId.HasValue && parada.RetiroId.Value != Guid.Empty);
            if (esRetiroParada && parada.HojaDeRutaId != Guid.Empty)
            {
                var rutaLocal = await Db.FindAsync<HojaDeRutaLocal>(parada.HojaDeRutaId);
                if (rutaLocal != null && rutaLocal.Estado is "Liberada" or "EnCarga" or "Despachada")
                {
                    rutaLocal.Estado = "EnTransito";
                    await Db.UpdateAsync(rutaLocal);
                }
            }

            await ActualizarFirmaRutaLocalEnPreferenciasAsync(parada.HojaDeRutaId);
        }
    }

    public async Task RevertirEstadoParadaLocalAsync(Guid paradaId, string estadoPrevio = "Pendiente")
    {
        await InitAsync();
        var parada = await Db.FindAsync<ParadaLocal>(paradaId);
        if (parada == null)
            return;

        parada.Estado = MapearEstadoParada(estadoPrevio);
        await Db.UpdateAsync(parada);

        var envio = await Db.Table<EnvioLocal>()
            .Where(e => e.ParadaId == paradaId)
            .FirstOrDefaultAsync();
        if (envio != null)
        {
            envio.Estado = parada.Estado == "Pendiente" ? "EnTransito" : parada.Estado;
            await Db.UpdateAsync(envio);
        }

        if (parada.Estado == "Pendiente")
        {
            var bultos = await Db.Table<BultoLocal>()
                .Where(b => b.ParadaId == paradaId)
                .ToListAsync();
            foreach (var b in bultos)
            {
                if (b.VerificadoEnEntrega)
                {
                    b.VerificadoEnEntrega = false;
                    await Db.UpdateAsync(b);
                }
            }
        }

        await ActualizarFirmaRutaLocalEnPreferenciasAsync(parada.HojaDeRutaId);
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

        parada.Estado = "EntregadaParcialmente";
        await Db.UpdateAsync(parada);

        foreach (var bulto in await Db.Table<BultoLocal>().Where(b => b.ParadaId == paradaId).ToListAsync())
        {
            if (bulto.FaltanteEnCarga)
                continue;

            bulto.VerificadoEnEntrega = entregados.Contains(bulto.Codigo.Trim());
            await Db.UpdateAsync(bulto);
        }

        await ActualizarFirmaRutaLocalEnPreferenciasAsync(parada.HojaDeRutaId);
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
            await ActualizarFirmaRutaLocalEnPreferenciasAsync(ruta.Id);
        }
    }

    public async Task MarcarBultoVerificadoCargaAsync(Guid bultoId, bool verificado)
    {
        await InitAsync();
        if (bultoId == Guid.Empty)
            return;

        var bulto = await Db.FindAsync<BultoLocal>(bultoId);
        if (bulto != null)
        {
            bulto.VerificadoEnCarga = verificado;
            bulto.Cargado = verificado;
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

        var codigosRestantes = (codigosCargados ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToList();

        var ruta = await Db.FindAsync<HojaDeRutaLocal>(hojaDeRutaId);
        if (ruta == null)
            return;

        var paradas = await Db.Table<ParadaLocal>().Where(p => p.HojaDeRutaId == ruta.Id).ToListAsync();
        var estadoBultosPorParada = new List<(ParadaLocal Parada, List<BultoLocal> Bultos, int CargadosCount, int FaltantesCount)>();

        foreach (var parada in paradas)
        {
            var esRetiro = string.Equals(parada.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase)
                || (parada.RetiroId.HasValue && parada.RetiroId.Value != Guid.Empty);

            var bultosParada = await Db.Table<BultoLocal>()
                .Where(b => b.ParadaId == parada.Id)
                .OrderByDescending(b => b.VerificadoEnCarga)
                .ToListAsync();

            var cargadosCount = 0;
            var faltantesCount = 0;

            if (!esRetiro)
            {
                foreach (var b in bultosParada)
                {
                    var idx = codigosRestantes.FindIndex(c =>
                        string.Equals(c, b.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (idx >= 0)
                    {
                        codigosRestantes.RemoveAt(idx);
                        b.VerificadoEnCarga = true;
                        b.Cargado = true;
                        b.FaltanteEnCarga = false;
                        await Db.UpdateAsync(b);
                        cargadosCount++;
                    }
                    else
                    {
                        b.VerificadoEnCarga = false;
                        b.Cargado = false;
                        b.FaltanteEnCarga = true;
                        await Db.UpdateAsync(b);
                        faltantesCount++;
                    }
                }
            }

            estadoBultosPorParada.Add((parada, bultosParada, cargadosCount, faltantesCount));
        }

        var hayAlMenosUnaParadaViajante = estadoBultosPorParada.Count == 0
            || estadoBultosPorParada.Any(x =>
                x.Parada.Estado != "Cancelada" &&
                (string.Equals(x.Parada.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase) || x.CargadosCount > 0));

        if (hayAlMenosUnaParadaViajante)
        {
            foreach (var (parada, bultosParada, cargadosCount, faltantesCount) in estadoBultosPorParada)
            {
                if (parada.Estado == "Cancelada")
                    continue;

                var esRetiro = string.Equals(parada.Tipo, "Retiro", StringComparison.OrdinalIgnoreCase);
                if (!esRetiro && bultosParada.Count > 0 && cargadosCount == 0)
                {
                    parada.Estado = "Cancelada";
                    await Db.UpdateAsync(parada);

                    if (parada.EnvioId != Guid.Empty)
                    {
                        var envioIncompleto = await Db.FindAsync<EnvioLocal>(parada.EnvioId);
                        if (envioIncompleto != null)
                        {
                            envioIncompleto.Estado = "EnDeposito";
                            await Db.UpdateAsync(envioIncompleto);
                        }
                    }
                }
                else if (!esRetiro && cargadosCount > 0)
                {
                    parada.CantidadBultos = cargadosCount;
                    parada.CantidadBultosFaltantesEnCarga = faltantesCount;
                    await Db.UpdateAsync(parada);

                    if (parada.EnvioId != Guid.Empty)
                    {
                        var envioParcial = await Db.FindAsync<EnvioLocal>(parada.EnvioId);
                        if (envioParcial != null && envioParcial.Estado is "AsignadoARuta" or "EnDeposito" or "Pendiente")
                        {
                            envioParcial.Estado = "EnTransito";
                            await Db.UpdateAsync(envioParcial);
                        }
                    }
                }
            }

            if (paradas.Count > 0)
            {
                var activas = paradas.Where(p => p.Estado != "Cancelada").ToList();
                ruta.TotalParadas = activas.Count;
            }

            ruta.Estado = "EnTransito";
            await Db.UpdateAsync(ruta);
        }
        else if (ruta.Estado is "Liberada" or "Borrador")
        {
            ruta.Estado = "EnCarga";
            await Db.UpdateAsync(ruta);
        }

        await ActualizarFirmaRutaLocalEnPreferenciasAsync(ruta.Id);
    }

    public async Task ActualizarSecuenciaParadasAsync(IReadOnlyList<ParadaItemViewModel> paradasOrdenadas)
    {
        if (paradasOrdenadas == null || paradasOrdenadas.Count == 0)
            return;

        await InitAsync();

        Guid hojaDeRutaId = Guid.Empty;

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
                    if (hojaDeRutaId == Guid.Empty)
                        hojaDeRutaId = paradaLocal.HojaDeRutaId;

                    paradaLocal.Orden = nuevoOrden;
                    paradaLocal.OrdenVisita = nuevoOrden;
                    paradaLocal.HoraEstimada = $"{8 + nuevoOrden:D2}:30";
                    paradaLocal.EsParadaActual = nuevoOrden == 1;
                    conn.Update(paradaLocal);
                }
            }
        });

        if (hojaDeRutaId != Guid.Empty)
        {
            try
            {
                Preferences.Default.Set($"RutaOrdenOptimizadoLocal_{hojaDeRutaId:N}", true);
            }
            catch
            {
            }

            await ActualizarFirmaRutaLocalEnPreferenciasAsync(hojaDeRutaId);
        }
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
            "Completada" or "Entregada" or "Entregado" or "Retirada" or "Retirado" => "Entregado",
            "EntregadaParcialmente" or "EntregaParcial" => "EntregadaParcialmente",
            "Fallida" or "Fallido" or "NoEntregado" or "NoRetirada" or "NoRetirado" => "Fallido",
            "Reprogramado" => "Fallido",
            "EnDevolucion" or "EnRetornoADeposito" => "EnDevolucion",
            "Cancelada" or "Cancelado" or "Reasignado" => "Cancelada",
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

    public async Task GuardarEnviosAdmitidosDepositoAsync(IEnumerable<EnvioAdmitidoDepositoLocal> envios)
    {
        await InitAsync();
        foreach (var envio in envios)
        {
            var existente = await Db.FindAsync<EnvioAdmitidoDepositoLocal>(envio.EnvioId);
            if (existente == null)
            {
                await Db.InsertAsync(envio);
            }
            else
            {
                existente.CodigoSeguimiento = envio.CodigoSeguimiento;
                existente.NombreDestinatario = envio.NombreDestinatario;
                existente.DireccionDestino = envio.DireccionDestino;
                existente.TelefonoDestinatario = envio.TelefonoDestinatario;
                existente.ModalidadServicio = envio.ModalidadServicio;
                existente.PesoTotal = envio.PesoTotal;
                existente.CantidadBultos = envio.CantidadBultos;
                existente.BultosJson = envio.BultosJson;
                await Db.UpdateAsync(existente);
            }
        }
    }

    public async Task<List<EnvioAdmitidoDepositoLocal>> ObtenerEnviosAdmitidosDepositoAsync()
    {
        await InitAsync();
        return await Db.Table<EnvioAdmitidoDepositoLocal>()
            .Where(e => !e.RecepcionadoLocal)
            .OrderByDescending(e => e.FechaAlta)
            .ToListAsync();
    }

    public async Task MarcarEnvioAdmitidoRecepcionadoLocalAsync(Guid envioId)
    {
        await InitAsync();
        var envio = await Db.FindAsync<EnvioAdmitidoDepositoLocal>(envioId);
        if (envio != null)
        {
            envio.RecepcionadoLocal = true;
            await Db.UpdateAsync(envio);
        }
    }
}
