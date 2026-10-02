using SQLite;
using UltimaMilla.Mobile.Models.Local;

namespace UltimaMilla.Mobile.Data;

public interface ILocalDatabaseService
{
    Task InitializeAsync();
    Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson);
    Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync();
    Task MarcarComoSincronizadoAsync(int id);
    Task RegistrarErrorAsync(int id, string error);
}

public class LocalDatabaseService : ILocalDatabaseService
{
    private SQLiteAsyncConnection? _database;
    private readonly string _dbPath;

    public LocalDatabaseService()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "ultimamilla_repartidor.db3");
    }

    public async Task InitializeAsync()
    {
        if (_database is not null)
            return;

        var options = new SQLiteConnectionString(_dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache, true);
        _database = new SQLiteAsyncConnection(options);

        await _database.CreateTableAsync<OperacionOfflineLocal>();
    }

    public async Task<int> EncolarOperacionAsync(string tipoOperacion, string payloadJson)
    {
        await InitializeAsync();
        var operacion = new OperacionOfflineLocal
        {
            TipoOperacion = tipoOperacion,
            PayloadJson = payloadJson,
            FechaCreacionUtc = DateTime.UtcNow,
            Sincronizado = false
        };

        return await _database!.InsertAsync(operacion);
    }

    public async Task<List<OperacionOfflineLocal>> ObtenerOperacionesPendientesAsync()
    {
        await InitializeAsync();
        return await _database!.Table<OperacionOfflineLocal>()
            .Where(o => !o.Sincronizado)
            .OrderBy(o => o.FechaCreacionUtc)
            .ToListAsync();
    }

    public async Task MarcarComoSincronizadoAsync(int id)
    {
        await InitializeAsync();
        var op = await _database!.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Sincronizado = true;
            await _database.UpdateAsync(op);
        }
    }

    public async Task RegistrarErrorAsync(int id, string error)
    {
        await InitializeAsync();
        var op = await _database!.FindAsync<OperacionOfflineLocal>(id);
        if (op != null)
        {
            op.Reintentos++;
            op.UltimoError = error;
            await _database.UpdateAsync(op);
        }
    }
}
