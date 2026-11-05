using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;
using UltimaMilla.Mobile.Views.Carga;

namespace UltimaMilla.Mobile.ViewModels.Carga;

public partial class BultoCargaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CodigoBulto { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public int ParadaOrden { get; init; }
    public decimal PesoKg { get; init; }
    public bool EsSobrante { get; init; }
    public string EstadoEnvio { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(BadgeColor))]
    [NotifyPropertyChangedFor(nameof(BadgeTextColor))]
    [NotifyPropertyChangedFor(nameof(CardBgColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderColor))]
    private bool _verificadoEnCarga;

    public bool EstaEscaneado
    {
        get => VerificadoEnCarga;
        set => VerificadoEnCarga = value;
    }

    public string EstadoTexto
    {
        get
        {
            if (EsSobrante)
                return "Sobrante / Discrepancia";
            if (EstadoEnvio == "EnDevolucion")
                return "En Devolución (Rendir en Depósito)";
            if (EstadoEnvio == "Entregado")
                return "Entregado en Destino";
            return VerificadoEnCarga ? "Cargado en Vehículo" : "Pendiente de Escaneo";
        }
    }

    public Color BadgeColor
    {
        get
        {
            if (EsSobrante)
                return Color.FromArgb("#FEF2F2");
            if (EstadoEnvio == "EnDevolucion")
                return Color.FromArgb("#FFF7ED");
            if (EstadoEnvio == "Entregado")
                return Color.FromArgb("#EFF6FF");
            return VerificadoEnCarga ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#F1F5F9");
        }
    }

    public Color BadgeTextColor
    {
        get
        {
            if (EsSobrante)
                return Color.FromArgb("#DC2626");
            if (EstadoEnvio == "EnDevolucion")
                return Color.FromArgb("#C2410C");
            if (EstadoEnvio == "Entregado")
                return Color.FromArgb("#2563EB");
            return VerificadoEnCarga ? Color.FromArgb("#16A34A") : Color.FromArgb("#64748B");
        }
    }

    public Color CardBgColor
    {
        get
        {
            if (EsSobrante)
                return Color.FromArgb("#FEF2F2");
            return VerificadoEnCarga ? Color.FromArgb("#F8FAFC") : Color.FromArgb("#FFFFFF");
        }
    }

    public Color CardBorderColor
    {
        get
        {
            if (EsSobrante)
                return Color.FromArgb("#FCA5A5");
            return VerificadoEnCarga ? Color.FromArgb("#86EFAC") : Color.FromArgb("#CBD5E1");
        }
    }
}

