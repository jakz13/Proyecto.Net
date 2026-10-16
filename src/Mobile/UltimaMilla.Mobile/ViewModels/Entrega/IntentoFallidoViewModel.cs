using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimaMilla.Mobile.Data;
using UltimaMilla.Mobile.ViewModels.Ruta;

namespace UltimaMilla.Mobile.ViewModels.Entrega;

[QueryProperty(nameof(Parada), "Parada")]
public partial class IntentoFallidoViewModel : ObservableObject
{
    private readonly ILocalDatabaseService? _localDatabaseService;

    [ObservableProperty]
    private ParadaItemViewModel? _parada;

    [ObservableProperty]
    private string _motivoSeleccionado = string.Empty;

    [ObservableProperty]
    private string _observaciones = string.Empty;

    [ObservableProperty]
    private bool _tieneFoto;

    [ObservableProperty]
    private string _fotoStatusText = "Ninguna foto adjunta";

    [ObservableProperty]
    private string _coordenadasGps = "-34.9124, -56.1589 (Precisión ±4m)";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<string> MotivosNoEntrega { get; } = new()
    {
        "Cliente ausente / No atiende llamadas",
        "Dirección incorrecta o inexistente",
        "Negocio o comercio cerrado",
        "Rechazado expresamente por el cliente",
        "Zona inaccesible o fuerza mayor",
        "Bulto dañado / Avería en tránsito",
        "Problemas con medio de pago / Cobro"
    };

    public IntentoFallidoViewModel() : this(null)
    {
    }

    public IntentoFallidoViewModel(ILocalDatabaseService? localDatabaseService)
    {
        _localDatabaseService = localDatabaseService;

        // Mock para previsualización directa
        _parada = new ParadaItemViewModel
        {
            Orden = 2,
            Direccion = "Bvar. España 2678, Apto 501",
            Ciudad = "Pocitos, Montevideo",
            Destinatario = "Lucía Fernández",
            Telefono = "+598 99 765 432",
            CantidadBultos = 1,
            Estado = "Pendiente",
            HoraEstimada = "10:15",
            CodigoSeguimiento = "ENV-2026-00109"
        };
    }

    [RelayCommand]
    private async Task TomarFotoEvidenciaAsync()
    {
        if (IsBusy)
            return;

        // Simulación de captura fotográfica en terreno
        TieneFoto = true;
        FotoStatusText = "Evidencia adjunta: fachada_visita_fallida_01.jpg";

        if (Shell.Current is not null)
        {
            await Shell.Current.DisplayAlertAsync(
                "Cámara de Evidencia",
                "Fotografía de fachada/timbre capturada y geolocalizada con éxito para respaldo del intento de entrega.",
                "Continuar");
        }
    }

    [RelayCommand]
    private async Task RegistrarIncidenciaAsync()
    {
        if (IsBusy)
            return;

        // 1. Validaciones ergonómicas
        if (string.IsNullOrWhiteSpace(MotivoSeleccionado))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Motivo requerido",
                    "Por favor, seleccione el motivo de no entrega del listado para cumplir con el protocolo UC-21.",
                    "Entendido");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(Observaciones))
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Observaciones requeridas",
                    "Por favor, detalle brevemente la situación ocurrida en el domicilio antes de registrar la incidencia.",
                    "Entendido");
            }
            return;
        }

        try
        {
            IsBusy = true;

            // 2. Construcción de payload para cola local cifrada (RNF 6.11)
            var payload = JsonSerializer.Serialize(new
            {
                ParadaId = Parada?.Id ?? Guid.NewGuid(),
                CodigoSeguimiento = Parada?.CodigoSeguimiento ?? "ENV-FALLO-MOCK",
                Motivo = MotivoSeleccionado,
                Observaciones = Observaciones.Trim(),
                TieneEvidenciaFoto = TieneFoto,
                ArchivoFoto = TieneFoto ? "fachada_visita_fallida_01.jpg" : null,
                CoordenadasGps = CoordenadasGps,
                FechaUtc = DateTime.UtcNow
            });

            if (_localDatabaseService is not null)
            {
                await _localDatabaseService.EncolarOperacionAsync("FALLO_ENTREGA", payload);
            }

            // 3. Actualizar estado local de la parada
            if (Parada is not null)
            {
                Parada.Estado = "Fallido";
            }

            // 4. Feedback visual y sonoro al repartidor
            if (Shell.Current is not null)
            {
                await Shell.Current.DisplayAlertAsync(
                    "Incidencia Registrada (UC-21)",
                    $"El intento fallido por '{MotivoSeleccionado}' se guardó en la cola offline cifrada y será sincronizado automáticamente.",
                    "Aceptar");

                // Retornar a la lista principal de ruta
                await Shell.Current.GoToAsync("//RutaDiaPage");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
