using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class Repartidor : Entity, IMustHaveTenant
{
    public Guid UsuarioId { get; private set; }
    public Guid OperadorId { get; private set; }
    public Guid? VehiculoAsignadoId { get; private set; }
    public string Documento { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;

    protected Repartidor() { }

    public Repartidor(
        Guid id,
        Guid usuarioId,
        Guid operadorId,
        string documento,
        string telefono,
        Guid? vehiculoAsignadoId = null,
        string estado = "Disponible") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del repartidor no puede ser vacío.");

        if (usuarioId == Guid.Empty)
            throw new DomainValidationException("El identificador del usuario no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(documento))
            throw new DomainValidationException("El documento del repartidor no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainValidationException("El teléfono del repartidor no puede ser vacío.");

        UsuarioId = usuarioId;
        OperadorId = operadorId;
        VehiculoAsignadoId = vehiculoAsignadoId;
        Documento = documento.Trim();
        Telefono = telefono.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? "Disponible" : estado.Trim();
    }

    public void AsignarVehiculo(Guid vehiculoId)
    {
        if (vehiculoId == Guid.Empty)
            throw new DomainValidationException("El identificador del vehículo no puede ser vacío.");

        VehiculoAsignadoId = vehiculoId;
    }

    public void DesasignarVehiculo()
    {
        VehiculoAsignadoId = null;
    }

    public void CambiarEstado(string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new DomainValidationException("El nuevo estado no puede ser vacío.");

        Estado = nuevoEstado.Trim();
    }

    public void ActualizarContacto(string telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainValidationException("El teléfono no puede ser vacío.");

        Telefono = telefono.Trim();
    }
}
