using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;
using UltimaMilla.Mobile.Views.Carga;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

/// <summary>Un bulto que hay que retirar y si ya se escaneó.</summary>
public partial class BultoRetiroItemViewModel : ObservableObject
{
    public string Codigo { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    private bool _escaneado;

    public string EstadoTexto => Escaneado ? "Escaneado" : "Pendiente de escaneo";
}

/// <summary>
/// Retiro en domicilio: el repartidor retira en el domicilio del comprador uno o más bultos para llevarlos al comercio.
/// Se registra con la identificación de cada bulto (escaneo), la fotografía de su estado y la firma de quien los entrega, también sin conexión.
/// </summary>
[QueryProperty(nameof(Parada), "Parada")]
public partial class RegistroRetiroViewModel : EvidenciaViewModelBase
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly List<MotivoNoEntregaLocal> _motivosCatalogo = new();

    [ObservableProperty]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    private string _nombreEntrega = string.Empty;

    [ObservableProperty]
    private string _documentoEntrega = string.Empty;

    [ObservableProperty]
    private string _codigoManual = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    [NotifyPropertyChangedFor(nameof(TodosEscaneados))]
    private int _bultosEscaneados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoMostrarNoRealizado))]
    private bool _mostrarNoRealizado;

    [ObservableProperty]
    private string _motivoSeleccionado = string.Empty;

    [ObservableProperty]
    private string _observacionesNoRealizado = string.Empty;

    public bool NoMostrarNoRealizado => !MostrarNoRealizado;

    public ObservableCollection<BultoRetiroItemViewModel> Bultos { get; } = new();

    public ObservableCollection<string> MotivosNoRetiro { get; } = new();

    public bool TodosEscaneados => Bultos.Count > 0 && BultosEscaneados == Bultos.Count;

    public string ResumenBultosTexto => Bultos.Count == 0
        ? "Este retiro no tiene bultos informados."
        : $"{BultosEscaneados} de {Bultos.Count} bulto(s) escaneados";

    public RegistroRetiroViewModel() : this(null, null)
    {
    }

    public RegistroRetiroViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
    }

    partial void OnParadaChanged(ParadaItemViewModel? value)
    {
        Bultos.Clear();
        BultosEscaneados = 0;
        if (value is null)
            return;

        // Quien entrega los bultos es el contacto del domicilio que informó el comercio
        if (!string.IsNullOrWhiteSpace(value.Destinatario))
            NombreEntrega = value.Destinatario.Trim();

        foreach (var bulto in value.Bultos)
        {
            Bultos.Add(new BultoRetiroItemViewModel { Codigo = bulto.Codigo });
        }

        OnPropertyChanged(nameof(ResumenBultosTexto));
        OnPropertyChanged(nameof(TodosEscaneados));
        _ = CargarMotivosAsync();
    }

    private async Task CargarMotivosAsync()
    {
        if (_localDatabaseService is null)
            return;

        try
        {
            _motivosCatalogo.Clear();
            MotivosNoRetiro.Clear();
            foreach (var motivo in await _localDatabaseService.ObtenerMotivosNoEntregaAsync())
            {
                _motivosCatalogo.Add(motivo);
                MotivosNoRetiro.Add(motivo.Descripcion);
            }

            if (string.IsNullOrEmpty(MotivoSeleccionado) && MotivosNoRetiro.Count > 0)
                MotivoSeleccionado = MotivosNoRetiro[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroRetiroViewModel] No se pudo leer el catálogo de motivos: {ex.Message}");
        }
    }

    private async Task ProcesarCodigoAsync(string codigo)
    {
        var normalizado = codigo.Trim();
        var bulto = Bultos.FirstOrDefault(b => b.Codigo.Equals(normalizado, StringComparison.OrdinalIgnoreCase));
        if (bulto is null)
        {
            await AvisarAsync("Bulto no encontrado", $"El bulto {normalizado.ToUpperInvariant()} no corresponde a este retiro.");
            return;
        }

        if (bulto.Escaneado)
        {
            await AvisarAsync("Bulto ya escaneado", $"El bulto {bulto.Codigo} ya fue escaneado.");
            return;
        }

        bulto.Escaneado = true;
        BultosEscaneados = Bultos.Count(b => b.Escaneado);
    }

    [RelayCommand]
    private async Task EscanearConCamaraAsync()
    {
        if (Shell.Current is null)
            return;

        try
        {
            var permiso = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permiso != PermissionStatus.Granted)
                permiso = await Permissions.RequestAsync<Permissions.Camera>();

            if (permiso != PermissionStatus.Granted)
            {
                await AvisarAsync("Permiso requerido", "Permiso de cámara denegado. Habilítelo en la configuración o ingrese el código manualmente.");
                return;
            }

            var escaner = new EscanerCodigoPage();
            await Shell.Current.Navigation.PushModalAsync(escaner);
            var codigo = await escaner.ResultadoEscaneoTask;
            if (!string.IsNullOrWhiteSpace(codigo))
                await ProcesarCodigoAsync(codigo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroRetiroViewModel] Error al escanear: {ex.Message}");
            await AvisarAsync("Cámara no disponible", "No fue posible abrir el lector. Ingrese el código manualmente.");
        }
    }

    [RelayCommand]
    private async Task ValidarCodigoManualAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoManual))
            return;

        var codigo = CodigoManual;
        CodigoManual = string.Empty;
        await ProcesarCodigoAsync(codigo);
    }

    [RelayCommand]
    private void AlternarNoRealizado() => MostrarNoRealizado = !MostrarNoRealizado;

    [RelayCommand]
    private async Task GuardarRetiroAsync()
    {
        if (IsBusy)
            return;

        var parada = Parada;
        if (parada is null || parada.Id == Guid.Empty || parada.RetiroId is not { } retiroId || retiroId == Guid.Empty)
        {
            await AvisarAsync("Retiro no disponible", "No se pudo identificar el retiro. Vuelva a la hoja de ruta e intente nuevamente.");
            return;
        }

        if (!TodosEscaneados)
        {
            await AvisarAsync("Faltan bultos", "Debe escanear todos los bultos del retiro. Si no se pudo retirar, use «No se pudo realizar el retiro».");
            return;
        }

        if (string.IsNullOrWhiteSpace(NombreEntrega))
        {
            await AvisarAsync("Dato requerido", "Complete el nombre de quien entrega los bultos.");
            return;
        }

        if (!TieneFoto)
        {
            await AvisarAsync("Fotografía requerida", "Debe adjuntar una fotografía del estado de los bultos.");
            return;
        }

        if (!TieneFirma)
        {
            await AvisarAsync("Firma requerida", "Debe registrar la firma de quien entrega los bultos.");
            return;
        }

        try
        {
            IsBusy = true;

            await ActualizarUbicacionYFechaAsync();

            var firma = await ObtenerFirmaBase64Async();
            if (firma is null)
            {
                await AvisarAsync("Firma no disponible", "No se pudo capturar la firma dibujada. Límpiela y vuelva a firmar.");
                return;
            }

            var payload = new RetiroRealizadoOfflinePayload
            {
                HojaDeRutaId = parada.HojaDeRutaId,
                ParadaId = parada.Id,
                RetiroId = retiroId,
                IdempotencyKey = $"RETIRO-{retiroId:N}",
                CodigosBultosEscaneados = Bultos.Select(b => b.Codigo).ToList(),
                FotoEstadoBase64 = FotoEvidenciaBase64,
                FirmaBase64 = firma,
                NombreEntrega = NombreEntrega.Trim(),
                DocumentoEntrega = string.IsNullOrWhiteSpace(DocumentoEntrega) ? null : DocumentoEntrega.Trim(),
                Latitud = LatitudActual,
                Longitud = LongitudActual,
                FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
            };

            await EncolarYCerrarAsync(
                parada,
                TiposOperacionOffline.RetiroRealizado,
                payload,
                nuevoEstado: "Entregado",
                titulo: "Retiro registrado",
                detalle: $"Se retiraron {Bultos.Count} bulto(s) de {NombreEntrega.Trim()}.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegistrarNoRealizadoAsync()
    {
        if (IsBusy)
            return;

        var parada = Parada;
        if (parada is null || parada.Id == Guid.Empty || parada.RetiroId is not { } retiroId || retiroId == Guid.Empty)
        {
            await AvisarAsync("Retiro no disponible", "No se pudo identificar el retiro. Vuelva a la hoja de ruta e intente nuevamente.");
            return;
        }

        var motivo = _motivosCatalogo.FirstOrDefault(m => string.Equals(m.Descripcion, MotivoSeleccionado, StringComparison.OrdinalIgnoreCase));
        if (motivo is null)
        {
            await AvisarAsync(
                "Motivo requerido",
                "Seleccione el motivo. Si la lista está vacía, descargue la hoja de ruta con conexión para obtener el catálogo del operador.");
            return;
        }

        try
        {
            IsBusy = true;

            await ActualizarUbicacionYFechaAsync();

            var payload = new RetiroNoRealizadoOfflinePayload
            {
                HojaDeRutaId = parada.HojaDeRutaId,
                ParadaId = parada.Id,
                RetiroId = retiroId,
                IdempotencyKey = $"RETIRO-NO-{retiroId:N}-{DateTime.UtcNow.Ticks}",
                MotivoCodigo = motivo.Codigo,
                MotivoNoEntregaId = motivo.Id,
                Observaciones = string.IsNullOrWhiteSpace(ObservacionesNoRealizado) ? null : ObservacionesNoRealizado.Trim(),
                Latitud = LatitudActual,
                Longitud = LongitudActual,
                FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
            };

            await EncolarYCerrarAsync(
                parada,
                TiposOperacionOffline.RetiroNoRealizado,
                payload,
                nuevoEstado: "Fallido",
                titulo: "Retiro no realizado",
                detalle: $"Quedó registrado el motivo: {motivo.Descripcion}.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EncolarYCerrarAsync<T>(ParadaItemViewModel parada, string tipoOperacion, T payload, string nuevoEstado, string titulo, string detalle)
    {
        if (_localDatabaseService is not null)
        {
            await _localDatabaseService.EncolarOperacionAsync(
                tipoOperacion,
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));

            await _localDatabaseService.ActualizarEstadoParadaLocalAsync(parada.Id, nuevoEstado);
        }

        parada.Estado = nuevoEstado;

        if (_syncService?.IsOnline == true)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _syncService.SincronizarOperacionesPendientesAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RegistroRetiroViewModel] Sincronización en segundo plano: {ex.Message}");
                }
            });
        }

        if (Shell.Current is not null)
        {
            var infoRed = _syncService?.IsOnline == true
                ? "Se está enviando a la central."
                : "Guardado en el dispositivo. Se enviará automáticamente al recuperar la conexión.";

            await Shell.Current.DisplayAlertAsync(titulo, $"{detalle}\n\n{infoRed}", "Finalizar");
            await Shell.Current.GoToAsync("//RutaDiaPage");
        }
    }
}
