namespace UltimaMilla.Frontend.Contracts.Modelos.Rutas;

/// <summary>
/// Parámetros para retirar, reasignar o reprogramar un envío incluido en una hoja de ruta (UC-10, RF-13, RF-15).
/// </summary>
public record ReasignarEnvioDto
{
    public Guid HojaDeRutaActualId { get; init; }
    public Guid EnvioId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;

    /// <summary>
    /// Motivo de la incidencia: "AveriaVehiculo", "AusenciaRepartidor", "IncidenciaVial", "SolicitudCliente", "AjusteItinerario".
    /// </summary>
    public string MotivoIncidencia { get; init; } = "AveriaVehiculo";
    public string? DetalleMotivo { get; init; }

    /// <summary>
    /// Acción seleccionada: "ReasignarOtraRuta", "ReprogramarFranja", "VolverADeposito".
    /// </summary>
    public string AccionDestino { get; init; } = "VolverADeposito";

    /// <summary>
    /// Si la acción es "ReasignarOtraRuta", identificador de la nueva hoja de ruta de destino.
    /// </summary>
    public Guid? HojaDeRutaDestinoId { get; init; }

    /// <summary>
    /// Si la acción es "ReprogramarFranja", nueva fecha y franja horaria pactadas.
    /// </summary>
    public DateOnly? NuevaFecha { get; init; }
    public string? NuevaFranjaHoraria { get; init; }
    public string? NuevoTurno { get; init; }

    /// <summary>
    /// Si la ruta ya estaba despachada (en tránsito), código interno del catálogo de motivos de no entrega.
    /// </summary>
    public string? CodigoMotivoNoEntrega { get; init; }
}
