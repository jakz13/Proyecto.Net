using MediatR;

namespace UltimaMilla.Application.Queries.Envios.ObtenerEvidenciaEntrega;

/// <summary>Tipo de evidencia que el repartidor adjunta a un intento de entrega.</summary>
public enum TipoEvidenciaEntrega
{
    Firma,
    Foto
}

/// <summary>Imagen de la firma o de la foto de un intento de entrega, lista para servirse como archivo.</summary>
public record ObtenerEvidenciaEntregaQuery(Guid EnvioId, Guid IntentoId, TipoEvidenciaEntrega Tipo) : IRequest<EvidenciaEntregaDto>;

public record EvidenciaEntregaDto(byte[] Contenido, string TipoContenido);
