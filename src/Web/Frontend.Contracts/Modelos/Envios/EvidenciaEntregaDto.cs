namespace UltimaMilla.Frontend.Contracts.Modelos.Envios;

/// <summary>
/// Evidencia que dejó el repartidor en un intento de entrega: quién recibió y si hay firma o foto para consultar.
/// El contenido de las imágenes se pide aparte (<see cref="ImagenEvidenciaDto"/>) para no viajar en el detalle.
/// </summary>
public record EvidenciaEntregaDto
{
    public Guid IntentoId { get; init; }
    public DateTime FechaHora { get; init; }
    public string Resultado { get; init; } = string.Empty;
    public string? NombreReceptor { get; init; }
    public string? DocumentoReceptor { get; init; }
    public bool TieneFirma { get; init; }
    public bool TieneFoto { get; init; }
}

/// <summary>Imagen de una firma o de una foto de evidencia.</summary>
public record ImagenEvidenciaDto(byte[] Contenido, string TipoContenido);
