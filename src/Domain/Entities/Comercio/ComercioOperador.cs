using System.Security.Cryptography;
using System.Text;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Comercio;

public class ComercioOperador : Entity, IMustHaveTenant
{
    public Guid ComercioId { get; private set; }
    public Guid OperadorId { get; private set; }
    public DateTime FechaAlta { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    /// <summary>Huella SHA-256 (hexadecimal) de la clave de integración; la clave en claro nunca se almacena.</summary>
    public string ClaveApi { get; private set; } = string.Empty;

    protected ComercioOperador() { }

    public ComercioOperador(
        Guid id,
        Guid comercioId,
        Guid operadorId,
        string claveApi,
        string estado = "Activo") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la relación comercio-operador no puede ser vacío.");

        if (comercioId == Guid.Empty)
            throw new DomainValidationException("El identificador del comercio no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(claveApi))
            throw new DomainValidationException("La clave API no puede ser vacía.");

        ComercioId = comercioId;
        OperadorId = operadorId;
        ClaveApi = claveApi.Trim();
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

    public void RegenerarClaveApi(string nuevaClaveApi)
    {
        if (string.IsNullOrWhiteSpace(nuevaClaveApi))
            throw new DomainValidationException("La nueva clave API no puede ser vacía.");

        ClaveApi = nuevaClaveApi.Trim();
    }

    public bool EstaActivo => Estado == "Activo";

    public static string CalcularHuellaClaveApi(string claveApi)
    {
        if (string.IsNullOrWhiteSpace(claveApi))
            throw new DomainValidationException("La clave API no puede ser vacía.");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(claveApi.Trim())));
    }
}
