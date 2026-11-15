namespace UltimaMilla.Frontend.Contracts.Modelos.Despacho;

public record ParadaRetiroVerificacionDto
{
    public int Orden { get; init; }
    public Guid RetiroId { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Contacto { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public int CantidadBultos { get; init; }
    public decimal PesoKg { get; init; }
    public decimal VolumenM3 { get; init; }
}

/// <summary>
/// Información completa de la hoja de ruta para el control de salida por el operario de depósito.
/// </summary>
public record VerificacionDespachoDto
{
    public Guid HojaDeRutaId { get; init; }
    public string NumeroRuta { get; init; } = string.Empty;
    public DateOnly Fecha { get; init; }
    public int ViajeNumero { get; init; } = 1;
    public string Turno { get; init; } = "Mañana";
    public string EstadoRuta { get; init; } = "Liberada";

    public Guid RepartidorId { get; init; }
    public string RepartidorNombre { get; init; } = string.Empty;

    public Guid VehiculoId { get; init; }
    public string VehiculoMatricula { get; init; } = string.Empty;
    public string VehiculoModelo { get; init; } = string.Empty;
    public decimal CapacidadPesoMaxKg { get; init; }
    public decimal CapacidadVolumenMaxM3 { get; init; }

    public bool TieneRutaPendienteDeRendir { get; init; }
    public string? NumeroRutaPendienteRendicion { get; init; }

    public List<BultoVerificacionDto> Bultos { get; init; } = new();

    /// <summary>Paradas de retiro de la ruta: no se cargan en el depósito, pero forman parte del recorrido y se listan antes de confirmar la salida.</summary>
    public List<ParadaRetiroVerificacionDto> ParadasRetiro { get; init; } = new();

    public int TotalBultos => Bultos.Count;
    public int BultosCargados => Bultos.Count(b => b.CargadoEfectivo);
    public int BultosDiscrepantes => Bultos.Count(b => !b.CargadoEfectivo);
}
