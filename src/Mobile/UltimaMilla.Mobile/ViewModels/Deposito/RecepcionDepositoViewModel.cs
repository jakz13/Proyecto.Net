using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using UltimaMilla.Mobile.Models.DTOs;
using UltimaMilla.Mobile.Services.Auth;
using UltimaMilla.Mobile.Services.Sync;
using UltimaMilla.Mobile.Views.Carga;

namespace UltimaMilla.Mobile.ViewModels.Deposito;

public partial class BultoRecepcionItemViewModel : ObservableObject
{
    public Guid BultoId { get; set; }
    public string Identificacion { get; set; } = string.Empty;
    public decimal Peso { get; set; }
    public decimal Alto { get; set; }
    public decimal Ancho { get; set; }
    public decimal Profundo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeColor))]
    [NotifyPropertyChangedFor(nameof(BadgeTextColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(CardBgColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderColor))]
    private bool _escaneado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BadgeColor))]
    [NotifyPropertyChangedFor(nameof(BadgeTextColor))]
    [NotifyPropertyChangedFor(nameof(EstadoTexto))]
    [NotifyPropertyChangedFor(nameof(CardBgColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderColor))]
    [NotifyPropertyChangedFor(nameof(TextoBotonDanado))]
    [NotifyPropertyChangedFor(nameof(FondoBotonDanado))]
    [NotifyPropertyChangedFor(nameof(TextoColorBotonDanado))]
    private bool _danado;

    public string TextoBotonDanado => Danado ? "Quitar Daño" : "Marcar Dañado";
    public Color FondoBotonDanado => Danado ? Color.FromArgb("#FEF2F2") : Color.FromArgb("#F1F5F9");
    public Color TextoColorBotonDanado => Danado ? Color.FromArgb("#DC2626") : Color.FromArgb("#64748B");

    public string EstadoTexto
    {
        get
        {
            if (Danado) return "Dañado";
            if (Escaneado) return "Verificado";
            return "Pendiente";
        }
    }

    public Color BadgeColor
    {
        get
        {
            if (Danado) return Color.FromArgb("#FEE2E2");
            if (Escaneado) return Color.FromArgb("#DCFCE7");
            return Color.FromArgb("#F1F5F9");
        }
    }

    public Color BadgeTextColor
    {
        get
        {
            if (Danado) return Color.FromArgb("#B91C1C");
            if (Escaneado) return Color.FromArgb("#15803D");
            return Color.FromArgb("#64748B");
        }
    }

    public Color CardBgColor
    {
        get
        {
            if (Danado) return Color.FromArgb("#FFF5F5");
            if (Escaneado) return Color.FromArgb("#F0FDF4");
            return Color.FromArgb("#FFFFFF");
        }
    }

    public Color CardBorderColor
    {
        get
        {
            if (Danado) return Color.FromArgb("#FCA5A5");
            if (Escaneado) return Color.FromArgb("#86EFAC");
            return Color.FromArgb("#E2E8F0");
        }
    }
}

public partial class RecepcionDepositoViewModel : ObservableObject
{
    private readonly ISyncService? _syncService;
    private readonly IAuthService? _authService;
    private readonly ILogger<RecepcionDepositoViewModel>? _logger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _escaneandoConCamara;

    [ObservableProperty]
    private string _filtroBusqueda = string.Empty;

