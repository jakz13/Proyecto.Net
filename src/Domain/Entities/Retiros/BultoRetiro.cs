using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Retiros;

/// <summary>Bulto que el operador debe retirar en el domicilio del comprador. Cada retiro incluye uno o más.</summary>
public class BultoRetiro : Entity
{
    public Guid RetiroId { get; private set; }
    public string Identificacion { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public decimal Peso { get; private set; }
    public decimal Alto { get; private set; }
    public decimal Ancho { get; private set; }
    public decimal Profundo { get; private set; }

    /// <summary>Volumen en metros cúbicos (las dimensiones se informan en centímetros), igual que en los bultos de envío.</summary>
    public decimal Volumen => Alto * Ancho * Profundo / 1_000_000m;

    protected BultoRetiro() { }

    public BultoRetiro(Guid id, Guid retiroId, string identificacion, string descripcion, decimal peso, decimal alto, decimal ancho, decimal profundo)
        : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del bulto a retirar no puede ser vacío.");

        if (retiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new DomainValidationException("La identificación del bulto a retirar no puede ser vacía.");

        if (peso <= 0)
            throw new DomainValidationException("El peso del bulto a retirar debe ser mayor a cero.");

        if (alto <= 0 || ancho <= 0 || profundo <= 0)
            throw new DomainValidationException("Las dimensiones del bulto a retirar deben ser mayores a cero.");

        ValidacionTexto.LargoMaximo(descripcion, 200, "La descripción del bulto");

        RetiroId = retiroId;
        Identificacion = identificacion.Trim().ToUpperInvariant();
        Descripcion = descripcion?.Trim() ?? string.Empty;
        Peso = peso;
        Alto = alto;
        Ancho = ancho;
        Profundo = profundo;
    }
}
