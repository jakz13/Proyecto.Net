namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Parámetros para declarar formalmente un envío como extraviado (UC-43, RF-11, RF-12).
/// Precondición: El envío debe encontrarse en estado 'EnDeposito' o 'EnTransito'.
/// </summary>
public record DeclararExtraviadoDto
{
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string MotivoExtravio { get; init; } = string.Empty;
    public string? ObservacionesDetalladas { get; init; }
    public bool CrearIncidenciaOperativa { get; init; } = true;
    public string? NumeroIncidenciaExistente { get; init; }
    public bool NotificarComercio { get; init; } = true;
    public string DespachadorUsuario { get; init; } = string.Empty;
}
