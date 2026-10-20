using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Ejecucion;

public class Devolucion : Entity
{
    public Guid EnvioId { get; private set; }
    public Guid? MotivoDevolucionId { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public DateTime FechaInicio { get; private set; }
    public DateTime? FechaRendicion { get; private set; }

    protected Devolucion() { }

    public Devolucion(
        Guid id,
        Guid envioId,
        Guid? motivoDevolucionId = null,
        string estado = "EnDevolucion") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la devolución no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        EnvioId = envioId;
        MotivoDevolucionId = motivoDevolucionId;
        Estado = string.IsNullOrWhiteSpace(estado) ? "EnDevolucion" : estado.Trim();
        FechaInicio = DateTime.UtcNow;
    }

    public const string EstadoEnDevolucion = "EnDevolucion";
    public const string EstadoEnCaminoADeposito = "EnCaminoADeposito";
    public const string EstadoDevuelto = "Devuelto";

    /// <summary>El repartidor retiró el bulto en la calle y lo lleva de vuelta al depósito (UC-22).</summary>
    public void MarcarEnCaminoADeposito()
    {
        if (Estado == EstadoDevuelto)
            throw new DomainValidationException("La devolución ya fue cerrada.");

        Estado = EstadoEnCaminoADeposito;
    }

    public void Rendir()
    {
        Estado = EstadoDevuelto;
        FechaRendicion = DateTime.UtcNow;
    }
}
