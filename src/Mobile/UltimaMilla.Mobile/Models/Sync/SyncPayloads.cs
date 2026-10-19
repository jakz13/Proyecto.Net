namespace UltimaMilla.Mobile.Models.Sync;

public static class TiposOperacionOffline
{
    public const string ConfirmarCarga = "CONFIRMAR_CARGA";
    public const string SeleccionarParada = "SELECCIONAR_PARADA";
    public const string EntregaExitosa = "ENTREGA_EXITOSA";
    public const string FalloEntrega = "FALLO_ENTREGA";
}

public class ConfirmarCargaOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public List<string> CodigosBultosCargados { get; set; } = new();
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ObservacionesDiscrepancia { get; set; }
}

public class SeleccionarParadaOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public double? LatitudActual { get; set; }
    public double? LongitudActual { get; set; }
    public DateTime? Timestamp { get; set; }
    public string Accion { get; set; } = "IniciarNavegacion";
}

public class EntregaExitosaOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid EnvioId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public string DocumentoReceptor { get; set; } = string.Empty;
    public string? FirmaDigitalBase64 { get; set; }
    public string? FotoEvidenciaBase64 { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public DateTime? FechaHoraDispositivoUtc { get; set; }
}

public class IntentoFallidoOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid EnvioId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? MotivoNoEntregaId { get; set; }
    public string? MotivoCodigo { get; set; }
    public string Observaciones { get; set; } = string.Empty;
    public string? FotoEvidenciaBase64 { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public DateTime? FechaHoraDispositivoUtc { get; set; }
}
