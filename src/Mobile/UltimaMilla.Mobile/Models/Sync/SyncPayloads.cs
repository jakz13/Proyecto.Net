namespace UltimaMilla.Mobile.Models.Sync;

public static class TiposOperacionOffline
{
    public const string ConfirmarCarga = "CONFIRMAR_CARGA";
    public const string SeleccionarParada = "SELECCIONAR_PARADA";
    public const string EntregaExitosa = "ENTREGA_EXITOSA";
    public const string EntregaParcial = "ENTREGA_PARCIAL";
    public const string FalloEntrega = "FALLO_ENTREGA";
    public const string RetiroRealizado = "RETIRO_REALIZADO";
    public const string RetiroNoRealizado = "RETIRO_NO_REALIZADO";
    public const string RetiroDevolucion = "RETIRO_DEVOLUCION";
    public const string EventoGeocerca = "EVENTO_GEOCERCA";
    public const string IniciarRecorrido = "INICIAR_RECORRIDO";
    public const string TelemetriaGps = "TELEMETRIA_GPS";
    public const string FinalizarHojaDeRuta = "FINALIZAR_HOJA_DE_RUTA";
    public const string RecepcionDeposito = "RECEPCION_DEPOSITO";
}

public class RecepcionDepositoOfflinePayload
{
    public Guid EnvioId { get; set; }
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public List<string> CodigosBultosEscaneados { get; set; } = new();
    public List<string>? CodigosBultosDanados { get; set; }
    public string? TipoDiscrepancia { get; set; }
    public string? Observaciones { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime FechaRecepcionUtc { get; set; } = DateTime.UtcNow;
}

public class FinalizarHojaDeRutaOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public List<string>? CodigosBultosDevueltos { get; set; }
    public string? MotivoInternoCodigo { get; set; }
    public string? Observaciones { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime FechaFinalizacionUtc { get; set; } = DateTime.UtcNow;
}

public class IniciarRecorridoOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime FechaInicioUtc { get; set; } = DateTime.UtcNow;
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

/// <summary>Un bulto que no se entregó en una entrega parcial, con su motivo (RF 31 a 36).</summary>
public class BultoNoEntregadoOfflinePayload
{
    public string CodigoBulto { get; set; } = string.Empty;
    public string? MotivoCodigo { get; set; }
    public Guid? MotivoNoEntregaId { get; set; }
    public string? Observaciones { get; set; }
}

/// <summary>Entrega parcial por bultos: se entregan algunos y el resto queda pendiente con su motivo.</summary>
public class EntregaParcialOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid EnvioId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public List<string> CodigosBultosEntregados { get; set; } = new();
    public List<BultoNoEntregadoOfflinePayload> BultosNoEntregados { get; set; } = new();
    public string NombreReceptor { get; set; } = string.Empty;
    public string DocumentoReceptor { get; set; } = string.Empty;
    public string? FirmaDigitalBase64 { get; set; }
    public string? FotoEvidenciaBase64 { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public DateTime? FechaHoraDispositivoUtc { get; set; }
}

/// <summary>Retiro en domicilio realizado: bultos escaneados, foto de su estado y firma de quien los entrega (RF 40).</summary>
public class RetiroRealizadoOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid RetiroId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public List<string> CodigosBultosEscaneados { get; set; } = new();
    public string? FotoEstadoBase64 { get; set; }
    public string? FirmaBase64 { get; set; }
    public string NombreEntrega { get; set; } = string.Empty;
    public string? DocumentoEntrega { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public DateTime? FechaHoraDispositivoUtc { get; set; }
}

/// <summary>Retiro en domicilio que no se pudo realizar, con un motivo del catálogo del operador.</summary>
public class RetiroNoRealizadoOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid RetiroId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? MotivoCodigo { get; set; }
    public Guid? MotivoNoEntregaId { get; set; }
    public string? Observaciones { get; set; }
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

public class RetiroDevolucionOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid EnvioId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public DateTime? FechaHoraDispositivoUtc { get; set; }
    public string? Observaciones { get; set; }
}

public class EventoGeocercaOfflinePayload
{
    public int? EventoLocalId { get; set; }
    public Guid HojaDeRutaId { get; set; }
    public Guid ParadaId { get; set; }
    public Guid EnvioId { get; set; }
    public string CodigoSeguimiento { get; set; } = string.Empty;
    public string TipoEvento { get; set; } = string.Empty;
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double DistanciaMetros { get; set; }
    public int? TiempoPermanenciaSegundos { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class TelemetriaGpsOfflinePayload
{
    public Guid HojaDeRutaId { get; set; }
    public Guid RepartidorId { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public double? PrecisionMetros { get; set; }
    public DateTime TimestampUtc { get; set; }
    public int? BateriaPct { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
