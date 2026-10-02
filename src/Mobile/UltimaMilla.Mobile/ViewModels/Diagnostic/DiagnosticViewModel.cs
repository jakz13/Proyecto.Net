using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Configuration;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Services.Api;
using UltimaMilla.Mobile.ViewModels.Base;

namespace UltimaMilla.Mobile.ViewModels.Diagnostic;

public partial class DiagnosticViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly ILocalDatabaseService _databaseService;

    [ObservableProperty]
    private string _apiUrl = ApiEndpoints.BaseUrl;

    [ObservableProperty]
    private string _status = "Desconectado";

    [ObservableProperty]
    private string _statusMessage = "Presiona el botón para probar la conexión con el Backend.";

    [ObservableProperty]
    private string _serverTimestamp = "-";

    [ObservableProperty]
    private string _serverEnvironment = "-";

    [ObservableProperty]
    private int _pendingOfflineOperationsCount = 0;

    [ObservableProperty]
    private bool _hasSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DiagnosticViewModel(IApiClient apiClient, ILocalDatabaseService databaseService)
    {
        _apiClient = apiClient;
        _databaseService = databaseService;
        Title = "Diagnóstico y Conectividad";
    }

    [RelayCommand]
    public async Task CheckConnectivityAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            HasSuccess = false;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Conectando con la API...";

            var result = await _apiClient.CheckConnectivityAsync();

            if (result != null)
            {
                Status = result.Status;
                StatusMessage = result.Message;
                ServerTimestamp = result.TimestampUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
                ServerEnvironment = $"{result.Environment} (v{result.Version})";
                HasSuccess = true;
            }
            else
            {
                Status = "Error";
                StatusMessage = "No se recibió respuesta válida del servidor.";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            Status = "Sin Conexión (Offline)";
            StatusMessage = "Fallo al conectar con el backend. Verifique que la API esté corriendo.";
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            IsBusy = false;
            await RefreshOfflineCountAsync();
        }
    }

    [RelayCommand]
    public async Task EnqueueTestOfflineOperationAsync()
    {
        try
        {
            await _databaseService.EncolarOperacionAsync(
                "TEST_WALKING_SKELETON",
                $"{{\"timestamp\":\"{DateTime.UtcNow:O}\",\"device\":\"MAUI_REPARTIDOR\"}}"
            );
            await RefreshOfflineCountAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error SQLite: {ex.Message}";
            HasError = true;
        }
    }

    [RelayCommand]
    public async Task RefreshOfflineCountAsync()
    {
        var pending = await _databaseService.ObtenerOperacionesPendientesAsync();
        PendingOfflineOperationsCount = pending.Count;
    }
}
