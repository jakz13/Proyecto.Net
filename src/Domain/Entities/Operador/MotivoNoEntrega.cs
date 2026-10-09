using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class MotivoNoEntrega : Entity, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    protected MotivoNoEntrega() { }

    public MotivoNoEntrega(
        Guid id,
        Guid operadorId,
        string codigo,
        string descripcion) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del motivo no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainValidationException("El código del motivo no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainValidationException("La descripción del motivo no puede ser vacía.");

        OperadorId = operadorId;
        Codigo = codigo.Trim().ToUpperInvariant();
        Descripcion = descripcion.Trim();
        Activo = true;
    }

    public void Activar()
    {
        Activo = true;
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public void ModificarDescripcion(string descripcion)
    {
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DomainValidationException("La descripción del motivo no puede ser vacía.");

        Descripcion = descripcion.Trim();
    }
}
