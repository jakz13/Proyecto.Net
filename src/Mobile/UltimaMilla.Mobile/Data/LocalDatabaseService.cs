using SQLite;
using UltimaMilla.Mobile.Models.Local;

namespace UltimaMilla.Mobile.Data;

public interface ILocalDatabaseService
{
    Task InitAsync();
    SQLiteAsyncConnection Connection { get; }
    Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson);
    Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync();
    Task MarcarComoSincronizadoAsync(int id);
    Task RegistrarErrorAsync(int id, string error);
}

public class LocalDatabaseService : ILocalDatabaseService
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public SQLiteAsyncConnection Connection => _database;

    public LocalDatabaseService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "ultimamilla_encrypted.db3");

        // RNF 6.11: Base de datos cifrada en reposo mediante SQLite / SQLCipher.
        // NOTA: Clave temporal hardcodeada solicitada. En etapas posteriores se resolverá de forma dinámica desde SecureStorage.
        const string encryptionPassword = "SuperSecretKey2026";

        var connectionString = new SQLiteConnectionString(
            dbPath,
            openFlags: SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex,
            storeDateTimeAsTicks: true,
            key: encryptionPassword);

        _database = new SQLiteAsyncConnection(connectionString);
    }

    /// <summary>
    /// Crea las tablas locales necesarias para la operativa offline si aún no existen.
    /// </summary>
    public async Task InitAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            // Creación de tablas de modelos offline
            await _database.CreateTableAsync<HojaDeRutaLocal>();
            await _database.CreateTableAsync<ParadaLocal>();
            await _database.CreateTableAsync<EnvioLocal>();
            await _database.CreateTableAsync<BultoLocal>();
            await _database.CreateTableAsync<MotivoNoEntregaLocal>();
            await _database.CreateTableAsync<OperacionOfflineLocal>();

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

        return await _database.InsertAsync(operacion);
    }

    public async Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync()
    {
        await InitAsync();
        return await _database.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .OrderBy(o => o.FechaCreacionUtc)
            .ToListAsync();
    }

    public async Task MarcarComoSincronizadoAsync(int id)
    {
        await InitAsync();
        var op = await _database.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Sincronizado = true;
            await _database.UpdateAsync(op);
        }
    }

    public async Task RegistrarErrorAsync(int id, string error)
    {
        await InitAsync();
        var op = await _database.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Reintentos++;
            op.UltimoError = error;
            await _database.UpdateAsync(op);
        }
    }
}
