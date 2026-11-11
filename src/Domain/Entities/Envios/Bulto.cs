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

    /// <summary>Código del motivo de no entrega registrado para este bulto cuando no pudo entregarse en la parada (RF-32). Se persiste.</summary>
    public string? MotivoNoEntregaCodigo { get; private set; }

    /// <summary>Identificador de la prueba de entrega asociada cuando el bulto fue efectivamente entregado (RF-32). Se persiste.</summary>
    public Guid? PruebaDeEntregaId { get; private set; }

    /// <summary>Indica si el bulto ya fue entregado al destinatario.</summary>
    public bool EstaEntregado => string.Equals(EstadoBulto, MaquinaEstadosBulto.Entregado, StringComparison.Ordinal);

    /// <summary>Indica si el bulto se encuentra en depósito o pendiente de reenvío y disponible para asignarse a una hoja de ruta (RF-34).</summary>
    public bool EstaDisponibleParaRuta =>
        EstadoBulto is MaquinaEstadosBulto.Recibido or MaquinaEstadosBulto.Danado or MaquinaEstadosBulto.PendienteReenvio;

    protected Bulto() { }

    public Bulto(
        Guid id,
        Guid envioId,
        string identificacion,
        decimal peso,
        decimal alto,
        decimal ancho,
        decimal profundo,
        string estadoBulto = MaquinaEstadosBulto.Ingresado) : base(id)
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
        EstadoBulto = string.IsNullOrWhiteSpace(estadoBulto) ? MaquinaEstadosBulto.Ingresado : estadoBulto.Trim();
    }

    public void ActualizarEstado(string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new DomainValidationException("El estado del bulto no puede ser vacío.");

        EstadoBulto = nuevoEstado.Trim();
    }

    public void MarcarRecibido()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.Recibido);
        EstadoBulto = MaquinaEstadosBulto.Recibido;
    }

    public void MarcarDanado()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.Danado);
        EstadoBulto = MaquinaEstadosBulto.Danado;
    }

    public void MarcarFaltanteEnRecepcion()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.FaltanteEnRecepcion);
        EstadoBulto = MaquinaEstadosBulto.FaltanteEnRecepcion;
    }

    public void MarcarCargadoEnVehiculo()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.CargadoEnVehiculo);
        EstadoBulto = MaquinaEstadosBulto.CargadoEnVehiculo;
    }

    public void MarcarCargado() => MarcarCargadoEnVehiculo();

    public void MarcarFaltanteEnCarga()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.FaltanteEnCarga);
        EstadoBulto = MaquinaEstadosBulto.FaltanteEnCarga;
    }

    public void MarcarFaltante(string? motivo = null) => MarcarFaltanteEnCarga();

    public void MarcarEnRetornoADeposito()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.EnRetornoADeposito);
        EstadoBulto = MaquinaEstadosBulto.EnRetornoADeposito;
    }

    /// <summary>Registra la entrega efectiva del bulto y lo vincula con la prueba de entrega correspondiente (RF-31, RF-32).</summary>
    public void MarcarEntregado(Guid? pruebaDeEntregaId = null)
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.Entregado);
        EstadoBulto = MaquinaEstadosBulto.Entregado;
        PruebaDeEntregaId = pruebaDeEntregaId;
        MotivoNoEntregaCodigo = null;
    }

    /// <summary>
    /// Registra que el bulto no pudo entregarse en la parada, guarda el motivo del catálogo del operador
    /// y lo transiciona al estado posterior según la regla operativa vigente (RF-32, RF-33).
    /// </summary>
    public void MarcarNoEntregado(string codigoMotivo, string? destinoSegunRegla = null)
    {
        if (string.IsNullOrWhiteSpace(codigoMotivo))
            throw new DomainValidationException($"Debe indicar el motivo de no entrega para el bulto '{Identificacion}'.");

        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.NoEntregado);
        EstadoBulto = MaquinaEstadosBulto.NoEntregado;
        MotivoNoEntregaCodigo = codigoMotivo.Trim().ToUpperInvariant();
        PruebaDeEntregaId = null;

        if (!string.IsNullOrWhiteSpace(destinoSegunRegla)
            && !string.Equals(destinoSegunRegla, MaquinaEstadosBulto.NoEntregado, StringComparison.Ordinal))
        {
            MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, destinoSegunRegla.Trim());
            EstadoBulto = destinoSegunRegla.Trim();
        }
    }

    /// <summary>Marca el bulto como devuelto al comercio de origen.</summary>
    public void MarcarDevuelto()
    {
        MaquinaEstadosBulto.ValidarTransicion(Identificacion, EstadoBulto, MaquinaEstadosBulto.Devuelto);
        EstadoBulto = MaquinaEstadosBulto.Devuelto;
    }

    /// <summary>Valida que el bulto admita la clasificación indicada y devuelve el tipo normalizado.</summary>
    public string? ValidarClasificacion(PrioridadBulto prioridad, Guid? zonaCoberturaId, string? tipo)
    {
        if (!Enum.IsDefined(prioridad))
            throw new DomainValidationException($"La prioridad '{prioridad}' no es válida.");

        if (zonaCoberturaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la zona de cobertura no puede ser vacío.");

        if (EstadoBulto is not (MaquinaEstadosBulto.Recibido or MaquinaEstadosBulto.Danado or MaquinaEstadosBulto.PendienteReenvio))
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
