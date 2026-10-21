using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Sync;

namespace UltimaMilla.Mobile.ViewModels.Carga;

public partial class BultoCargaItemViewModel : ObservableObject
{
    public string CodigoBulto { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public int ParadaOrden { get; init; }
    public decimal PesoKg { get; init; }
    public bool EsSobrante { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(BadgeColor))]
    [NotifyPropertyChangedFor(nameof(BadgeTextColor))]
    private bool _estaEscaneado;

    public string EstadoTexto => EsSobrante
        ? "Sobrante / Discrepancia"
        : (EstaEscaneado ? "Cargado y Verificado" : "Pendiente de Escaneo");

    public Color BadgeColor => EsSobrante
        ? Color.FromArgb("#FEF2F2")
        : (EstaEscaneado ? Color.FromArgb("#F0FDF4") : Color.FromArgb("#F1F5F9"));

    public Color BadgeTextColor => EsSobrante
        ? Color.FromArgb("#DC2626")
        : (EstaEscaneado ? Color.FromArgb("#16A34A") : Color.FromArgb("#64748B"));
}

public partial class CargaVehiculoViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly IHttpClientFactory? _httpClientFactory;

    [ObservableProperty]
    private string _titulo = "Control de Carga de Vehículo";

    [ObservableProperty]
    private string _patente = "SBA-4321";

    [ObservableProperty]
    private string _vehiculoTipo = "Furgón Master (1200 kg)";

    [ObservableProperty]
    private string _numeroRuta = "HR-20261020-01";

    [ObservableProperty]
    private Guid _hojaDeRutaId = DemoDataDefaults.DefaultHojaDeRutaId;

    [ObservableProperty]
    private ObservableCollection<BultoCargaItemViewModel> _bultos = new();

    [ObservableProperty]
    private ObservableCollection<BultoCargaItemViewModel> _bultosSobrantes = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDiscrepancias))]
    [NotifyPropertyChangedFor(nameof(ResumenEstadoTexto))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarCarga))]
    [NotifyPropertyChangedFor(nameof(ProgresoEscaneo))]
    private int _bultosCargados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgresoEscaneo))]
    private int _bultosTotales = 6;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDiscrepancias))]
    [NotifyPropertyChangedFor(nameof(ResumenEstadoTexto))]
    private string _mensajeDiscrepancia = string.Empty;

    public double ProgresoEscaneo => BultosTotales > 0 ? (double)BultosCargados / BultosTotales : 0;

    public bool TieneDiscrepancias => BultosSobrantes.Count > 0 || (!string.IsNullOrWhiteSpace(MensajeDiscrepancia)) || (BultosCargados > 0 && BultosCargados < BultosTotales);

    public bool TieneSobrantes => BultosSobrantes.Count > 0;

    public bool PuedeConfirmarCarga => BultosCargados > 0 || BultosSobrantes.Count > 0;

    public string ResumenEstadoTexto
    {
        get
        {
            if (BultosSobrantes.Count > 0)
                return $"Discrepancia detectada: {BultosSobrantes.Count} bulto(s) sobrante(s).";
            if (BultosCargados == BultosTotales)
                return "Carga completa y verificada (6 de 6 bultos). Listo para iniciar ruta.";
            if (BultosCargados > 0)
                return $"Carga en progreso: {BultosCargados} de {BultosTotales} bultos escaneados.";
            return "Presione Escanear Bulto para iniciar la verificación de la carga.";
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

    /// <summary>
    /// Inicializa la lista de bultos a partir de la base de datos local o la ruta activa.
    /// </summary>
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
                }
            }
            catch
            {
                // Fallback a defaults de demo
            }
        }
    }

    private void CargarBultosRutaDefecto()
    {
        Bultos.Clear();
        BultosSobrantes.Clear();

        // 6 bultos reales distribuidos en las 3 paradas de la hoja de ruta demo
        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-001-A",
            CodigoSeguimiento = "ENV-2026-00108",
            Destinatario = "Martín Rodríguez",
            Direccion = "Av. 18 de Julio 1450, Centro",
            ParadaOrden = 1,
            PesoKg = 1.3m,
            EstaEscaneado = false
        });

        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-001-B",
            CodigoSeguimiento = "ENV-2026-00108",
            Destinatario = "Martín Rodríguez",
            Direccion = "Av. 18 de Julio 1450, Centro",
            ParadaOrden = 1,
            PesoKg = 1.4m,
            EstaEscaneado = false
        });

        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-002-A",
            CodigoSeguimiento = "ENV-2026-00109",
            Destinatario = "Lucía Fernández",
            Direccion = "Bvar. España 2678, Pocitos",
            ParadaOrden = 2,
            PesoKg = 1.8m,
            EstaEscaneado = false
        });

        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-003-A",
            CodigoSeguimiento = "ENV-2026-00110",
            Destinatario = "Esteban Morales",
            Direccion = "Rambla Gandhi 456, Punta Carretas",
            ParadaOrden = 3,
            PesoKg = 2.5m,
            EstaEscaneado = false
        });

        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-003-B",
            CodigoSeguimiento = "ENV-2026-00110",
            Destinatario = "Esteban Morales",
            Direccion = "Rambla Gandhi 456, Punta Carretas",
            ParadaOrden = 3,
            PesoKg = 2.8m,
            EstaEscaneado = false
        });

        Bultos.Add(new BultoCargaItemViewModel
        {
            CodigoBulto = "BLT-003-C",
            CodigoSeguimiento = "ENV-2026-00110",
            Destinatario = "Esteban Morales",
            Direccion = "Rambla Gandhi 456, Punta Carretas",
            ParadaOrden = 3,
            PesoKg = 2.0m,
            EstaEscaneado = false
        });

        BultosTotales = Bultos.Count;
        BultosCargados = 0;
        MensajeDiscrepancia = string.Empty;
    }

    [RelayCommand]
    private async Task EscanearBultoAsync()
    {
        if (Shell.Current is null)
            return;

        var opcion = await Shell.Current.DisplayActionSheetAsync(
            "Simulador de Escaneo (UC-18)",
            "Cancelar",
            null,
            "Escanear siguiente bulto pendiente",
            "Escanear todos los bultos (Carga completa)",
            "Simular bulto sobrante (BLT-999-SOBRANTE)",
            "Ingresar código manual...");

        if (string.IsNullOrEmpty(opcion) || opcion == "Cancelar")
            return;

        if (opcion == "Escanear siguiente bulto pendiente")
        {
            EscanearSiguienteBulto();
        }
        else if (opcion == "Escanear todos los bultos (Carga completa)")
        {
            EscanearTodos();
        }
        else if (opcion == "Simular bulto sobrante (BLT-999-SOBRANTE)")
        {
            SimularBultoSobrante();
        }
        else if (opcion == "Ingresar código manual...")
        {
            await IngresarCodigoManualAsync();
        }
    }

    private void EscanearSiguienteBulto()
    {
        var siguiente = Bultos.FirstOrDefault(b => !b.EstaEscaneado);
        if (siguiente is null)
        {
            Shell.Current?.DisplayAlertAsync(
                "Carga Completa",
                "Todos los bultos asignados a esta hoja de ruta ya han sido escaneados y verificados.",
                "Aceptar");
            return;
        }

        siguiente.EstaEscaneado = true;
        ActualizarConteos();

        if (Shell.Current is not null)
        {
            Shell.Current.DisplayAlertAsync(
                "Bulto Verificado (UC-18)",
                $"Bulto {siguiente.CodigoBulto} escaneado correctamente.\nEnvío: {siguiente.CodigoSeguimiento}\nDestinatario: {siguiente.Destinatario}",
                "Continuar");
        }
    }

    private void EscanearTodos()
    {
        foreach (var b in Bultos)
        {
            b.EstaEscaneado = true;
        }

        ActualizarConteos();

        if (Shell.Current is not null)
        {
            Shell.Current.DisplayAlertAsync(
                "Carga Completa (UC-18)",
                "Se han escaneado los 6 bultos de la hoja de ruta con éxito. El vehículo se encuentra listo para iniciar reparto.",
                "Aceptar");
        }
    }

    private void SimularBultoSobrante()
    {
        const string codigoSobrante = "BLT-999-SOBRANTE";
        if (BultosSobrantes.Any(s => s.CodigoBulto == codigoSobrante))
        {
            Shell.Current?.DisplayAlertAsync(
                "Bulto ya registrado",
                $"El bulto {codigoSobrante} ya fue registrado previamente como sobrante.",
                "Entendido");
            return;
        }

        var sobrante = new BultoCargaItemViewModel
        {
            CodigoBulto = codigoSobrante,
            CodigoSeguimiento = "ENV-EXTERNO-999",
            Destinatario = "Comercio no asignado",
            Direccion = "No corresponde a esta hoja de ruta",
            ParadaOrden = 0,
            PesoKg = 3.5m,
            EsSobrante = true,
            EstaEscaneado = true
        };

        BultosSobrantes.Add(sobrante);
        MensajeDiscrepancia = "Alerta UC-18: Se detectó 1 bulto sobrante (BLT-999-SOBRANTE) que no pertenece a esta ruta. Debe ser devuelto a depósito.";
        ActualizarConteos();

        if (Shell.Current is not null)
        {
            Shell.Current.DisplayAlertAsync(
                "Alerta de Discrepancia (UC-18)",
                "El bulto BLT-999-SOBRANTE no está asignado a su hoja de ruta.\n\nSe ha registrado la discrepancia visualmente para control del depósito.",
                "Entendido");
        }
    }

    private async Task IngresarCodigoManualAsync()
    {
        if (Shell.Current is null)
            return;

        var codigoIngresado = await Shell.Current.DisplayPromptAsync(
            "Ingreso Manual de Bulto",
            "Ingrese el código alfanumérico del bulto:",
            "Escanear",
            "Cancelar",
            "Ej: BLT-002-A");

        if (string.IsNullOrWhiteSpace(codigoIngresado))
            return;

        var codigoNormalizado = codigoIngresado.Trim().ToUpperInvariant();

        var bultoCoincidente = Bultos.FirstOrDefault(b => b.CodigoBulto.Equals(codigoNormalizado, StringComparison.OrdinalIgnoreCase));
        if (bultoCoincidente != null)
        {
            bultoCoincidente.EstaEscaneado = true;
            ActualizarConteos();
            await Shell.Current.DisplayAlertAsync(
                "Bulto Correcto",
                $"Bulto {bultoCoincidente.CodigoBulto} verificado exitosamente.",
                "Aceptar");
        }
        else
        {
            // Código no coincide con la ruta -> registrar discrepancia
            var esSobrante = new BultoCargaItemViewModel
            {
                CodigoBulto = codigoNormalizado,
                CodigoSeguimiento = "ENV-DESCONOCIDO",
                Destinatario = "No asignado",
                Direccion = "Discrepancia de carga",
                ParadaOrden = 0,
                PesoKg = 1.0m,
                EsSobrante = true,
                EstaEscaneado = true
            };

            BultosSobrantes.Add(esSobrante);
            MensajeDiscrepancia = $"Alerta UC-18: El bulto {codigoNormalizado} no corresponde a esta ruta.";
            ActualizarConteos();

            await Shell.Current.DisplayAlertAsync(
                "Bulto Sobrante (UC-18)",
                $"El bulto {codigoNormalizado} no pertenece a esta hoja de ruta. Se ha registrado la discrepancia.",
                "Entendido");
        }
    }

    [RelayCommand]
    private void ReiniciarCarga()
    {
        CargarBultosRutaDefecto();
    }

    private void ActualizarConteos()
    {
        BultosCargados = Bultos.Count(b => b.EstaEscaneado);
        OnPropertyChanged(nameof(TieneSobrantes));
        OnPropertyChanged(nameof(TieneDiscrepancias));
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

        var faltantes = BultosTotales - BultosCargados;
        var sobrantes = BultosSobrantes.Count;

        if (faltantes > 0 || sobrantes > 0)
        {
            if (Shell.Current is not null)
            {
                var continuar = await Shell.Current.DisplayAlertAsync(
                    "Confirmación con Discrepancias (UC-18)",
                    $"Se detectaron las siguientes discrepancias en la verificación:\n- {faltantes} bulto(s) faltante(s) sin escanear.\n- {sobrantes} bulto(s) sobrante(s).\n\n¿Desea registrar las discrepancias y confirmar la salida del vehículo?",
                    "Confirmar con Discrepancias",
                    "Revisar Bultos");

                if (!continuar)
                    return;
            }
        }

        try
        {
            IsBusy = true;

            var codigosCargados = Bultos
                .Where(b => b.EstaEscaneado)
                .Select(b => b.CodigoBulto)
                .Concat(BultosSobrantes.Select(s => s.CodigoBulto))
                .ToList();

            var idempotencyKey = $"CARGA-{HojaDeRutaId:N}-{DateTime.UtcNow.Ticks}";
            var observaciones = (faltantes > 0 || sobrantes > 0)
                ? $"Discrepancia de carga: {faltantes} faltantes, {sobrantes} sobrantes. {MensajeDiscrepancia}".Trim()
                : "Carga completa y verificada sin discrepancias";

            var payload = new ConfirmarCargaOfflinePayload
            {
                HojaDeRutaId = HojaDeRutaId,
                CodigosBultosCargados = codigosCargados,
                IdempotencyKey = idempotencyKey,
                ObservacionesDiscrepancia = observaciones
            };

            bool enviadoOnline = false;

            // Intentar confirmación directa si hay conexión
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
                    }
                }
                catch
                {
                    // Fallback a almacenamiento offline local
                    enviadoOnline = false;
                }
            }

            // Persistir operación en cola offline cifrada de SQLite
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
                    ? "Carga confirmada en el servidor central en tiempo real."
                    : "Guardado en SQLite cifrado local. Se sincronizará automáticamente al conectarse.";

                await Shell.Current.DisplayAlertAsync(
                    "Carga Confirmada (UC-18)",
                    $"La carga del vehículo {Patente} fue confirmada exitosamente.\n{detalleConexion}",
                    "Iniciar Ruta");

                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
