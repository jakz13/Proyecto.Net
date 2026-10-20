using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Enums;
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

    /// <summary>Volumen del bulto en metros cúbicos (las dimensiones se informan en centímetros).</summary>
    public decimal Volumen => Alto * Ancho * Profundo / 1_000_000m;

    public PrioridadBulto? Prioridad { get; private set; }
    public Guid? ZonaClasificadaId { get; private set; }
    public string? TipoClasificacion { get; private set; }
    public DateTime? FechaClasificacion { get; private set; }

    public bool EstaClasificado => FechaClasificacion.HasValue;

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

    public void MarcarCargadoEnVehiculo()
    {
        EstadoBulto = "CargadoEnVehiculo";
    }

    /// <summary>Valida que el bulto admita la clasificación indicada y devuelve el tipo normalizado.</summary>
    public string? ValidarClasificacion(PrioridadBulto prioridad, Guid? zonaCoberturaId, string? tipo)
    {
        if (!Enum.IsDefined(prioridad))
            throw new DomainValidationException($"La prioridad '{prioridad}' no es válida.");

        if (zonaCoberturaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        if (EstadoBulto is not ("Recibido" or "Danado"))
            throw new DomainValidationException($"El bulto {Identificacion} no se puede clasificar porque su estado es '{EstadoBulto}'.");

        var tipoNormalizado = string.IsNullOrWhiteSpace(tipo) ? null : tipo.Trim();
        if (tipoNormalizado is { Length: > 50 })
            throw new DomainValidationException("El tipo de clasificación no puede superar los 50 caracteres.");

        return tipoNormalizado;
    }

    /// <summary>Registra la clasificación del bulto. Se puede reclasificar mientras el envío siga en depósito.</summary>
    public void Clasificar(PrioridadBulto prioridad, Guid? zonaCoberturaId, string? tipo)
    {
        var tipoNormalizado = ValidarClasificacion(prioridad, zonaCoberturaId, tipo);

        Prioridad = prioridad;
        ZonaClasificadaId = zonaCoberturaId;
        TipoClasificacion = tipoNormalizado;
        FechaClasificacion = DateTime.UtcNow;
    }

    public void MarcarEnRetornoADeposito() => EstadoBulto = "EnRetornoADeposito";

    public void MarcarRecibido() => EstadoBulto = "Recibido";

    public void MarcarFaltanteEnRecepcion() => EstadoBulto = "FaltanteEnRecepcion";

    public void MarcarDanado() => EstadoBulto = "Danado";

    public void MarcarCargado() => MarcarCargadoEnVehiculo();

    public void MarcarFaltanteEnCarga()
    {
        EstadoBulto = "FaltanteEnCarga";
    }

    public void MarcarFaltante(string? motivo = null) => MarcarFaltanteEnCarga();

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
