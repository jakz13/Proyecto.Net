namespace UltimaMilla.Frontend.Contracts.Modelos.Liquidaciones;

/// <summary>Liquidación de los envíos entregados a un comercio en un período (RF-30, RF-34).</summary>
public record LiquidacionDto
{
    public Guid Id { get; init; }
    public Guid ComercioOperadorId { get; init; }
    public string Comercio { get; init; } = string.Empty;
    public DateOnly PeriodoDesde { get; init; }
    public DateOnly PeriodoHasta { get; init; }
    public int CantidadEnvios { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Iva { get; init; }
    public decimal MontoTotal { get; init; }
    public string Estado { get; init; } = string.Empty; // "Pendiente", "Aprobada", "Pagada", "Anulada"
    public DateTime FechaGeneracion { get; init; }
}

public record MovimientoCuentaCorrienteDto
{
    public Guid Id { get; init; }
    public DateTime Fecha { get; init; }
    public string Concepto { get; init; } = string.Empty;
    /// <summary>Positivo = cargo al comercio; negativo = pago o ajuste a su favor.</summary>
    public decimal Monto { get; init; }
    public Guid? EnvioId { get; init; }
    public Guid? LiquidacionId { get; init; }
}

/// <summary>Estado de cuenta del comercio con el operador; el saldo positivo es lo que el comercio adeuda.</summary>
public record CuentaCorrienteDto
{
    public Guid ComercioOperadorId { get; init; }
    public string Moneda { get; init; } = "UYU";
    public decimal Saldo { get; init; }
    public List<MovimientoCuentaCorrienteDto> Movimientos { get; init; } = new();
}

public record GenerarLiquidacionesDto
{
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    /// <summary>Si se omite, se liquida a todos los comercios del operador con envíos pendientes de liquidar.</summary>
    public Guid? ComercioOperadorId { get; init; }
}

public record ResultadoGeneracionLiquidacionesDto
{
    public List<LiquidacionDto> Liquidaciones { get; init; } = new();
    public int EnviosLiquidados { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}
