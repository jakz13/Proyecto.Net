using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities;

public class Envio
{
    public Guid Id { get; private set; }
    public Guid ComercioId { get; private set; }
    public string DireccionDestino { get; private set; } = string.Empty;
    public string NombreDestinatario { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public EstadoEnvio Estado { get; private set; }

    // Required by EF Core
    protected Envio() { }

    public Envio(Guid id, Guid comercioId, string direccionDestino, string nombreDestinatario)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (comercioId == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(direccionDestino))
            throw new DomainValidationException("La dirección de destino no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(nombreDestinatario))
            throw new DomainValidationException("El nombre del destinatario no puede ser vacío.");

        Id = id;
        ComercioId = comercioId;
        DireccionDestino = direccionDestino.Trim();
        NombreDestinatario = nombreDestinatario.Trim();
        FechaCreacion = DateTime.UtcNow;
        Estado = EstadoEnvio.Admitido;
    }
}
