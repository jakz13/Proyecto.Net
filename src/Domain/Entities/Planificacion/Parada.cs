using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Planificacion;

public class Parada : Entity
{
    public Guid HojaDeRutaId { get; private set; }
    public Guid EnvioId { get; private set; }
    public int Orden { get; private set; }
    public Guid DireccionId { get; private set; }
    public Guid? FranjaHorariaId { get; private set; }
    public string Estado { get; private set; } = string.Empty;

    protected Parada() { }

    public Parada(
        Guid id,
        Guid hojaDeRutaId,
        Guid envioId,
        int orden,
        Guid direccionId,
        Guid? franjaHorariaId = null,
        string estado = "Pendiente") : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (envioId == Guid.Empty)
            throw new DomainValidationException("El identificador del envío no puede ser vacío.");

        if (direccionId == Guid.Empty)
            throw new DomainValidationException("El identificador de la dirección no puede ser vacío.");

        if (orden <= 0)
            throw new DomainValidationException("El orden de la parada debe ser mayor a cero.");

        HojaDeRutaId = hojaDeRutaId;
        EnvioId = envioId;
        Orden = orden;
        DireccionId = direccionId;
        FranjaHorariaId = franjaHorariaId;
        Estado = string.IsNullOrWhiteSpace(estado) ? "Pendiente" : estado.Trim();
    }

    public void ActualizarOrden(int nuevoOrden)
    {
        if (nuevoOrden <= 0)
            throw new DomainValidationException("El orden debe ser mayor a cero.");

        Orden = nuevoOrden;
    }

    public void CambiarEstado(string nuevoEstado)
    {
        if (string.IsNullOrWhiteSpace(nuevoEstado))
            throw new DomainValidationException("El estado no puede ser vacío.");

        Estado = nuevoEstado.Trim();
    }

    public void IniciarNavegacion()
    {
        if (Estado == "Entregada" || Estado == "Cancelada")
            throw new DomainValidationException($"No se puede iniciar navegación hacia una parada en estado '{Estado}'.");

        Estado = "EnCamino";
    }

    public void RegistrarArribo()
    {
        if (Estado == "Entregada" || Estado == "Cancelada")
            throw new DomainValidationException($"No se puede registrar arribo a una parada en estado '{Estado}'.");

        Estado = "EnSitio";
    }

    public void RegistrarEntregaExitosa()
    {
        if (Estado == "Cancelada")
            throw new DomainValidationException("No se puede registrar entrega exitosa en una parada cancelada.");

        Estado = "Entregada";
    }

    public void RegistrarIntentoFallido()
    {
        if (Estado == "Cancelada")
            throw new DomainValidationException("No se puede registrar un intento fallido en una parada cancelada.");

        Estado = "Fallida";
    }

    /// <summary>La parada nunca se visitó: se cierra así al rendir la hoja de ruta.</summary>
    public void MarcarNoVisitada()
    {
        if (Estado is "Entregada" or "Fallida" or "Cancelada")
            return;

        Estado = "NoVisitada";
    }

    public void CancelarPorFaltante(string motivo)
    {
        Estado = "Cancelada";
    }
}