public partial class CargaVehiculoViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly IHttpClientFactory? _httpClientFactory;

    [ObservableProperty]
    private string _titulo = "Control de Carga en Vehículo";

    [ObservableProperty]
    private string _patente = "SBA-4321";

    [ObservableProperty]
    private string _vehiculoTipo = "Furgón Master (1200 kg)";

    [ObservableProperty]
    private string _numeroRuta = "HR-20261020-01";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsRutaLiberada))]
    [NotifyPropertyChangedFor(nameof(EsRutaDespachada))]
    [NotifyPropertyChangedFor(nameof(PuedeIniciarRecorrido))]
    private string _estadoHojaDeRuta = "Liberada";

    [ObservableProperty]
    private Guid _hojaDeRutaId = DemoDataDefaults.DefaultHojaDeRutaId;

    [ObservableProperty]
    private string _codigoEscaneoInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BultoCargaItemViewModel> _bultosPendientes = new();

    [ObservableProperty]
    private ObservableCollection<BultoCargaItemViewModel> _bultosCargadosLista = new();

    [ObservableProperty]
    private ObservableCollection<BultoCargaItemViewModel> _bultosSobrantes = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeIniciarRecorrido))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarCarga))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDiscrepancias))]
    [NotifyPropertyChangedFor(nameof(ResumenEstadoTexto))]
    private string _mensajeDiscrepancia = string.Empty;

    public bool TieneBultosPendientes => BultosPendientes.Count > 0;
    public bool SinBultosPendientes => BultosPendientes.Count == 0;
    public bool TieneBultosCargados => BultosCargadosLista.Count > 0;
    public bool TieneSobrantes => BultosSobrantes.Count > 0;
    public bool TieneDiscrepancias => TieneSobrantes || (!string.IsNullOrWhiteSpace(MensajeDiscrepancia)) || (TieneBultosPendientes && TieneBultosCargados);

    public int BultosCargados => BultosCargadosLista.Count;
    public int BultosTotales => BultosPendientes.Count + BultosCargadosLista.Count;
    public double ProgresoEscaneo => BultosTotales > 0 ? (double)BultosCargados / BultosTotales : 1.0;

    public bool PuedeConfirmarCarga => BultosCargadosLista.Count > 0 && !IsBusy;
    public bool PuedeIniciarRecorrido => (EstadoHojaDeRuta == "Despachada" || (BultosTotales > 0 && BultosPendientes.Count == 0)) && !IsBusy;

    public bool EsRutaLiberada => EstadoHojaDeRuta is "Liberada" or "EnCarga" or "Borrador";
    public bool EsRutaDespachada => EstadoHojaDeRuta is "Despachada" or "EnTransito";

    public string ResumenEstadoTexto
    {
        get
        {
            if (TieneSobrantes)
                return $"Discrepancia detectada: {BultosSobrantes.Count} bulto(s) sobrante(s).";
            if (TieneBultosPendientes && TieneBultosCargados)
                return $"Carga en progreso: {BultosCargados} cargado(s), {BultosPendientes.Count} pendiente(s) de escaneo.";
            if (TieneBultosPendientes)
                return $"Carga pendiente: {BultosPendientes.Count} bulto(s) por escanear antes de iniciar ruta.";
            if (BultosCargadosLista.Count > 0)
                return $"Carga completa y verificada ({BultosCargados} de {BultosTotales} bultos). Listo para iniciar ruta.";
            return "No hay bultos pendientes de escaneo. Carga completada.";
        }
    }

    public CargaVehiculoViewModel() : this(null, null, null)
    {
    }

    public CargaVehiculoViewModel(
        ILocalDatabaseService? localDatabaseService,
        ISyncService? syncService = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _httpClientFactory = httpClientFactory;

        CargarBultosRutaDefecto();
    }

    public async Task InicializarAsync()
    {
        if (_localDatabaseService is not null)
        {
            try
            {
                var rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync();
                if (rutaLocal?.Ruta != null)
                {
                    HojaDeRutaId = rutaLocal.Ruta.Id;
                    NumeroRuta = rutaLocal.Ruta.NumeroRuta;
                    EstadoHojaDeRuta = rutaLocal.Ruta.Estado;

                    BultosPendientes.Clear();
                    BultosCargadosLista.Clear();
                    BultosSobrantes.Clear();

                    var paradas = rutaLocal.Paradas ?? [];
                    var todosBultos = paradas
                        .OrderBy(p => p.Orden)
                        .SelectMany(p => (p.Bultos ?? []).Select(b => new BultoCargaItemViewModel
                        {
                            Id = b.Id,
                            CodigoBulto = b.Codigo,
                            CodigoSeguimiento = p.CodigoSeguimiento,
                            Destinatario = p.Destinatario,
                            Direccion = $"{p.Direccion}, {p.Ciudad}",
                            ParadaOrden = p.Orden,
                            PesoKg = b.PesoKg,
                            EstadoEnvio = p.Estado,
                            VerificadoEnCarga = b.VerificadoEnCarga
                        }))
                        .ToList();

                    if (todosBultos.Count > 0)
                    {
                        foreach (var b in todosBultos)
                        {
                            if (b.VerificadoEnCarga)
                            {
                                BultosCargadosLista.Add(b);
                            }
                            else
                            {
                                BultosPendientes.Add(b);
                            }
                        }
                    }
                    else
                    {
                        CargarBultosRutaDefecto();
                    }

                    ActualizarConteos();
                    return;
                }
            }
            catch
            {
                // Fallback a defaults de demo
            }
        }

        CargarBultosRutaDefecto();
    }

    private void CargarBultosRutaDefecto()
    {
        BultosPendientes.Clear();
        BultosCargadosLista.Clear();
        BultosSobrantes.Clear();

        var bultosDemo = new List<BultoCargaItemViewModel>
        {
            new()
            {
                CodigoBulto = "BLT-001-A",
                CodigoSeguimiento = "ENV-2026-00108",
                Destinatario = "Martín Rodríguez",
                Direccion = "Av. 18 de Julio 1450, Centro",
                ParadaOrden = 1,
                PesoKg = 1.3m,
                VerificadoEnCarga = false
            },
            new()
            {
                CodigoBulto = "BLT-001-B",
                CodigoSeguimiento = "ENV-2026-00108",
                Destinatario = "Martín Rodríguez",
                Direccion = "Av. 18 de Julio 1450, Centro",
                ParadaOrden = 1,
                PesoKg = 1.4m,
                VerificadoEnCarga = false
            },
            new()
            {
                CodigoBulto = "BLT-002-A",
                CodigoSeguimiento = "ENV-2026-00109",
                Destinatario = "Lucía Fernández",
                Direccion = "Bvar. España 2678, Pocitos",
                ParadaOrden = 2,
                PesoKg = 1.8m,
                VerificadoEnCarga = false
            },
            new()
            {
                CodigoBulto = "BLT-003-A",
                CodigoSeguimiento = "ENV-2026-00110",
                Destinatario = "Esteban Morales",
                Direccion = "Rambla Gandhi 456, Punta Carretas",
                ParadaOrden = 3,
                PesoKg = 2.5m,
                VerificadoEnCarga = false
            },
            new()
            {
                CodigoBulto = "BLT-003-B",
                CodigoSeguimiento = "ENV-2026-00110",
                Destinatario = "Esteban Morales",
                Direccion = "Rambla Gandhi 456, Punta Carretas",
                ParadaOrden = 3,
                PesoKg = 2.8m,
                VerificadoEnCarga = false
            },
            new()
            {
                CodigoBulto = "BLT-003-C",
                CodigoSeguimiento = "ENV-2026-00110",
                Destinatario = "Esteban Morales",
                Direccion = "Rambla Gandhi 456, Punta Carretas",
                ParadaOrden = 3,
                PesoKg = 2.0m,
                VerificadoEnCarga = false
            }
        };

        foreach (var b in bultosDemo)
        {
            BultosPendientes.Add(b);
        }

        EstadoHojaDeRuta = "Liberada";
        MensajeDiscrepancia = string.Empty;
        ActualizarConteos();
    }

    [RelayCommand]
    private async Task EscanearCodigoManualAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoEscaneoInput))
            return;

        var codigo = CodigoEscaneoInput.Trim().ToUpperInvariant();
        await ProcesarCodigoEscaneadoAsync(codigo);
        CodigoEscaneoInput = string.Empty;
    }

    private async Task ProcesarCodigoEscaneadoAsync(string codigoNormalizado)
    {
        // 1. Coincidencia en pendientes de la hoja de ruta
        var bultoPendiente = BultosPendientes.FirstOrDefault(b => b.CodigoBulto.Equals(codigoNormalizado, StringComparison.OrdinalIgnoreCase));
        if (bultoPendiente != null)
        {
            bultoPendiente.VerificadoEnCarga = true;
            BultosPendientes.Remove(bultoPendiente);
            BultosCargadosLista.Add(bultoPendiente);

            if (_localDatabaseService != null && bultoPendiente.Id != Guid.Empty)
            {
                await _localDatabaseService.MarcarBultoVerificadoCargaAsync(bultoPendiente.Id, true);
            }

            ActualizarConteos();

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bulto Verificado",
                    $"Bulto {bultoPendiente.CodigoBulto} verificado exitosamente.\nEnvío: {bultoPendiente.CodigoSeguimiento}\nDestinatario: {bultoPendiente.Destinatario}",
                    "Continuar");
            }
            return;
        }

        // 2. Ya cargado previamente
        var yaCargado = BultosCargadosLista.FirstOrDefault(b => b.CodigoBulto.Equals(codigoNormalizado, StringComparison.OrdinalIgnoreCase));
        if (yaCargado != null)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bulto Ya Cargado",
                    $"El bulto {yaCargado.CodigoBulto} ya se encuentra cargado y verificado en el vehículo.",
                    "Aceptar");
            }
            return;
        }

        // 3. Flujo Alternativo UC-18 (Sobrante): No pertenece a la hoja de ruta
        var yaRegistradoSobrante = BultosSobrantes.FirstOrDefault(s => s.CodigoBulto.Equals(codigoNormalizado, StringComparison.OrdinalIgnoreCase));
        if (yaRegistradoSobrante == null)
        {
            var sobrante = new BultoCargaItemViewModel
            {
                CodigoBulto = codigoNormalizado,
                CodigoSeguimiento = "ENV-NO-ASIGNADO",
                Destinatario = "Comercio no asignado",
                Direccion = "No corresponde a esta hoja de ruta",
                ParadaOrden = 0,
                PesoKg = 1.0m,
                EsSobrante = true,
                VerificadoEnCarga = true
            };
            BultosSobrantes.Add(sobrante);
        }

        MensajeDiscrepancia = $"Discrepancia detectada: El bulto {codigoNormalizado} no pertenece a esta hoja de ruta (Sobrante).";
        ActualizarConteos();

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Discrepancia Detectada",
                $"Discrepancia detectada: El bulto {codigoNormalizado} no pertenece a esta hoja de ruta (Sobrante).",
                "Entendido");
        }
    }

    [RelayCommand]
    private async Task EscanearConCamaraAsync()
    {
        if (Shell.Current is null)
            return;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Permiso Requerido",
                    "Permiso de cámara denegado. Habilítelo en la configuración del dispositivo o utilice el ingreso manual.",
                    "Entendido");
                return;
            }

            var escanerModal = new EscanerCodigoPage();
            await Shell.Current.Navigation.PushModalAsync(escanerModal);
            var codigoLeido = await escanerModal.ResultadoEscaneoTask;

            if (!string.IsNullOrWhiteSpace(codigoLeido))
            {
                CodigoEscaneoInput = codigoLeido.Trim();
                await ProcesarCodigoEscaneadoAsync(codigoLeido.Trim().ToUpperInvariant());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] Fallo al iniciar cámara: {ex.Message}");
            await Shell.Current.DisplayAlertAsync(
                "Cámara No Disponible",
                "El lector de cámara no está disponible en este dispositivo. Puede utilizar el lector láser o ingresar el código manualmente.",
                "Entendido");
        }
    }

    [RelayCommand]
    private async Task EscanearBultoAsync()
    {
        if (Shell.Current is null)
            return;

        var opcion = await Shell.Current.DisplayActionSheetAsync(
            "Control de Carga en Vehículo",
            "Cancelar",
            null,
            "Escanear con cámara",
            "Escanear siguiente bulto pendiente",
            "Simular escaneo completo (Todos cargados)",
            "Simular bulto sobrante (BLT-999-SOBRANTE)",
            "Ingresar código manual...");

        if (string.IsNullOrEmpty(opcion) || opcion == "Cancelar")
            return;

        if (opcion == "Escanear con cámara")
        {
            await EscanearConCamaraAsync();
        }
        else if (opcion == "Escanear siguiente bulto pendiente")
        {
            var siguiente = BultosPendientes.FirstOrDefault();
            if (siguiente != null)
            {
                await ProcesarCodigoEscaneadoAsync(siguiente.CodigoBulto);
            }
            else
            {
                await Shell.Current.DisplayAlertAsync(
                    "Carga Completa",
                    "No hay bultos pendientes de escaneo. Carga completada.",
                    "Aceptar");
            }
        }
        else if (opcion == "Simular escaneo completo (Todos cargados)")
        {
            await SimularEscaneoCompletoAsync();
        }
        else if (opcion == "Simular bulto sobrante (BLT-999-SOBRANTE)")
        {
            await ProcesarCodigoEscaneadoAsync("BLT-999-SOBRANTE");
        }
        else if (opcion == "Ingresar código manual...")
        {
            var codigoIngresado = await Shell.Current.DisplayPromptAsync(
                "Ingreso Manual de Bulto",
                "Ingrese el código alfanumérico del bulto:",
                "Escanear",
                "Cancelar",
                "Ej: BLT-002-A");

            if (!string.IsNullOrWhiteSpace(codigoIngresado))
            {
                await ProcesarCodigoEscaneadoAsync(codigoIngresado.Trim().ToUpperInvariant());
            }
        }
    }

    [RelayCommand]
    private async Task SimularEscaneoCompletoAsync()
    {
        var lista = BultosPendientes.ToList();
        foreach (var b in lista)
        {
            b.VerificadoEnCarga = true;
            BultosPendientes.Remove(b);
            BultosCargadosLista.Add(b);

            if (_localDatabaseService != null && b.Id != Guid.Empty)
            {
                await _localDatabaseService.MarcarBultoVerificadoCargaAsync(b.Id, true);
            }
        }

        ActualizarConteos();

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Carga Completa",
                "Todos los bultos han sido marcados como cargados exitosamente.",
                "Aceptar");
        }
    }

    [RelayCommand]
    private async Task TocarBultoPendienteAsync(BultoCargaItemViewModel? bulto)
    {
        if (bulto == null)
            return;

        await ProcesarCodigoEscaneadoAsync(bulto.CodigoBulto);
    }

    [RelayCommand]
    private void ReiniciarCarga()
    {
        CargarBultosRutaDefecto();
    }

    private void ActualizarConteos()
    {
        OnPropertyChanged(nameof(TieneBultosPendientes));
        OnPropertyChanged(nameof(SinBultosPendientes));
        OnPropertyChanged(nameof(TieneBultosCargados));
        OnPropertyChanged(nameof(TieneSobrantes));
        OnPropertyChanged(nameof(TieneDiscrepancias));
        OnPropertyChanged(nameof(BultosCargados));
        OnPropertyChanged(nameof(BultosTotales));
        OnPropertyChanged(nameof(ProgresoEscaneo));
        OnPropertyChanged(nameof(PuedeConfirmarCarga));
        OnPropertyChanged(nameof(PuedeIniciarRecorrido));
        OnPropertyChanged(nameof(EsRutaLiberada));
        OnPropertyChanged(nameof(EsRutaDespachada));
        OnPropertyChanged(nameof(ResumenEstadoTexto));
    }

    [RelayCommand]
    private async Task IniciarRecorridoAsync()
    {
        if (IsBusy)
            return;

        if (!PuedeIniciarRecorrido)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Verificación Incompleta",
                    "Debe verificar el 100% de los bultos antes de iniciar el recorrido.",
                    "Entendido");
            }
            return;
        }

        try
        {
            IsBusy = true;

            if (_localDatabaseService != null)
            {
                await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(HojaDeRutaId, "EnTransito");
            }

            EstadoHojaDeRuta = "EnTransito";

            var idempotencyKey = $"INICIO-RUTA-{HojaDeRutaId:N}-{DateTime.UtcNow.Ticks}";
            var payload = new IniciarRecorridoOfflinePayload
            {
                HojaDeRutaId = HojaDeRutaId,
                IdempotencyKey = idempotencyKey,
                FechaInicioUtc = DateTime.UtcNow
            };

            if (_localDatabaseService != null)
            {
                var json = JsonSerializer.Serialize(payload);
                var opId = await _localDatabaseService.EncolarOperacionAsync(TiposOperacionOffline.IniciarRecorrido, json);

                if (_syncService?.IsOnline == true && _httpClientFactory != null)
                {
                    try
                    {
                        var client = _httpClientFactory.CreateClient("BackendApi");
                        var url = $"/api/mobile/v1/rutas/{HojaDeRutaId}/iniciar-recorrido";
                        var response = await client.PostAsJsonAsync(url, new { IdempotencyKey = idempotencyKey });
                        if (response.IsSuccessStatusCode)
                        {
                            await _localDatabaseService.MarcarComoSincronizadoAsync(opId);
                        }
                    }
                    catch
                    {
                        // Fallback a cola offline
                    }
                }
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Recorrido Iniciado",
                    "Carga verificada con éxito. Hoja de ruta iniciada en calle.",
                    "Aceptar");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmarCargaYDespacharAsync()
    {
        if (IsBusy)
            return;

        if (!PuedeConfirmarCarga)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Carga Incompleta",
                    "Debe escanear al menos un bulto antes de confirmar la carga.",
                    "Entendido");
            }
            return;
        }

        var faltantes = BultosPendientes.Count;
        var sobrantes = BultosSobrantes.Count;

        // UC-18 / UC-09: Flujo alternativo de discrepancia (faltantes o sobrantes)
        if (faltantes > 0 || sobrantes > 0)
        {
            if (Shell.Current is not null)
            {
                var codigosFaltantes = string.Join(", ", BultosPendientes.Select(b => b.CodigoBulto));
                var mensajeAlerta = faltantes > 0
                    ? $"Se detectaron {faltantes} bulto(s) faltante(s) sin escanear:\n{codigosFaltantes}\n\nLos envíos que no tengan todos sus bultos cargados quedarán excluidos de la ruta activa y volverán a depósito.\n\n¿Desea confirmar la salida del vehículo?"
                    : $"Se detectaron {sobrantes} bulto(s) sobrante(s).\n\n¿Desea registrar las discrepancias y confirmar la salida del vehículo?";

                var continuar = await Shell.Current.DisplayAlertAsync(
                    "Confirmación con Discrepancias",
                    mensajeAlerta,
                    "Confirmar Salida",
                    "Revisar Bultos");

                if (!continuar)
                    return;
            }
        }

        try
        {
            IsBusy = true;

            var codigosCargados = BultosCargadosLista
                .Select(b => b.CodigoBulto)
                .Concat(BultosSobrantes.Select(s => s.CodigoBulto))
                .ToList();

            var idempotencyKey = $"CARGA-{HojaDeRutaId:N}-{DateTime.UtcNow.Ticks}";
            var observaciones = (faltantes > 0 || sobrantes > 0)
                ? $"Discrepancia de carga: {faltantes} faltantes, {sobrantes} sobrantes. {MensajeDiscrepancia}".Trim()
                : "Carga completa y verificada sin discrepancias";

            // Persistir de forma Offline-First en SQLite local aplicando reglas UC-09 / UC-18
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.ConfirmarCargaYDespacharLocalAsync(HojaDeRutaId, codigosCargados);
            }

            EstadoHojaDeRuta = "Despachada";

            var payload = new ConfirmarCargaOfflinePayload
            {
                HojaDeRutaId = HojaDeRutaId,
                CodigosBultosCargados = codigosCargados,
                IdempotencyKey = idempotencyKey,
                ObservacionesDiscrepancia = observaciones
            };

            bool enviadoOnline = false;

            // Intentar confirmación directa con backend si hay conectividad
            if (_syncService?.IsOnline == true && _httpClientFactory is not null)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("BackendApi");
                    var requestBody = new
                    {
                        CodigosBultosCargados = codigosCargados,
                        IdempotencyKey = idempotencyKey,
                        ObservacionesDiscrepancia = observaciones
                    };

                    var url = $"/api/mobile/v1/rutas/{HojaDeRutaId}/carga/confirmar";
                    var response = await client.PostAsJsonAsync(url, requestBody);
                    if (response.IsSuccessStatusCode)
                    {
                        enviadoOnline = true;
                        // Invocar despacho en backend para completar transición UC-09
                        try
                        {
                            var despachoUrl = $"/api/planificacion/hojas-de-ruta/{HojaDeRutaId}/despacho";
                            await client.PostAsJsonAsync(despachoUrl, new { Observaciones = observaciones });
                        }
                        catch
                        {
                            // Silente ante fallo de despacho
                        }
                    }
                }
                catch
                {
                    enviadoOnline = false;
                }
            }

            // Encolar operación offline cifrada si no se pudo sincronizar online
            if (_localDatabaseService is not null)
            {
                var json = JsonSerializer.Serialize(payload);
                var opId = await _localDatabaseService.EncolarOperacionAsync(TiposOperacionOffline.ConfirmarCarga, json);
                if (enviadoOnline)
                {
                    await _localDatabaseService.MarcarComoSincronizadoAsync(opId);
                }
            }

            if (Shell.Current is not null)
            {
                string detalleConexion = enviadoOnline
                    ? "Carga y despacho sincronizados en tiempo real con el servidor."
                    : "Guardado en SQLite local. Se sincronizará automáticamente al conectarse.";

                await Shell.Current.DisplayAlertAsync(
                    "Carga Confirmada",
                    $"La carga del vehículo {Patente} fue confirmada exitosamente.\n{detalleConexion}",
                    "Ir a Mi Ruta");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
