using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Views.Carga;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

/// <summary>Un bulto de la parada con la marca de si se entrega ahora (entrega parcial).</summary>
public partial class BultoEntregaItemViewModel : ObservableObject
{
    public string Codigo { get; init; } = string.Empty;
    public decimal PesoKg { get; init; }

    [ObservableProperty]
    private bool _entregado = true;

    public string PesoTexto => PesoKg > 0 ? $"{PesoKg:F1} kg" : string.Empty;
}

[QueryProperty(nameof(ParadaSeleccionada), "Parada")]
public partial class RegistroEntregaViewModel : EvidenciaViewModelBase
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    private ParadaItemViewModel? _paradaSeleccionada;

    [ObservableProperty]
    private string _nombreReceptor = string.Empty;

    [ObservableProperty]
    private string _documentoReceptor = string.Empty;

    [ObservableProperty]
    private string _motivoSeleccionado = string.Empty;

    [ObservableProperty]
    private string _observacionesPendientes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    [NotifyPropertyChangedFor(nameof(EsEntregaParcial))]
    [NotifyPropertyChangedFor(nameof(TituloGuardar))]
    private int _bultosMarcados;

    public ObservableCollection<BultoEntregaItemViewModel> Bultos { get; } = new();

    public ObservableCollection<string> MotivosNoEntrega { get; } = new();

    private readonly List<MotivoNoEntregaLocal> _motivosCatalogo = new();

    /// <summary>La selección por bulto solo se ofrece si el operador tiene habilitada la entrega parcial.</summary>
    public bool TieneVariosBultos => Bultos.Count > 1 && (_syncService?.FuncionalidadHabilitada("entrega-parcial") ?? true);

    public bool EsEntregaParcial => Bultos.Count > 0 && BultosMarcados > 0 && BultosMarcados < Bultos.Count;

    public string ResumenBultosTexto => Bultos.Count == 0
        ? "Esta parada no tiene bultos informados."
        : $"{BultosMarcados} de {Bultos.Count} bulto(s) se entregan ahora";

    public string TituloGuardar => EsEntregaParcial ? "Guardar entrega parcial" : "Guardar entrega";

    public RegistroEntregaViewModel() : this(null, null)
    {
    }

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
    }

    partial void OnParadaSeleccionadaChanged(ParadaItemViewModel? value)
    {
        if (value is null)
            return;

        if (!string.IsNullOrWhiteSpace(value.Destinatario))
            NombreReceptor = value.Destinatario.Trim();

        Bultos.Clear();
        foreach (var bulto in value.Bultos)
        {
            var item = new BultoEntregaItemViewModel { Codigo = bulto.Codigo, PesoKg = bulto.PesoKg };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BultoEntregaItemViewModel.Entregado))
                    RecalcularMarcados();
            };
            Bultos.Add(item);
        }

        OnPropertyChanged(nameof(TieneVariosBultos));
        RecalcularMarcados();
        _ = CargarMotivosAsync();
    }

    private void RecalcularMarcados() => BultosMarcados = Bultos.Count(b => b.Entregado);

    private async Task CargarMotivosAsync()
    {
        if (_localDatabaseService is null)
            return;

        try
        {
            _motivosCatalogo.Clear();
            MotivosNoEntrega.Clear();
            foreach (var motivo in await _localDatabaseService.ObtenerMotivosNoEntregaAsync())
            {
                _motivosCatalogo.Add(motivo);
                MotivosNoEntrega.Add(motivo.Descripcion);
            }

            if (string.IsNullOrEmpty(MotivoSeleccionado) && MotivosNoEntrega.Count > 0)
                MotivoSeleccionado = MotivosNoEntrega[0];
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] No se pudo leer el catálogo de motivos: {ex.Message}");
        }
    }

    /// <summary>Escanear un bulto lo marca como entregado; uno que no pertenece a la parada se rechaza.</summary>
    [RelayCommand]
    private async Task EscanearBultoAsync()
    {
        if (Shell.Current is null || Bultos.Count == 0)
            return;

        try
        {
            var permiso = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (permiso != PermissionStatus.Granted)
                permiso = await Permissions.RequestAsync<Permissions.Camera>();

            string? codigo;
            if (permiso == PermissionStatus.Granted)
            {
                var escaner = new EscanerCodigoPage();
                await Shell.Current.Navigation.PushModalAsync(escaner);
                codigo = await escaner.ResultadoEscaneoTask;
            }
            else
            {
                codigo = await Shell.Current.DisplayPromptAsync("Ingreso manual", "Ingrese el código del bulto:", "Validar", "Cancelar", "Código del bulto");
            }

            if (string.IsNullOrWhiteSpace(codigo))
                return;

            var bulto = Bultos.FirstOrDefault(b => b.Codigo.Equals(codigo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (bulto is null)
            {
                await AvisarAsync("Bulto no encontrado", $"El bulto {codigo.Trim().ToUpperInvariant()} no pertenece a esta parada.");
                return;
            }

            bulto.Entregado = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Error al escanear: {ex.Message}");
            await AvisarAsync("Escaneo no disponible", "No fue posible abrir el lector. Marque los bultos manualmente en la lista.");
        }
    }

    [RelayCommand]
    private async Task GuardarEntregaAsync()
    {
        if (IsBusy)
            return;

        var parada = ParadaSeleccionada;
        if (parada is null || parada.Id == Guid.Empty)
        {
            await AvisarAsync("Parada no disponible", "No se pudo identificar la parada. Vuelva a la hoja de ruta e intente nuevamente.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NombreReceptor) || string.IsNullOrWhiteSpace(DocumentoReceptor))
        {
            await AvisarAsync("Campos requeridos", "Complete el nombre y el documento de quien recibe.");
            return;
        }

        if (!TieneFirma)
        {
            await AvisarAsync("Firma requerida", "Debe registrar la firma del receptor antes de confirmar la entrega.");
            return;
        }

        if (Bultos.Count > 0 && BultosMarcados == 0)
        {
            await AvisarAsync("Ningún bulto entregado", "Si no se entregó ningún bulto, registre un intento fallido desde el detalle de la parada.");
            return;
        }

        var parcial = EsEntregaParcial;
        MotivoNoEntregaLocal? motivo = null;
        if (parcial)
        {
            motivo = _motivosCatalogo.FirstOrDefault(m => string.Equals(m.Descripcion, MotivoSeleccionado, StringComparison.OrdinalIgnoreCase));
            if (motivo is null)
            {
                await AvisarAsync(
                    "Motivo requerido",
                    "Seleccione el motivo por el que algunos bultos no se entregan. Si la lista está vacía, descargue la hoja de ruta con conexión para obtener el catálogo del operador.");
                return;
            }
        }

        try
        {
            IsBusy = true;

            // Auditoría exacta al confirmar: posición real y hora del dispositivo
            await ActualizarUbicacionYFechaAsync();

            var firmaBase64 = await ObtenerFirmaBase64Async();
            if (firmaBase64 is null)
            {
                await AvisarAsync("Firma no disponible", "No se pudo capturar la firma dibujada. Límpiela y vuelva a firmar.");
                return;
            }

            var fotoBase64 = TieneFoto ? FotoEvidenciaBase64 : null;
            var idempotencyKey = $"ENTREGA-{parada.Id:N}";

            string tipoOperacion;
            string cargaJson;
            var jsonOpciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (parcial)
            {
                var entregados = Bultos.Where(b => b.Entregado).Select(b => b.Codigo).ToList();
                var payload = new EntregaParcialOfflinePayload
                {
                    HojaDeRutaId = parada.HojaDeRutaId,
                    ParadaId = parada.Id,
                    EnvioId = parada.EnvioId,
                    IdempotencyKey = idempotencyKey,
                    CodigosBultosEntregados = entregados,
                    BultosNoEntregados = Bultos.Where(b => !b.Entregado).Select(b => new BultoNoEntregadoOfflinePayload
                    {
                        CodigoBulto = b.Codigo,
                        MotivoCodigo = motivo!.Codigo,
                        MotivoNoEntregaId = motivo.Id,
                        Observaciones = string.IsNullOrWhiteSpace(ObservacionesPendientes) ? null : ObservacionesPendientes.Trim()
                    }).ToList(),
                    NombreReceptor = NombreReceptor.Trim(),
                    DocumentoReceptor = DocumentoReceptor.Trim(),
                    FirmaDigitalBase64 = firmaBase64,
                    FotoEvidenciaBase64 = fotoBase64,
                    Latitud = LatitudActual,
                    Longitud = LongitudActual,
                    FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
                };
                tipoOperacion = TiposOperacionOffline.EntregaParcial;
                cargaJson = JsonSerializer.Serialize(payload, jsonOpciones);

                if (_localDatabaseService is not null)
                {
                    await _localDatabaseService.EncolarOperacionAsync(tipoOperacion, cargaJson);
                    await _localDatabaseService.MarcarEntregaParcialLocalAsync(parada.Id, entregados);
                }
            }
            else
            {
                var payload = new EntregaExitosaOfflinePayload
                {
                    HojaDeRutaId = parada.HojaDeRutaId,
                    ParadaId = parada.Id,
                    EnvioId = parada.EnvioId,
                    IdempotencyKey = idempotencyKey,
                    NombreReceptor = NombreReceptor.Trim(),
                    DocumentoReceptor = DocumentoReceptor.Trim(),
                    FirmaDigitalBase64 = firmaBase64,
                    FotoEvidenciaBase64 = fotoBase64,
                    Latitud = LatitudActual,
                    Longitud = LongitudActual,
                    FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
                };
                tipoOperacion = TiposOperacionOffline.EntregaExitosa;
                cargaJson = JsonSerializer.Serialize(payload, jsonOpciones);

                if (_localDatabaseService is not null)
                {
                    await _localDatabaseService.EncolarOperacionAsync(tipoOperacion, cargaJson);
                    await _localDatabaseService.ActualizarEstadoParadaLocalAsync(parada.Id, "Entregado");
                }
            }

            parada.Estado = "Entregado";

            // Si hay conexión, se envía enseguida; si no, queda en la cola y se reenvía al recuperar la señal
            if (_syncService?.IsOnline == true)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var resultado = await _syncService.SincronizarOperacionesPendientesAsync();
                        // Si el servidor rechazó la entrega, la operación queda en la cola y se le muestra el motivo al repartidor
                        if (resultado.ErroresNegocio > 0 && resultado.DetallesErrores is { Count: > 0 } && Shell.Current is not null)
                        {
                            var motivo = string.Join("\n", resultado.DetallesErrores.Take(3));
                            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(
                                "El servidor rechazó la entrega",
                                $"La entrega quedó guardada en el dispositivo pero la central no la aceptó:\n\n{motivo}",
                                "Entendido"));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Sincronización en segundo plano: {ex.Message}");
                    }
                });
            }

            if (Shell.Current is not null)
            {
                var infoRed = _syncService?.IsOnline == true
                    ? "Se está enviando a la central."
                    : "Guardado en el dispositivo. Se enviará automáticamente al recuperar la conexión.";
                var titulo = parcial ? "Entrega parcial registrada" : "Entrega registrada";
                var detalle = parcial
                    ? $"Se entregaron {BultosMarcados} de {Bultos.Count} bulto(s) a {NombreReceptor.Trim()}. El resto queda pendiente según la política del operador."
                    : $"Entrega registrada para {NombreReceptor.Trim()}.";

                await Shell.Current.DisplayAlertAsync(titulo, $"{detalle}\n\n{infoRed}", "Finalizar");
                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
