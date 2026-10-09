using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Envios;

public class Bulto : Entity
{
    public Guid EnvioId { get; private set; }
    public string Identificacion { get; private set; } = string.Empty;
    public decimal Peso { get; private set; }
    public decimal Alto { get; private set; }
    public decimal Ancho { get; private set; }
    public decimal Profundo { get; private set; }
    public string EstadoBulto { get; private set; } = string.Empty;

    protected Bulto() { }

    public Bulto(
        Guid id,
        Guid envioId,
        string identificacion,
        decimal peso,
        decimal alto,
        decimal ancho,
        decimal profundo,
        string estadoBulto = "Ingresado") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del bulto no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(identificacion))
            throw new DomainValidationException("La identificación del bulto no puede ser vacía.");

        if (peso <= 0)
            throw new DomainValidationException("El peso del bulto debe ser mayor a cero.");

        if (alto <= 0 || ancho <= 0 || profundo <= 0)
            throw new DomainValidationException("Las dimensiones del bulto deben ser mayores a cero.");

        EnvioId = envioId;
        Identificacion = identificacion.Trim().ToUpperInvariant();
        Peso = peso;
        Alto = alto;
        Ancho = ancho;
        Profundo = profundo;
        EstadoBulto = string.IsNullOrWhiteSpace(estadoBulto) ? "Ingresado" : estadoBulto.Trim();
    }

    public void ActualizarEstado(string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new DomainValidationException("El estado del bulto no puede ser vacío.");

        EstadoBulto = nuevoEstado.Trim();
    }

    public void ActualizarDimensiones(decimal peso, decimal alto, decimal ancho, decimal profundo)
    {
        if (peso <= 0)
            throw new DomainValidationException("El peso del bulto debe ser mayor a cero.");

        if (alto <= 0 || ancho <= 0 || profundo <= 0)
            throw new DomainValidationException("Las dimensiones del bulto deben ser mayores a cero.");

        Peso = peso;
        Alto = alto;
        Ancho = ancho;
        Profundo = profundo;
    }
}
