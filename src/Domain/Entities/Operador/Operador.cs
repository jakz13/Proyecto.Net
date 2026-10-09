using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Operador;

public class Operador : AggregateRoot
{
    public string RazonSocial { get; private set; } = string.Empty;
    public string NombreComercial { get; private set; } = string.Empty;
    public string Estado { get; private set; } = string.Empty;
    public DateTime FechaAlta { get; private set; }

    protected Operador() { }

    public Operador(Guid id, string razonSocial, string nombreComercial, string estado = "Activo") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new DomainValidationException("La razón social del operador no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(nombreComercial))
            throw new DomainValidationException("El nombre comercial del operador no puede ser vacío.");

        RazonSocial = razonSocial.Trim();
        NombreComercial = nombreComercial.Trim();
        Estado = string.IsNullOrWhiteSpace(estado) ? "Activo" : estado.Trim();
        FechaAlta = DateTime.UtcNow;
    }

    public void Activar()
    {
        Estado = "Activo";
    }

    public void Suspender()
    {
        Estado = "Suspendido";
    }

    public void ActualizarDatos(string razonSocial, string nombreComercial)
    {
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new DomainValidationException("La razón social no puede ser vacía.");

        if (string.IsNullOrWhiteSpace(nombreComercial))
            throw new DomainValidationException("El nombre comercial no puede ser vacío.");

        RazonSocial = razonSocial.Trim();
        NombreComercial = nombreComercial.Trim();
    }
}
