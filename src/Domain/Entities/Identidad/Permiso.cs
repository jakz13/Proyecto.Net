using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Identidad;

public class Permiso : Entity
{
    public string Codigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;

    protected Permiso() { }

    public Permiso(Guid id, string codigo, string descripcion) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del permiso no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainValidationException("El código del permiso no puede ser vacío.");

        Codigo = codigo.Trim().ToUpperInvariant();
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }

    public void Modificar(string codigo, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new DomainValidationException("El código del permiso no puede ser vacío.");

        Codigo = codigo.Trim().ToUpperInvariant();
        Descripcion = descripcion?.Trim() ?? string.Empty;
    }
}
