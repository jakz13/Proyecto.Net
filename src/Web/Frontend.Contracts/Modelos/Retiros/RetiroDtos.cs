namespace UltimaMilla.Frontend.Contracts.Modelos.Retiros;

/// <summary>Bulto que se pide retirar: descripción, peso en kg y dimensiones en centímetros.</summary>
public record BultoRetiroSolicitudDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Peso { get; set; } = 1;
    public decimal Alto { get; set; } = 20;
    public decimal Ancho { get; set; } = 20;
    public decimal Profundo { get; set; } = 20;
}

/// <summary>Solicitud de retiro en domicilio que hace el comercio desde su portal.</summary>
public record SolicitarRetiroDto
{
    public string NombreContacto { get; set; } = string.Empty;
    public string? TelefonoContacto { get; set; }
    public string? EmailContacto { get; set; }
    public string Calle { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Ciudad { get; set; } = "Montevideo";
    public string? Referencia { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateOnly? FechaSolicitada { get; set; }
    public string? ReferenciaExterna { get; set; }
    public List<BultoRetiroSolicitudDto> Bultos { get; set; } = new() { new BultoRetiroSolicitudDto() };
}

public record ResultadoSolicitudRetiroDto
{
    public Guid Id { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public decimal Tarifa { get; init; }
    public bool Duplicado { get; init; }
}

public record RetiroResumenDto
{
    public Guid Id { get; init; }
    public string CodigoSeguimiento { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string NombreContacto { get; init; } = string.Empty;
    public string DireccionRetiro { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public int CantidadBultos { get; init; }
    public decimal PesoTotal { get; init; }
    public decimal Tarifa { get; init; }
    public DateTime FechaSolicitud { get; init; }
    public Guid? HojaDeRutaId { get; init; }
}

public record BultoRetiroDto
{
    public string Identificacion { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Peso { get; init; }
}

public record EventoRetiroDto
{
    public DateTime Fecha { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string? EstadoAnterior { get; init; }
    public string EstadoNuevo { get; init; } = string.Empty;
    public string Origen { get; init; } = string.Empty;
}

public record RetiroDetalleDto
{
    public RetiroResumenDto Resumen { get; init; } = new();
    public IReadOnlyList<BultoRetiroDto> Bultos { get; init; } = Array.Empty<BultoRetiroDto>();
    public IReadOnlyList<EventoRetiroDto> Eventos { get; init; } = Array.Empty<EventoRetiroDto>();
    public bool TieneFotoEstado { get; init; }
    public bool TieneFirma { get; init; }
}

public record PaginaRetirosDto
{
    public IReadOnlyList<RetiroResumenDto> Items { get; init; } = Array.Empty<RetiroResumenDto>();
    public int Total { get; init; }
}

/// <summary>Retiros del operador con el conteo por estado, para el tablero y los reportes.</summary>
public record ResumenRetirosOperadorDto
{
    public Dictionary<string, int> PorEstado { get; init; } = new();
    public int Total { get; init; }
    public int EnCurso { get; init; }
    public IReadOnlyList<RetiroResumenDto> Items { get; init; } = Array.Empty<RetiroResumenDto>();
}
