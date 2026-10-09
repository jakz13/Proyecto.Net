using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class TransicionEstadoPermitida : Entity
{
    public EstadoEnvio EstadoOrigen { get; private set; }
    public EstadoEnvio EstadoDestino { get; private set; }
    public string? Condicion { get; private set; }

    protected TransicionEstadoPermitida() { }

    public TransicionEstadoPermitida(
        Guid id,
        EstadoEnvio estadoOrigen,
        EstadoEnvio estadoDestino,
        string? condicion = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la transición permitida no puede ser vacío.");

        EstadoOrigen = estadoOrigen;
        EstadoDestino = estadoDestino;
        Condicion = condicion?.Trim();
    }
}