    [ObservableProperty]
    private string _codigoEscaneoInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneEnvioSeleccionado))]
    [NotifyPropertyChangedFor(nameof(SinEnvioSeleccionado))]
    [NotifyPropertyChangedFor(nameof(TituloEnvioSeleccionado))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarSinDiscrepancia))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarConDiscrepancia))]
    private EnvioAdmitidoMobileDto? _envioSeleccionado;

    public bool TieneEnvioSeleccionado => EnvioSeleccionado != null;
    public bool SinEnvioSeleccionado => EnvioSeleccionado == null;

    public string TituloEnvioSeleccionado => EnvioSeleccionado != null
        ? $"{EnvioSeleccionado.CodigoSeguimiento} - {EnvioSeleccionado.NombreDestinatario}"
        : "Ningún envío seleccionado";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarSinDiscrepancia))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarConDiscrepancia))]
    private bool _tieneDiscrepancias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarSinDiscrepancia))]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarConDiscrepancia))]
    [NotifyPropertyChangedFor(nameof(ModoNormalActivo))]
    private bool _modoDiscrepanciaActivo;

    public bool ModoNormalActivo => !ModoDiscrepanciaActivo;
    public bool TieneSobrantes => BultosSobrantes.Count > 0;

    [ObservableProperty]
    private string _tipoDiscrepancia = "Faltante";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeConfirmarConDiscrepancia))]
    private string _observacionesDiscrepancia = string.Empty;

    [ObservableProperty]
    private string _mensajeEstado = string.Empty;

    public ObservableCollection<EnvioAdmitidoMobileDto> EnviosAdmitidos { get; } = new();
    public ObservableCollection<EnvioAdmitidoMobileDto> EnviosFiltrados { get; } = new();
    public ObservableCollection<BultoRecepcionItemViewModel> BultosDeclarados { get; } = new();
    public ObservableCollection<string> BultosSobrantes { get; } = new();

    public int TotalBultosDeclarados => BultosDeclarados.Count;

    public int BultosVerificadosCount => BultosDeclarados.Count(b => b.Escaneado && !b.Danado);

    public int BultosDanadosCount => BultosDeclarados.Count(b => b.Danado);

    public int BultosFaltantesCount => BultosDeclarados.Count(b => !b.Escaneado);

    public int BultosSobrantesCount => BultosSobrantes.Count;

    public double ProgresoRecepcion => TotalBultosDeclarados == 0
        ? 0
        : (double)BultosVerificadosCount / TotalBultosDeclarados;

    public string ResumenProgresoTexto => TotalBultosDeclarados == 0
        ? "0 de 0 bultos verificados"
        : $"{BultosVerificadosCount} de {TotalBultosDeclarados} bultos verificados";

    public bool PuedeConfirmarSinDiscrepancia =>
        EnvioSeleccionado != null &&
        TotalBultosDeclarados > 0 &&
        BultosDeclarados.All(b => b.Escaneado && !b.Danado) &&
        BultosSobrantes.Count == 0 &&
        !ModoDiscrepanciaActivo;

    public bool PuedeConfirmarConDiscrepancia =>
        EnvioSeleccionado != null &&
        (TieneDiscrepancias || ModoDiscrepanciaActivo) &&
        !string.IsNullOrWhiteSpace(ObservacionesDiscrepancia);

    public RecepcionDepositoViewModel() : this(null, null, null)
    {
    }

    public RecepcionDepositoViewModel(
        ISyncService? syncService,
        IAuthService? authService,
        ILogger<RecepcionDepositoViewModel>? logger = null)
    {
        _syncService = syncService;
        _authService = authService;
        _logger = logger;
    }

    public async Task InicializarAsync()
    {
        if (EscaneandoConCamara)
            return;

        await CargarEnviosAdmitidosAsync();
    }

    [RelayCommand]
    public async Task CargarEnviosAdmitidosAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            MensajeEstado = "Consultando envíos admitidos para recepción...";

            if (_syncService == null)
            {
                MensajeEstado = "Servicio de sincronización no disponible.";
                return;
            }

            var envios = await _syncService.ObtenerEnviosAdmitidosDepositoAsync();

            EnviosAdmitidos.Clear();
            foreach (var e in envios)
            {
                EnviosAdmitidos.Add(e);
            }

            AplicarFiltro();

            MensajeEstado = EnviosAdmitidos.Count == 0
                ? "No hay envíos admitidos pendientes de recepción en depósito."
                : $"{EnviosAdmitidos.Count} envío(s) admitido(s) disponibles.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error al cargar envíos admitidos para recepción.");
            MensajeEstado = $"Error al cargar envíos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void AplicarFiltro()
    {
        EnviosFiltrados.Clear();
        var filtro = (FiltroBusqueda ?? string.Empty).Trim().ToUpperInvariant();

        foreach (var e in EnviosAdmitidos)
        {
            if (string.IsNullOrWhiteSpace(filtro) ||
                e.CodigoSeguimiento.ToUpperInvariant().Contains(filtro) ||
                e.NombreDestinatario.ToUpperInvariant().Contains(filtro) ||
                e.DireccionDestino.ToUpperInvariant().Contains(filtro) ||
                e.Bultos.Any(b => b.Identificacion.ToUpperInvariant().Contains(filtro)))
            {
                EnviosFiltrados.Add(e);
            }
        }
    }

    [RelayCommand]
    public void SeleccionarEnvio(EnvioAdmitidoMobileDto? envio)
    {
        if (envio == null) return;

        EnvioSeleccionado = envio;
        BultosDeclarados.Clear();
        BultosSobrantes.Clear();
        ModoDiscrepanciaActivo = false;
        TieneDiscrepancias = false;
        ObservacionesDiscrepancia = string.Empty;
        TipoDiscrepancia = "Faltante";

        foreach (var b in envio.Bultos)
        {
            BultosDeclarados.Add(new BultoRecepcionItemViewModel
            {
                BultoId = b.BultoId,
                Identificacion = b.Identificacion,
                Peso = b.Peso,
                Alto = b.Alto,
                Ancho = b.Ancho,
                Profundo = b.Profundo,
                Escaneado = false,
                Danado = false
            });
        }

        ActualizarConteos();
        MensajeEstado = $"Envío {envio.CodigoSeguimiento} seleccionado ({BultosDeclarados.Count} bultos declarados).";
    }

    [RelayCommand]
    public void LimpiarEnvioSeleccionado()
    {
        EnvioSeleccionado = null;
        BultosDeclarados.Clear();
        BultosSobrantes.Clear();
        ModoDiscrepanciaActivo = false;
        TieneDiscrepancias = false;
        ObservacionesDiscrepancia = string.Empty;
        CodigoEscaneoInput = string.Empty;
        ActualizarConteos();
        MensajeEstado = "Seleccione un envío admitido o escanee un bulto para comenzar.";
    }

    [RelayCommand]
    public async Task EscanearConCamaraAsync()
    {
        if (IsBusy) return;

        try
        {
            EscaneandoConCamara = true;
            var scannerPage = new EscanerCodigoPage();
            if (Shell.Current != null)
            {
                await Shell.Current.Navigation.PushModalAsync(scannerPage);
            }

            var codigo = await scannerPage.ResultadoEscaneoTask;
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                await ProcesarCodigoEscaneadoAsync(codigo);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error al abrir la cámara para escaneo de recepción.");
        }
        finally
        {
            EscaneandoConCamara = false;
        }
    }

    [RelayCommand]
    public async Task ValidarCodigoManualAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoEscaneoInput))
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync("Código Requerido", "Ingrese o escanee el código del bulto.", "Aceptar");
            }
            return;
        }

        var cod = CodigoEscaneoInput;
        CodigoEscaneoInput = string.Empty;
        await ProcesarCodigoEscaneadoAsync(cod);
    }

    public async Task ProcesarCodigoEscaneadoAsync(string codigoBruto)
    {
        var codigo = NormalizarCodigo(codigoBruto);
        if (string.IsNullOrWhiteSpace(codigo))
            return;

        // Si no hay envío seleccionado, buscar a qué envío admitido pertenece el código
        if (EnvioSeleccionado == null)
        {
            var coincidencia = EnviosAdmitidos.FirstOrDefault(e =>
                e.Bultos.Any(b => NormalizarCodigo(b.Identificacion) == codigo) ||
                NormalizarCodigo(e.CodigoSeguimiento) == codigo);

            if (coincidencia != null)
            {
                SeleccionarEnvio(coincidencia);
            }
            else
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Bulto No Reconocido",
                        $"El código '{codigo}' no corresponde a ningún envío en estado admitido actualmente.",
                        "Aceptar");
                }
                return;
            }
        }

        // Buscar en los bultos declarados del envío seleccionado
        var bultoPendiente = BultosDeclarados.FirstOrDefault(b => !b.Escaneado && NormalizarCodigo(b.Identificacion) == codigo);

        if (bultoPendiente != null)
        {
            bultoPendiente.Escaneado = true;
            ActualizarConteos();

            if (PuedeConfirmarSinDiscrepancia)
            {
                MensajeEstado = "Todos los bultos han sido verificados. Listo para confirmar recepción.";
            }
            else
            {
                MensajeEstado = $"Bulto {bultoPendiente.Identificacion} verificado.";
            }
            return;
        }

        // Si ya estaba escaneado
        var bultoYaEscaneado = BultosDeclarados.FirstOrDefault(b => b.Escaneado && NormalizarCodigo(b.Identificacion) == codigo);
        if (bultoYaEscaneado != null)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Bulto Ya Verificado",
                    $"El bulto '{bultoYaEscaneado.Identificacion}' ya fue escaneado para este envío.",
                    "Aceptar");
            }
            return;
        }

        // Si no coincide con ninguno declarado: Sobrante
        if (!BultosSobrantes.Contains(codigo))
        {
            BultosSobrantes.Add(codigo);
            ModoDiscrepanciaActivo = true;
            TipoDiscrepancia = "Sobrante";
            if (string.IsNullOrWhiteSpace(ObservacionesDiscrepancia))
            {
                ObservacionesDiscrepancia = $"Bulto sobrante detectado: {codigo}.";
            }
            else
            {
                ObservacionesDiscrepancia += $" Sobrante: {codigo}.";
            }
        }

        ActualizarConteos();

        if (Shell.Current != null)
        {
            var codEnvio = EnvioSeleccionado?.CodigoSeguimiento ?? "activo";
            await Shell.Current.DisplayAlertAsync(
                "Discrepancia: Bulto Sobrante",
                $"El código '{codigo}' no pertenece a los bultos declarados para el envío {codEnvio}. Se registró como sobrante.",
                "Aceptar");
        }
    }

    [RelayCommand]
    public void AlternarDanadoBulto(BultoRecepcionItemViewModel? bulto)
    {
        if (bulto == null) return;

        bulto.Danado = !bulto.Danado;
        if (bulto.Danado)
        {
            bulto.Escaneado = true; // Si está dañado, fue recibido físicamente
            ModoDiscrepanciaActivo = true;
            TipoDiscrepancia = "Bulto Dañado";
            if (!ObservacionesDiscrepancia.Contains(bulto.Identificacion))
            {
                ObservacionesDiscrepancia = string.IsNullOrWhiteSpace(ObservacionesDiscrepancia)
                    ? $"Bulto dañado: {bulto.Identificacion}."
                    : $"{ObservacionesDiscrepancia} Dañado: {bulto.Identificacion}.";
            }
        }

        ActualizarConteos();
    }

    [RelayCommand]
    public void ActivarModoDiscrepancia()
    {
        ModoDiscrepanciaActivo = true;
        if (string.IsNullOrWhiteSpace(ObservacionesDiscrepancia))
        {
            if (BultosFaltantesCount > 0)
                TipoDiscrepancia = "Faltante";
            else if (BultosSobrantesCount > 0)
                TipoDiscrepancia = "Sobrante";
            else if (BultosDanadosCount > 0)
                TipoDiscrepancia = "Bulto Dañado";
        }
        ActualizarConteos();
    }

    [RelayCommand]
    public async Task ConfirmarRecepcionAsync()
    {
        if (EnvioSeleccionado == null || IsBusy) return;

        try
        {
            IsBusy = true;
            MensajeEstado = "Registrando recepción en depósito...";

            var escaneados = BultosDeclarados.Where(b => b.Escaneado).Select(b => b.Identificacion).ToList();

            var ok = await _syncService!.RecepcionarBultosDepositoAsync(
                EnvioSeleccionado.EnvioId,
                EnvioSeleccionado.CodigoSeguimiento,
                escaneados,
                codigosDanados: null,
                tipoDiscrepancia: null,
                observaciones: null);

            if (ok)
            {
                var codEnvio = EnvioSeleccionado.CodigoSeguimiento;
                EnviosAdmitidos.Remove(EnvioSeleccionado);
                AplicarFiltro();
                LimpiarEnvioSeleccionado();

                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Recepción Confirmada",
                        $"El envío {codEnvio} fue recepcionado y pasó a estado 'En Depósito'.",
                        "Aceptar");
                }
            }
            else
            {
                MensajeEstado = "No se pudo registrar la recepción. Intente nuevamente.";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error al confirmar recepción en depósito.");
            MensajeEstado = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConfirmarRecepcionConDiscrepanciaAsync()
    {
        if (EnvioSeleccionado == null || IsBusy) return;

        if (string.IsNullOrWhiteSpace(ObservacionesDiscrepancia))
        {
            if (Shell.Current != null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Observaciones Requeridas",
                    "Debe documentar las observaciones de la discrepancia para completar la recepción.",
                    "Aceptar");
            }
            return;
        }

        try
        {
            IsBusy = true;
            MensajeEstado = "Registrando recepción documentada con discrepancias...";

            var escaneados = BultosDeclarados.Where(b => b.Escaneado).Select(b => b.Identificacion).ToList();
            escaneados.AddRange(BultosSobrantes);

            var danados = BultosDeclarados.Where(b => b.Danado).Select(b => b.Identificacion).ToList();

            var ok = await _syncService!.RecepcionarBultosDepositoAsync(
                EnvioSeleccionado.EnvioId,
                EnvioSeleccionado.CodigoSeguimiento,
                escaneados,
                codigosDanados: danados,
                tipoDiscrepancia: TipoDiscrepancia,
                observaciones: ObservacionesDiscrepancia.Trim());

            if (ok)
            {
                var codEnvio = EnvioSeleccionado.CodigoSeguimiento;
                EnviosAdmitidos.Remove(EnvioSeleccionado);
                AplicarFiltro();
                LimpiarEnvioSeleccionado();

                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlertAsync(
                        "Recepción Documentada",
                        $"La recepción del envío {codEnvio} fue registrada con discrepancias documentadas y pasó a estado 'En Depósito'.",
                        "Aceptar");
                }
            }
            else
            {
                MensajeEstado = "No se pudo registrar la recepción con discrepancia.";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error al confirmar recepción con discrepancia en depósito.");
            MensajeEstado = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ActualizarConteos()
    {
        TieneDiscrepancias = BultosFaltantesCount > 0 || BultosSobrantesCount > 0 || BultosDanadosCount > 0;

        OnPropertyChanged(nameof(TotalBultosDeclarados));
        OnPropertyChanged(nameof(BultosVerificadosCount));
        OnPropertyChanged(nameof(BultosDanadosCount));
        OnPropertyChanged(nameof(BultosFaltantesCount));
        OnPropertyChanged(nameof(BultosSobrantesCount));
        OnPropertyChanged(nameof(TieneSobrantes));
        OnPropertyChanged(nameof(ModoNormalActivo));
        OnPropertyChanged(nameof(ProgresoRecepcion));
        OnPropertyChanged(nameof(ResumenProgresoTexto));
        OnPropertyChanged(nameof(PuedeConfirmarSinDiscrepancia));
        OnPropertyChanged(nameof(PuedeConfirmarConDiscrepancia));
    }

    private static string NormalizarCodigo(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return string.Empty;

        var limpio = codigo.Trim();
        foreach (var prefijo in new[] { "BULTO:", "BULTO-", "CODIGO:", "TRACKING:", "ENVIO:" })
        {
            if (limpio.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                limpio = limpio[prefijo.Length..].Trim();
            }
        }

        return limpio.ToUpperInvariant();
    }
}
