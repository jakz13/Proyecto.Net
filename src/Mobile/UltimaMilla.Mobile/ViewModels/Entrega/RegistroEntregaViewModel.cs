using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.Models.Local;
using UltimaMilla.Mobile.Models.Sync;
using UltimaMilla.Mobile.Services.Realtime;
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
[QueryProperty(nameof(ModoFallo), "ModoFallo")]
public partial class RegistroEntregaViewModel : EvidenciaViewModelBase
{
    private readonly ILocalDatabaseService? _localDatabaseService;
    private readonly ISyncService? _syncService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsRetiro))]
    [NotifyPropertyChangedFor(nameof(MostrarFormularioEvidencia))]
    [NotifyPropertyChangedFor(nameof(MostrarFormularioNoRetirado))]
    [NotifyPropertyChangedFor(nameof(TituloGuardar))]
    private ParadaItemViewModel? _paradaSeleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarFormularioEvidencia))]
    [NotifyPropertyChangedFor(nameof(MostrarFormularioNoRetirado))]
    [NotifyPropertyChangedFor(nameof(TabExitosoBgColor))]
    [NotifyPropertyChangedFor(nameof(TabExitosoTextColor))]
    [NotifyPropertyChangedFor(nameof(TabExitosoBorderColor))]
    [NotifyPropertyChangedFor(nameof(TabFalloBgColor))]
    [NotifyPropertyChangedFor(nameof(TabFalloTextColor))]
    [NotifyPropertyChangedFor(nameof(TabFalloBorderColor))]
    private bool _modoFallo;

    public bool EsRetiro => ParadaSeleccionada?.EsRetiro == true;
    public bool MostrarFormularioEvidencia => !EsRetiro || !ModoFallo;
    public bool MostrarFormularioNoRetirado => EsRetiro && ModoFallo;

    public Color TabExitosoBgColor => !ModoFallo ? Color.FromArgb("#16A34A") : Color.FromArgb("#F8FAFC");
    public Color TabExitosoTextColor => !ModoFallo ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#475569");
    public Color TabExitosoBorderColor => !ModoFallo ? Color.FromArgb("#16A34A") : Color.FromArgb("#CBD5E1");

    public Color TabFalloBgColor => ModoFallo ? Color.FromArgb("#DC2626") : Color.FromArgb("#FEF2F2");
    public Color TabFalloTextColor => ModoFallo ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#DC2626");
    public Color TabFalloBorderColor => ModoFallo ? Color.FromArgb("#DC2626") : Color.FromArgb("#EF4444");

    [RelayCommand]
    private void SeleccionarModoRetiroExitoso() => ModoFallo = false;

    [RelayCommand]
    private void SeleccionarModoNoRetirado() => ModoFallo = true;

    [ObservableProperty]
    private string _nombreReceptor = string.Empty;

    [ObservableProperty]
    private string _documentoReceptor = string.Empty;

    partial void OnDocumentoReceptorChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        var soloDigitos = new string(value.Where(char.IsDigit).Take(8).ToArray());
        if (soloDigitos != value)
        {
            DocumentoReceptor = soloDigitos;
        }
    }

    [ObservableProperty]
    private bool _requiereFirma = true;

    [ObservableProperty]
    private bool _requiereFoto;

    [ObservableProperty]
    private bool _requiereDocumento = true;

    [ObservableProperty]
    private string _textoEtiquetaFirma = "Firma Digital en Pantalla (Obligatorio según regla del operador) *";

    [ObservableProperty]
    private string _textoEtiquetaFoto = "Evidencia Fotográfica (Opcional)";

    [ObservableProperty]
    private string _textoEtiquetaDocumento = "DOCUMENTO / C.I. (Obligatorio según regla del operador) *";

    [ObservableProperty]
    private string _observacionesPendientes = string.Empty;

    protected override IEnumerable<string> ObtenerOpcionesMotivoModal() => MotivosNoEntrega;
    protected override string TituloSelectorMotivo => "Motivo de bultos no entregados";
    protected override string SubtituloSelectorMotivo => "Seleccione la causa para los bultos pendientes";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResumenBultosTexto))]
    [NotifyPropertyChangedFor(nameof(HayBultosNoEntregadosEnDomicilio))]
    [NotifyPropertyChangedFor(nameof(EsEntregaParcial))]
    [NotifyPropertyChangedFor(nameof(TituloGuardar))]
    private int _bultosMarcados;

    public ObservableCollection<BultoEntregaItemViewModel> Bultos { get; } = new();

    public ObservableCollection<string> MotivosNoEntrega { get; } = new();

    private readonly List<MotivoNoEntregaLocal> _motivosCatalogo = new();

    public int CantidadBultosFaltantesEnCarga => ParadaSeleccionada?.CantidadBultosFaltantesEnCarga ?? 0;

    public bool TieneBultosFaltantesDesdeCarga => !EsRetiro && CantidadBultosFaltantesEnCarga > 0;

    public string AvisoFaltanteEnCargaTexto => CantidadBultosFaltantesEnCarga == 1
        ? "1 bulto faltó durante la carga en depósito y no viaja en el vehículo; se registrará como entrega parcial."
        : $"{CantidadBultosFaltantesEnCarga} bultos faltaron durante la carga en depósito y no viajan en el vehículo; se registrará como entrega parcial.";

    /// <summary>La selección por bulto solo se ofrece si el operador tiene habilitada la entrega parcial.</summary>
    public bool TieneVariosBultos => !EsRetiro
        && (Bultos.Count > 1 || TieneBultosFaltantesDesdeCarga)
        && (_syncService?.FuncionalidadHabilitada("entrega-parcial") ?? true);

    public bool HayBultosNoEntregadosEnDomicilio => !EsRetiro && Bultos.Count > 0 && BultosMarcados < Bultos.Count;

    public bool EsEntregaParcial => !EsRetiro && Bultos.Count > 0 && BultosMarcados > 0
        && (BultosMarcados < Bultos.Count || TieneBultosFaltantesDesdeCarga);

    public string ResumenBultosTexto => Bultos.Count == 0
        ? "Esta parada no tiene bultos informados."
        : (TieneBultosFaltantesDesdeCarga
            ? $"{BultosMarcados} de {Bultos.Count} bulto(s) cargados se entregan ahora"
            : $"{BultosMarcados} de {Bultos.Count} bulto(s) se entregan ahora");

    public string TituloGuardar => EsRetiro
        ? "Confirmar retiro"
        : (EsEntregaParcial ? "Guardar entrega parcial" : "Guardar entrega");

    public RegistroEntregaViewModel() : this(null, null)
    {
    }

    protected override Guid HojaDeRutaVigilada => ParadaSeleccionada?.HojaDeRutaId ?? Guid.Empty;

    public RegistroEntregaViewModel(ILocalDatabaseService? localDatabaseService, ISyncService? syncService = null, IRealtimeTrackingService? realtimeTrackingService = null)
    {
        _localDatabaseService = localDatabaseService;
        _syncService = syncService;
        ConfigurarVigilanciaDeRuta(realtimeTrackingService, localDatabaseService);
        CargarReglasOperativas();
    }

    public void CargarReglasOperativas()
    {
        var regla = (Preferences.Default.Get("ReglaOperativa_TipoPruebaEntrega", "Firma digital y documento del receptor") ?? "").ToUpperInvariant();
        var esCompleta = regla.Contains("COMPLETA") || regla.Contains("TODOS") || regla.Contains("AMBOS");

        RequiereFirma = esCompleta || regla.Contains("FIRMA") || (!regla.Contains("FOTO") && !regla.Contains("PIN") && !regla.Contains("DOCUMENTO"));
        RequiereFoto = esCompleta || regla.Contains("FOTO");
        RequiereDocumento = true;

        TextoEtiquetaFirma = RequiereFirma
            ? "Firma Digital en Pantalla (Obligatorio según regla del operador) *"
            : "Firma Digital en Pantalla (Opcional)";

        TextoEtiquetaFoto = RequiereFoto
            ? "Evidencia Fotográfica (Obligatorio según regla del operador) *"
            : "Evidencia Fotográfica (Opcional)";

        TextoEtiquetaDocumento = "DOCUMENTO / C.I. (Obligatorio según regla del operador) *";
    }

    partial void OnParadaSeleccionadaChanged(ParadaItemViewModel? value)
    {
        if (value is null)
            return;

        CargarReglasOperativas();

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

        ObservacionesPendientes = string.Empty;
        OnPropertyChanged(nameof(CantidadBultosFaltantesEnCarga));
        OnPropertyChanged(nameof(TieneBultosFaltantesDesdeCarga));
        OnPropertyChanged(nameof(AvisoFaltanteEnCargaTexto));
        OnPropertyChanged(nameof(TieneVariosBultos));
        RecalcularMarcados();
        OnPropertyChanged(nameof(HayBultosNoEntregadosEnDomicilio));
        OnPropertyChanged(nameof(EsEntregaParcial));
        OnPropertyChanged(nameof(TituloGuardar));
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

            SincronizarOpcionesMotivoTarjetas();
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
                codigo = await SolicitarCodigoManualAsync(
                    "Ingreso manual",
                    "Ingrese el código del bulto:",
                    "Código del bulto");
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
    private async Task ConfirmarRetiroNoRealizadoAsync()
    {
        if (IsBusy)
            return;

        var parada = ParadaSeleccionada;
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
                "Seleccione el motivo por el que no se pudo realizar el retiro.");
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
                Observaciones = string.IsNullOrWhiteSpace(ObservacionesPendientes) ? null : ObservacionesPendientes.Trim(),
                Latitud = LatitudActual,
                Longitud = LongitudActual,
                FechaHoraDispositivoUtc = FechaHoraDispositivoUtc
            };

            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync(
                    TiposOperacionOffline.RetiroNoRealizado,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
                await _localDatabaseService.ActualizarEstadoParadaLocalAsync(parada.Id, "Fallido");
            }

            parada.Estado = "Fallido";

            if (_syncService?.IsOnline == true)
            {
                try
                {
                    await _syncService.SincronizarOperacionesPendientesAsync();
                    await _syncService.DescargarYActualizarRutasDelServidorAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Sincronización retiro no realizado: {ex.Message}");
                }
            }

            await AvisarAsync(
                "Retiro no realizado",
                $"Quedó registrado el motivo: {motivo.Descripcion}.",
                subtitulo: "Actualizado",
                textoBoton: "Volver a la Ruta",
                navegarAlCerrarRuta: "//RutaDiaPage");
        }
        finally
        {
            IsBusy = false;
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

        if (!System.Text.RegularExpressions.Regex.IsMatch(DocumentoReceptor.Trim(), @"^\d{8}$"))
        {
            await AvisarAsync("Documento inválido", "El documento del receptor debe contener exactamente 8 dígitos numéricos.");
            return;
        }

        if (RequiereFirma && !TieneFirma)
        {
            await AvisarAsync("Firma requerida", "La regla operativa del operador exige la firma digital del receptor.");
            return;
        }

        if (RequiereFoto && !TieneFoto)
        {
            await AvisarAsync("Fotografía requerida", "La regla operativa del operador exige capturar una fotografía como evidencia de entrega.");
            return;
        }

        if (Bultos.Count > 0 && BultosMarcados == 0)
        {
            await AvisarAsync("Ningún bulto entregado", "Si no se entregó ningún bulto, registre un intento fallido desde el detalle de la parada.");
            return;
        }

        var parcial = EsEntregaParcial;
        var faltanBultosEnDomicilio = HayBultosNoEntregadosEnDomicilio;
        MotivoNoEntregaLocal? motivo = null;
        if (faltanBultosEnDomicilio)
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

            string? firmaBase64 = null;
            if (TieneFirma)
            {
                firmaBase64 = await ObtenerFirmaBase64Async();
                if (RequiereFirma && firmaBase64 is null)
                {
                    await AvisarAsync("Firma no disponible", "No se pudo capturar la firma dibujada. Límpiela y vuelva a firmar.");
                    return;
                }
            }
            else if (RequiereFirma)
            {
                await AvisarAsync("Firma requerida", "La regla operativa del operador exige la firma digital del receptor.");
                return;
            }

            var fotoBase64 = TieneFoto ? FotoEvidenciaBase64 : null;
            if (RequiereFoto && string.IsNullOrWhiteSpace(fotoBase64))
            {
                await AvisarAsync("Fotografía requerida", "La regla operativa del operador exige capturar una fotografía como evidencia de entrega.");
                return;
            }
            var estadoAnterior = parada.Estado;
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
                    BultosNoEntregados = faltanBultosEnDomicilio && motivo is not null
                        ? Bultos.Where(b => !b.Entregado).Select(b => new BultoNoEntregadoOfflinePayload
                        {
                            CodigoBulto = b.Codigo,
                            MotivoCodigo = motivo.Codigo,
                            MotivoNoEntregaId = motivo.Id,
                            Observaciones = string.IsNullOrWhiteSpace(ObservacionesPendientes) ? null : ObservacionesPendientes.Trim()
                        }).ToList()
                        : new List<BultoNoEntregadoOfflinePayload>(),
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

            parada.Estado = parcial ? "EntregadaParcialmente" : "Entregado";

            bool? syncExitoso = null;
            string? motivoRechazoServidor = null;

            // Si hay conexión, se intenta sincronizar dentro de una ventana acotada para reflejar
            // de forma limpia y corporativa si el servidor aceptó o rechazó la operación.
            if (_syncService?.IsOnline == true)
            {
                try
                {
                    var syncTask = _syncService.SincronizarOperacionesPendientesAsync();
                    var completada = await Task.WhenAny(syncTask, Task.Delay(TimeSpan.FromSeconds(6)));
                    if (completada == syncTask)
                    {
                        var resultado = await syncTask;
                        if (resultado.ErroresNegocio > 0)
                        {
                            syncExitoso = false;
                            motivoRechazoServidor = resultado.DetallesErrores is { Count: > 0 }
                                ? string.Join("\n", resultado.DetallesErrores.Take(3))
                                : resultado.Mensaje;
                        }
                        else
                        {
                            syncExitoso = true;
                            await _syncService.DescargarYActualizarRutasDelServidorAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Sincronización inmediata: {ex.Message}");
                }
            }

            if (syncExitoso == false && !string.IsNullOrWhiteSpace(motivoRechazoServidor))
            {
                parada.Estado = string.IsNullOrWhiteSpace(estadoAnterior) ? "Pendiente" : estadoAnterior;
                if (_localDatabaseService is not null)
                {
                    await _localDatabaseService.RevertirEstadoParadaLocalAsync(parada.Id, parada.Estado);
                }

                if (_syncService?.IsOnline == true)
                {
                    try
                    {
                        await _syncService.DescargarYActualizarRutasDelServidorAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RegistroEntregaViewModel] Refresco tras rechazo: {ex.Message}");
                    }
                }

                await AvisarAsync(
                    "Observación del Servidor",
                    motivoRechazoServidor,
                    subtitulo: "La central no aceptó la operación",
                    textoBoton: "Entendido",
                    navegarAlCerrarRuta: "//RutaDiaPage");
            }
            else
            {
                var infoRed = syncExitoso == true
                    ? "Sincronizada correctamente con la central."
                    : (_syncService?.IsOnline == true
                        ? "Se está enviando a la central en segundo plano."
                        : "Guardado en el dispositivo. Se enviará automáticamente al recuperar la conexión.");
                var titulo = parcial ? "Entrega Parcial Registrada" : "Entrega Registrada";
                var detalle = parcial
                    ? (TieneBultosFaltantesDesdeCarga && !faltanBultosEnDomicilio
                        ? $"Se entregaron los {BultosMarcados} bulto(s) cargados a {NombreReceptor.Trim()} ({CantidadBultosFaltantesEnCarga} faltante(s) desde la carga)."
                        : $"Se entregaron {BultosMarcados} de {Bultos.Count} bulto(s) cargados a {NombreReceptor.Trim()}. El resto queda pendiente según la política del operador.")
                    : $"Entrega registrada para {NombreReceptor.Trim()}.";

                await AvisarAsync(
                    titulo,
                    $"{detalle}\n\n{infoRed}",
                    subtitulo: syncExitoso == true ? "Confirmada en central" : "Respaldo local cifrado",
                    textoBoton: "Volver a la Ruta",
                    navegarAlCerrarRuta: "//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
