using System.Collections.ObjectModel;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Security;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.ViewModels.Ruta;
using UltimaMilla.Mobile.Views.Carga;

namespace UltimaMilla.Mobile.ViewModels.Carga;

public partial class BultoCargaItemViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ParadaId { get; init; }
    public Guid EnvioId { get; init; }
    public string CodigoBulto { get; init; } = string.Empty;
    public string Codigo => CodigoBulto;
    public string Identificacion { get; init; } = string.Empty;
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Destinatario { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public int ParadaOrden { get; init; }
    public decimal PesoKg { get; init; }
    public bool EsSobrante { get; init; }
    public string EstadoEnvio { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cargado))]
    [NotifyPropertyChangedFor(nameof(EstaEscaneado))]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(BadgeColor))]
    [NotifyPropertyChangedFor(nameof(BadgeTextColor))]
    [NotifyPropertyChangedFor(nameof(CardBgColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderColor))]
    private bool _verificadoEnCarga;

    public bool Cargado
    {
        get => VerificadoEnCarga;
        set => VerificadoEnCarga = value;
    }

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
    private static readonly TimeSpan IntervaloVigilanciaDespacho = TimeSpan.FromSeconds(15);

    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IBiometricAuthService? _biometricAuthService;
    private IDispatcherTimer? _vigilanciaDespacho;
    private bool _actualizandoSelectorRuta;

    public bool EscaneandoConCamara { get; private set; }

    [ObservableProperty]
    private string _titulo = "Control de Carga en Vehículo";

    [ObservableProperty]
    private string _patente = "Sin vehículo asignado";

    [ObservableProperty]
    private string _vehiculoTipo = "Sin datos";

    [ObservableProperty]
    private string _numeroRuta = "Sin hoja de ruta";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsRutaLiberada))]
    [NotifyPropertyChangedFor(nameof(EsRutaDespachada))]
    [NotifyPropertyChangedFor(nameof(EsperandoDespacho))]
    [NotifyPropertyChangedFor(nameof(PuedeIniciarRecorrido))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarCarga))]
    [NotifyPropertyChangedFor(nameof(MensajeEstadoDespacho))]
    private string _estadoHojaDeRuta = "Sin ruta";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayRuta))]
    [NotifyPropertyChangedFor(nameof(NoHayRuta))]
    private Guid _hojaDeRutaId = Guid.Empty;

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
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _tieneMultiplesRutas;

    [ObservableProperty]
    private HojaDeRutaSelectorItem? _hojaDeRutaSeleccionada;

    public ObservableCollection<HojaDeRutaSelectorItem> HojasDeRutaDisponibles { get; } = new();

    async partial void OnHojaDeRutaSeleccionadaChanged(HojaDeRutaSelectorItem? value)
    {
        if (value != null && !IsBusy && !_actualizandoSelectorRuta)
        {
            await CargarBultosDeRutaAsync(value.Id);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDiscrepancias))]
    [NotifyPropertyChangedFor(nameof(ResumenEstadoTexto))]
    private string _mensajeDiscrepancia = string.Empty;

    public bool HayRuta => HojaDeRutaId != Guid.Empty;
    public bool NoHayRuta => !HayRuta;
    public bool TieneBultosPendientes => BultosPendientes.Count > 0;
    public bool SinBultosPendientes => BultosPendientes.Count == 0;
    public bool TieneBultosCargados => BultosCargadosLista.Count > 0;
    public bool TieneSobrantes => BultosSobrantes.Count > 0;
    public bool TieneDiscrepancias => TieneSobrantes || (!string.IsNullOrWhiteSpace(MensajeDiscrepancia)) || (TieneBultosPendientes && TieneBultosCargados);

    public int BultosCargados => BultosCargadosLista.Count;
    public int BultosCargadosCount => BultosCargadosLista.Count;
    public int BultosPendientesCount => BultosPendientes.Count;
    public int BultosTotales => BultosPendientes.Count + BultosCargadosLista.Count;
    public int TotalBultosCount => BultosTotales;
    public double ProgresoEscaneo => BultosTotales > 0 ? (double)BultosCargados / BultosTotales : 0.0;
    public double ProgresoCarga => ProgresoEscaneo;
    public string ResumenProgresoCargaTexto => BultosTotales > 0
        ? $"{BultosCargados} de {BultosTotales} bultos cargados"
        : $"{BultosCargados} bultos cargados";

    /// <summary>La carga se escanea y se confirma mientras la ruta espera ser cargada o esté en proceso de carga (antes de ser despachada).</summary>
    public bool EsRutaLiberada => EstadoHojaDeRuta is "Liberada" or "EnCarga" or "Borrador" || BultosPendientes.Count > 0;

    /// <summary>La carga ya se informó: el despachador del depósito debe verificarla y despachar la ruta.</summary>
    public bool EsperandoDespacho => EstadoHojaDeRuta == "EnCarga" && BultosPendientes.Count == 0;

    /// <summary>Solo el servidor despacha la ruta; la app lo sabe cuando descarga la ruta con ese estado.</summary>
    public bool EsRutaDespachada => EstadoHojaDeRuta is "Despachada" or "EnTransito";

    public bool PuedeConfirmarCarga => HayRuta && EsRutaLiberada && BultosCargadosLista.Count > 0 && !IsBusy;
    public bool PuedeIniciarRecorrido => EstadoHojaDeRuta == "Despachada" && !IsBusy;

    public string MensajeEstadoDespacho => EstadoHojaDeRuta switch
    {
        "EnCarga" => "Carga informada al depósito. Esperando que el despachador verifique y despache la ruta. Esta pantalla se actualiza sola.",
        "Despachada" => "Ruta despachada por el depósito. Ya puede iniciar el recorrido.",
        "EnTransito" => "Recorrido en curso.",
        "Finalizada" => "La hoja de ruta ya fue finalizada.",
        _ => string.Empty
    };

    public string ResumenEstadoTexto
    {
        get
        {
            if (!HayRuta)
                return "No hay una hoja de ruta descargada. Conéctese y toque Actualizar para descargarla.";
            if (TieneSobrantes)
                return $"Discrepancia detectada: {BultosSobrantes.Count} bulto(s) sobrante(s).";
            if (TieneBultosPendientes && TieneBultosCargados)
                return $"Carga en progreso: {BultosCargados} de {BultosTotales} bultos cargados ({BultosPendientes.Count} pendiente(s) de escaneo).";
            if (TieneBultosPendientes)
                return $"Carga pendiente: {BultosPendientes.Count} bulto(s) por escanear antes de iniciar ruta.";
            if (BultosCargadosLista.Count > 0)
                return $"Carga completa y verificada ({BultosCargados} de {BultosTotales} bultos).";
            return "Esta hoja de ruta no tiene bultos para cargar.";
        }
    }

    public CargaVehiculoViewModel() : this(null, null, null, null)
    {
    }

    public CargaVehiculoViewModel(
        ILocalDatabaseService? localDatabaseService,
        ISyncService? syncService = null,
        IHttpClientFactory? httpClientFactory = null,
        IBiometricAuthService? biometricAuthService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        _httpClientFactory = httpClientFactory;
        _biometricAuthService = biometricAuthService;
    }

    /// <summary>Carga la ruta guardada en el dispositivo. Si todavía no hay ninguna, la pantalla lo informa en lugar de inventar datos.</summary>
    public async Task InicializarAsync()
    {
        if (EscaneandoConCamara)
            return;

        if (_syncService?.IsOnline == true)
        {
            try
            {
                await _syncService.DescargarYActualizarRutasDelServidorAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] Error al descargar rutas en inicialización: {ex.Message}");
            }
        }

        await CargarRutasDesdeSQLiteAsync();
    }

    [RelayCommand]
    public async Task ActualizarRutasServidorAsync()
    {
        try
        {
            IsRefreshing = true;
            await ActualizarDesdeServidorAsync(silencioso: false);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task CargarRutasDesdeSQLiteAsync()
    {
        BultosPendientes.Clear();
        BultosCargadosLista.Clear();
        BultosSobrantes.Clear();
        MensajeDiscrepancia = string.Empty;

        if (_localDatabaseService is null)
        {
            HojaDeRutaId = Guid.Empty;
            NumeroRuta = "Sin hoja de ruta";
            EstadoHojaDeRuta = "Sin ruta";
            ActualizarConteos();
            return;
        }

        var rutasLocales = await _localDatabaseService.ListarHojasDeRutaLocalesAsync();
        HojasDeRutaDisponibles.Clear();

        foreach (var r in rutasLocales)
        {
            string descEstado = r.Estado switch
            {
                "EnTransito" => "En Tránsito",
                "Despachada" => "Despachada",
                "EnCarga" => "En Carga",
                "Liberada" => "Liberada",
                "Finalizada" => "Finalizada",
                _ => r.Estado
            };

            HojasDeRutaDisponibles.Add(new HojaDeRutaSelectorItem(
                r.Id,
                r.NumeroRuta,
                r.Estado,
                r.TotalParadas,
                $"{r.NumeroRuta} - {descEstado} ({r.TotalParadas} paradas)"
            ));
        }

        TieneMultiplesRutas = HojasDeRutaDisponibles.Count > 1;

        var rutaParaCarga = HojasDeRutaDisponibles.FirstOrDefault(r => r.Estado is "Liberada" or "EnCarga")
            ?? HojasDeRutaDisponibles.FirstOrDefault(r => r.Estado is "Despachada" or "EnTransito")
            ?? HojasDeRutaDisponibles.FirstOrDefault();

        if (rutaParaCarga != null)
        {
            try
            {
                _actualizandoSelectorRuta = true;
                HojaDeRutaSeleccionada = rutaParaCarga;
            }
            finally
            {
                _actualizandoSelectorRuta = false;
            }
            await CargarBultosDeRutaAsync(rutaParaCarga.Id);
        }
        else
        {
            HojaDeRutaId = Guid.Empty;
            NumeroRuta = "Sin hoja de ruta";
            EstadoHojaDeRuta = "Sin ruta";
            ActualizarConteos();
        }
    }

    public async Task CargarBultosDeRutaAsync(Guid rutaId)
    {
        BultosPendientes.Clear();
        BultosCargadosLista.Clear();
        BultosSobrantes.Clear();
        MensajeDiscrepancia = string.Empty;

        RutaActivaLocalDto? rutaLocal = null;
        if (_localDatabaseService is not null)
        {
            try
            {
                rutaLocal = await _localDatabaseService.ObtenerRutaActivaLocalAsync(rutaId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] No se pudo leer la ruta local: {ex.Message}");
            }
        }

        if (rutaLocal?.Ruta is null)
        {
            HojaDeRutaId = Guid.Empty;
            NumeroRuta = "Sin hoja de ruta";
            EstadoHojaDeRuta = "Sin ruta";
            ActualizarConteos();
            return;
        }

        HojaDeRutaId = rutaLocal.Ruta.Id;
        NumeroRuta = string.IsNullOrWhiteSpace(rutaLocal.Ruta.NumeroRuta) ? "Sin número" : rutaLocal.Ruta.NumeroRuta;
        EstadoHojaDeRuta = rutaLocal.Ruta.Estado;

        var ultima = _syncService?.UltimaRutaDescargada;
        if (ultima is not null && ultima.HojaDeRutaId == rutaLocal.Ruta.Id)
        {
            Patente = string.IsNullOrWhiteSpace(ultima.PatenteVehiculo) ? "Sin vehículo asignado" : ultima.PatenteVehiculo;
            VehiculoTipo = string.IsNullOrWhiteSpace(ultima.TipoVehiculo) ? "Sin datos" : ultima.TipoVehiculo;
        }

        // Solo los bultos de las paradas de entrega y de retiro que siguen en la ruta
        var bultos = (rutaLocal.Paradas ?? [])
            .OrderBy(p => p.Orden)
            .SelectMany(p => (p.Bultos ?? []).Select(b => new BultoCargaItemViewModel
            {
                Id = b.Id,
                ParadaId = p.Id,
                EnvioId = p.EnvioId,
                CodigoBulto = b.Codigo,
                Identificacion = b.Codigo,
                CodigoSeguimiento = p.CodigoSeguimiento,
                Destinatario = p.Destinatario,
                Direccion = string.IsNullOrWhiteSpace(p.Ciudad) ? p.Direccion : $"{p.Direccion}, {p.Ciudad}",
                ParadaOrden = p.Orden,
                PesoKg = b.PesoKg,
                EstadoEnvio = p.Estado,
                VerificadoEnCarga = b.VerificadoEnCarga
            }))
            .ToList();

        foreach (var b in bultos)
        {
            if (b.VerificadoEnCarga)
                BultosCargadosLista.Add(b);
            else
                BultosPendientes.Add(b);
        }

        ActualizarConteos();
    }

    /// <summary>
    /// Mientras la ruta espera el despacho del depósito, consulta al servidor cada pocos segundos: apenas el despachador la despacha,
    /// la pantalla habilita el inicio del recorrido sin que el repartidor tenga que hacer nada.
    /// </summary>
    public void IniciarVigilanciaDespacho()
    {
        DetenerVigilanciaDespacho();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _vigilanciaDespacho = dispatcher.CreateTimer();
        _vigilanciaDespacho.Interval = IntervaloVigilanciaDespacho;
        _vigilanciaDespacho.Tick += async (_, _) =>
        {
            if (EsperandoDespacho && !IsBusy && !EscaneandoConCamara)
                await ActualizarDesdeServidorAsync(silencioso: true);
        };
        _vigilanciaDespacho.Start();
    }

    public void DetenerVigilanciaDespacho()
    {
        if (_vigilanciaDespacho is null)
            return;

        _vigilanciaDespacho.Stop();
        _vigilanciaDespacho = null;
    }

    [RelayCommand]
    private Task ActualizarAsync() => ActualizarDesdeServidorAsync(silencioso: false);

    /// <summary>Envía lo pendiente, descarga la ruta tal como está en el servidor y refresca la pantalla.</summary>
    private async Task ActualizarDesdeServidorAsync(bool silencioso)
    {
        if (_syncService is null)
            return;

        if (!_syncService.IsOnline)
        {
            if (!silencioso && Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Sin conexión",
                    "No hay conexión con el servidor. Se mostrará lo guardado en el dispositivo y se actualizará al recuperar la señal.",
                    "Entendido");
            }
            return;
        }

        try
        {
            await _syncService.SincronizarOperacionesPendientesAsync();
            await _syncService.DescargarYActualizarRutasDelServidorAsync();
            await CargarRutasDesdeSQLiteAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] No se pudo actualizar desde el servidor: {ex.Message}");
            if (!silencioso && Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync("No se pudo actualizar", "Intente nuevamente en unos segundos.", "Entendido");
            }
        }
    }

    private static string NormalizarCodigoEscaneado(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var limpio = new string(raw.Where(c => !char.IsControl(c) && c != '\u200B' && c != '\uFEFF').ToArray()).Trim();

        string[] prefijos = ["ID BULTO:", "ID DE BULTO:", "BULTO:", "CODIGO:", "ID:"];
        foreach (var prefijo in prefijos)
        {
            if (limpio.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                limpio = limpio[prefijo.Length..].Trim();
                break;
            }
        }

        return limpio;
    }

    private static BultoCargaItemViewModel? BuscarBultoTolerante(IEnumerable<BultoCargaItemViewModel> coleccion, string codigoLimpio)
    {
        var lista = coleccion.ToList();
        if (lista.Count == 0 || string.IsNullOrWhiteSpace(codigoLimpio))
            return null;

        // 1. Coincidencia exacta por CodigoBulto o Identificacion
        var exacto = lista.FirstOrDefault(b =>
            string.Equals(NormalizarCodigoEscaneado(b.CodigoBulto), codigoLimpio, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizarCodigoEscaneado(b.Identificacion), codigoLimpio, StringComparison.OrdinalIgnoreCase));
        if (exacto != null)
            return exacto;

        // 2. Coincidencia exacta por CodigoSeguimiento del envío asociado
        var porSeguimiento = lista.FirstOrDefault(b =>
            !string.IsNullOrWhiteSpace(b.CodigoSeguimiento) &&
            string.Equals(NormalizarCodigoEscaneado(b.CodigoSeguimiento), codigoLimpio, StringComparison.OrdinalIgnoreCase));
        if (porSeguimiento != null)
            return porSeguimiento;

        // 3. Coincidencia compuesta o sufijo (ej. TRK-XXXX-BLT-001 vs BLT-001)
        if (codigoLimpio.Length >= 4)
        {
            var parcial = lista.FirstOrDefault(b =>
            {
                var codBulto = NormalizarCodigoEscaneado(b.CodigoBulto);
                var codSeg = NormalizarCodigoEscaneado(b.CodigoSeguimiento);
                if (!string.IsNullOrEmpty(codBulto))
                {
                    if (string.Equals($"{codSeg}-{codBulto}", codigoLimpio, StringComparison.OrdinalIgnoreCase) ||
                        codBulto.EndsWith(codigoLimpio, StringComparison.OrdinalIgnoreCase) ||
                        codigoLimpio.EndsWith(codBulto, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            });
            if (parcial != null)
                return parcial;
        }

        return null;
    }

    [RelayCommand]
    private async Task EscanearCodigoManualAsync()
    {
        var codigoLimpio = NormalizarCodigoEscaneado(CodigoEscaneoInput);
        if (string.IsNullOrWhiteSpace(codigoLimpio))
            return;

        await ProcesarCodigoEscaneadoAsync(codigoLimpio);
        CodigoEscaneoInput = string.Empty;
    }

    private async Task ProcesarCodigoEscaneadoAsync(string codigoNormalizado, Guid? bultoIdEspecifico = null)
    {
        var codigoLimpio = NormalizarCodigoEscaneado(codigoNormalizado);
        if (string.IsNullOrWhiteSpace(codigoLimpio))
            return;

        if (!HayRuta)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Sin Hoja de Ruta",
                    "No hay una hoja de ruta activa cargada en el dispositivo. Toque Actualizar para sincronizar con el servidor.",
                    "Entendido");
            }
            return;
        }

        // 1. Buscar primero entre los bultos de la hoja de ruta que aún NO están cargados (!b.Cargado)
        BultoCargaItemViewModel? bultoPendiente = null;
        if (bultoIdEspecifico.HasValue && bultoIdEspecifico.Value != Guid.Empty)
        {
            bultoPendiente = BultosPendientes.FirstOrDefault(b => !b.Cargado && b.Id == bultoIdEspecifico.Value);
        }

        bultoPendiente ??= BuscarBultoTolerante(BultosPendientes.Where(b => !b.Cargado), codigoLimpio);

        if (bultoPendiente != null)
        {
            // Persistir por clave primaria única (Id) en SQLite antes de refrescar colecciones
            if (_localDatabaseService != null)
            {
                await _localDatabaseService.MarcarBultoVerificadoCargaAsync(bultoPendiente.Id, true);
            }

            bultoPendiente.VerificadoEnCarga = true;
            bultoPendiente.Cargado = true;

            BultosPendientes.Remove(bultoPendiente);
            BultosCargadosLista.Add(bultoPendiente);

            if (!TieneSobrantes)
            {
                MensajeDiscrepancia = string.Empty;
            }

            CodigoEscaneoInput = string.Empty;
            ActualizarConteos();

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bulto Verificado",
                    $"Bulto {bultoPendiente.CodigoBulto} cargado correctamente ({BultosCargados} de {BultosTotales} bultos cargados).\nEnvío: {bultoPendiente.CodigoSeguimiento}\nDestinatario: {bultoPendiente.Destinatario}",
                    "Continuar");
            }
            return;
        }

        // 2. Solo si TODOS los bultos con ese código ya están marcados como Cargado == true, informar que ya fue escaneado previamente
        var yaCargado = BuscarBultoTolerante(BultosCargadosLista.Where(b => b.Cargado), codigoLimpio);
        if (yaCargado != null)
        {
            CodigoEscaneoInput = string.Empty;
            ActualizarConteos();

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bulto Ya Cargado",
                    $"El bulto {yaCargado.CodigoBulto} ya fue escaneado y verificado previamente en el vehículo.",
                    "Aceptar");
            }
            return;
        }

        // 3. No coincide con ningún bulto de la ruta activa -> registrar como discrepancia/sobrante mostrando código recibido y códigos esperados
        var yaRegistradoSobrante = BultosSobrantes.FirstOrDefault(s =>
            string.Equals(NormalizarCodigoEscaneado(s.CodigoBulto), codigoLimpio, StringComparison.OrdinalIgnoreCase));

        if (yaRegistradoSobrante == null)
        {
            BultosSobrantes.Add(new BultoCargaItemViewModel
            {
                CodigoBulto = codigoLimpio,
                Identificacion = codigoLimpio,
                Destinatario = "Sin asignar",
                Direccion = "No corresponde a esta hoja de ruta",
                ParadaOrden = 0,
                EsSobrante = true,
                VerificadoEnCarga = true
            });
        }

        var codigosEsperados = BultosPendientes
            .Concat(BultosCargadosLista)
            .Select(b => b.CodigoBulto)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var textoEsperados = codigosEsperados.Count > 0
            ? string.Join(", ", codigosEsperados)
            : "Ninguno";

        MensajeDiscrepancia = $"Discrepancia detectada: El código '{codigoLimpio}' no pertenece a esta hoja de ruta (Sobrante). Códigos esperados en la ruta: {textoEsperados}.";
        CodigoEscaneoInput = string.Empty;
        ActualizarConteos();

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Discrepancia Detectada",
                $"El código recibido '{codigoLimpio}' no pertenece a esta hoja de ruta y se registró como sobrante.\n\nCódigos esperados en la ruta:\n{textoEsperados}",
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

            EscaneandoConCamara = true;
            var escanerModal = new EscanerCodigoPage();
            await Shell.Current.Navigation.PushModalAsync(escanerModal);
            var codigoLeido = await escanerModal.ResultadoEscaneoTask;

            var codigoLimpio = NormalizarCodigoEscaneado(codigoLeido);
            if (!string.IsNullOrWhiteSpace(codigoLimpio))
            {
                CodigoEscaneoInput = codigoLimpio;
                await ProcesarCodigoEscaneadoAsync(codigoLimpio);
                CodigoEscaneoInput = string.Empty;
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
        finally
        {
            EscaneandoConCamara = false;
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
            "Ingresar código manual...");

        if (string.IsNullOrEmpty(opcion) || opcion == "Cancelar")
            return;

        if (opcion == "Escanear con cámara")
        {
            await EscanearConCamaraAsync();
        }
        else if (opcion == "Ingresar código manual...")
        {
            var codigoIngresado = await Shell.Current.DisplayPromptAsync(
                "Ingreso Manual de Bulto",
                "Ingrese el código del bulto:",
                "Validar",
                "Cancelar",
                "Código del bulto");

            var codigoLimpio = NormalizarCodigoEscaneado(codigoIngresado);
            if (!string.IsNullOrWhiteSpace(codigoLimpio))
            {
                await ProcesarCodigoEscaneadoAsync(codigoLimpio);
            }
        }
    }

    [RelayCommand]
    private async Task TocarBultoPendienteAsync(BultoCargaItemViewModel? bulto)
    {
        if (bulto == null)
            return;

        await ProcesarCodigoEscaneadoAsync(bulto.CodigoBulto, bulto.Id);
    }

    private void ActualizarConteos()
    {
        OnPropertyChanged(nameof(TieneBultosPendientes));
        OnPropertyChanged(nameof(SinBultosPendientes));
        OnPropertyChanged(nameof(TieneBultosCargados));
        OnPropertyChanged(nameof(TieneSobrantes));
        OnPropertyChanged(nameof(TieneDiscrepancias));
        OnPropertyChanged(nameof(BultosCargados));
        OnPropertyChanged(nameof(BultosCargadosCount));
        OnPropertyChanged(nameof(BultosPendientesCount));
        OnPropertyChanged(nameof(BultosTotales));
        OnPropertyChanged(nameof(TotalBultosCount));
        OnPropertyChanged(nameof(ProgresoEscaneo));
        OnPropertyChanged(nameof(ProgresoCarga));
        OnPropertyChanged(nameof(ResumenProgresoCargaTexto));
        OnPropertyChanged(nameof(PuedeConfirmarCarga));
        OnPropertyChanged(nameof(PuedeIniciarRecorrido));
        OnPropertyChanged(nameof(EsRutaLiberada));
        OnPropertyChanged(nameof(EsRutaDespachada));
        OnPropertyChanged(nameof(EsperandoDespacho));
        OnPropertyChanged(nameof(ResumenEstadoTexto));
        OnPropertyChanged(nameof(MensajeEstadoDespacho));
    }

    [RelayCommand]
    private async Task IniciarRecorridoAsync()
    {
        if (IsBusy)
            return;

        // El recorrido solo empieza cuando el servidor ya despachó la ruta (lo hace el despachador desde el depósito)
        if (!PuedeIniciarRecorrido)
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Ruta sin despachar",
                    "El despachador del depósito todavía no despachó esta hoja de ruta. Espere su verificación o toque Actualizar.",
                    "Entendido");
            }
            return;
        }

        if (_biometricAuthService != null)
        {
            var autorizado = await _biometricAuthService.ValidarIdentidadAsync(
                "Autorizar Custodia de Carga",
                "Verifique su identidad para asumir la custodia de los bultos e iniciar el recorrido.");

            if (!autorizado)
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Operación Cancelada",
                        "No se completó la verificación de identidad. La custodia de la carga no fue confirmada.",
                        "Entendido");
                }
                return;
            }
        }

        try
        {
            IsBusy = true;

            if (_localDatabaseService != null)
            {
                await _localDatabaseService.ActualizarEstadoHojaDeRutaAsync(HojaDeRutaId, "EnTransito");
            }

            EstadoHojaDeRuta = "EnTransito";

            var idempotencyKey = $"INICIO-RUTA-{HojaDeRutaId:N}";
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
                    catch (Exception ex)
                    {
                        // Queda en la cola y se reenvía al recuperar la conexión
                        System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] Inicio de recorrido en cola: {ex.Message}");
                    }
                }
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Recorrido Iniciado",
                    "Hoja de ruta iniciada en calle.",
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
    private async Task ConfirmarCargaAsync()
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
                    ? $"Se detectaron {faltantes} bulto(s) faltante(s) sin escanear:\n{codigosFaltantes}\n\nLos envíos que no tengan todos sus bultos cargados quedarán excluidos de la ruta activa y volverán a depósito.\n\n¿Desea informar la carga al depósito?"
                    : $"Se detectaron {sobrantes} bulto(s) sobrante(s).\n\n¿Desea registrar las discrepancias e informar la carga al depósito?";

                var continuar = await Shell.Current.DisplayAlertAsync(
                    "Confirmación con Discrepancias",
                    mensajeAlerta,
                    "Informar carga",
                    "Revisar Bultos");

                if (!continuar)
                    return;
            }
        }

        if (_biometricAuthService != null)
        {
            var autorizado = await _biometricAuthService.ValidarIdentidadAsync(
                "Autorizar Custodia de Carga",
                "Verifique su identidad para asumir la custodia de los bultos cargados.");

            if (!autorizado)
            {
                if (Shell.Current is not null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Operación Cancelada",
                        "No se completó la verificación de identidad. La custodia de la carga no fue confirmada.",
                        "Entendido");
                }
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

            var idempotencyKey = $"CARGA-{HojaDeRutaId:N}";
            var observaciones = (faltantes > 0 || sobrantes > 0)
                ? $"Discrepancia de carga: {faltantes} faltantes, {sobrantes} sobrantes. {MensajeDiscrepancia}".Trim()
                : "Carga completa y verificada sin discrepancias";

            // Offline-first: primero queda guardado en el dispositivo (la ruta pasa a "EnCarga", nunca a "Despachada")
            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.ConfirmarCargaLocalAsync(HojaDeRutaId, codigosCargados);
            }

            EstadoHojaDeRuta = "EnCarga";

            var payload = new ConfirmarCargaOfflinePayload
            {
                HojaDeRutaId = HojaDeRutaId,
                CodigosBultosCargados = codigosCargados,
                IdempotencyKey = idempotencyKey,
                ObservacionesDiscrepancia = observaciones
            };

            int opId = 0;
            if (_localDatabaseService is not null)
            {
                opId = await _localDatabaseService.EncolarOperacionAsync(TiposOperacionOffline.ConfirmarCarga, JsonSerializer.Serialize(payload));
            }

            bool enviadoOnline = false;
            if (_syncService?.IsOnline == true && _httpClientFactory is not null)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient("BackendApi");
                    var url = $"/api/mobile/v1/rutas/{HojaDeRutaId}/carga/confirmar";
                    var response = await client.PostAsJsonAsync(url, new
                    {
                        CodigosBultosCargados = codigosCargados,
                        IdempotencyKey = idempotencyKey,
                        ObservacionesDiscrepancia = observaciones
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        enviadoOnline = true;
                        if (_localDatabaseService is not null && opId != 0)
                        {
                            await _localDatabaseService.MarcarComoSincronizadoAsync(opId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Queda en la cola y se reenvía al recuperar la conexión
                    System.Diagnostics.Debug.WriteLine($"[CargaVehiculoViewModel] Confirmación de carga en cola: {ex.Message}");
                }
            }

            ActualizarConteos();

            if (Shell.Current is not null)
            {
                string detalleConexion = enviadoOnline
                    ? "La carga ya está informada al depósito. Falta que el despachador la verifique y despache la ruta."
                    : "Guardado en el dispositivo. Se enviará al depósito apenas haya conexión.";

                await Shell.Current.DisplayAlertAsync(
                    "Carga informada",
                    $"La carga del vehículo {Patente} quedó registrada.\n{detalleConexion}",
                    "Entendido");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
