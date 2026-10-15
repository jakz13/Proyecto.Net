namespace UltimaMilla.Frontend.Contracts.Modelos.ReglasOperativas;

/// <summary>
/// Parámetros para simular la aplicación operativa de las reglas vigentes sobre un envío en curso (UC-04, RF-04).
/// </summary>
public record SimularReglaOperativaDto
{
    public string ModalidadServicio { get; set; } = "Estándar 24-48h";
    public DateTime FechaDespacho { get; set; } = DateTime.Now.AddHours(-18);
    public int IntentosRealizados { get; set; } = 1;
    public DateTime? FechaUltimoIntento { get; set; } = DateTime.Now.AddHours(-4);
}

/// <summary>
/// Resultado del análisis de aplicación de reglas operativas para un envío simulado.
/// </summary>
public record ResultadoSimulacionReglasDto
{
    public bool Exitoso { get; init; } = true;
    public int VersionAplicada { get; init; }
    public int MaxIntentosPermitidos { get; init; }
    public int IntentosRestantes { get; init; }
    public bool SuperaMaximoIntentos { get; init; }
    public DateTime? ProximaFechaReintentoPermitida { get; init; }
    public bool PuedeReintentarAhora { get; init; }
    public DateTime FechaLimiteSla { get; init; }
    public bool SlaVencido { get; init; }
    public string TipoPruebaExigida { get; init; } = string.Empty;
    public string PoliticaDevolucion { get; init; } = string.Empty;
    public string AccionSugerida { get; init; } = string.Empty;
}
