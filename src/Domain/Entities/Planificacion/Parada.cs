using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Planificacion;

public class Parada : Entity
{
    public const string TipoEntrega = "Entrega";
    public const string TipoRetiro = "Retiro";

    public Guid HojaDeRutaId { get; private set; }

    /// <summary>"Entrega" (lleva un envío al destinatario) o "Retiro" (retira bultos en el domicilio del comprador, RF 39).</summary>
    public string Tipo { get; private set; } = TipoEntrega;

    /// <summary>Envío que se entrega en la parada; null en las paradas de retiro.</summary>
    public Guid? EnvioId { get; private set; }

    /// <summary>Retiro que se hace en la parada; null en las paradas de entrega.</summary>
    public Guid? RetiroId { get; private set; }

    public bool EsRetiro => Tipo == TipoRetiro;
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

    /// <summary>Crea una parada de retiro en domicilio (RF 39): la hoja de ruta pasa a ser mixta, con entregas y retiros.</summary>
    public static Parada ParaRetiro(Guid id, Guid hojaDeRutaId, Guid retiroId, int orden, Guid direccionId, Guid? franjaHorariaId = null)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador de la parada no puede ser vacío.");

        if (hojaDeRutaId == Guid.Empty)
            throw new DomainValidationException("El identificador de la hoja de ruta no puede ser vacío.");

        if (retiroId == Guid.Empty)
            throw new DomainValidationException("El identificador del retiro no puede ser vacío.");

        if (direccionId == Guid.Empty)
            throw new DomainValidationException("El identificador de la dirección no puede ser vacío.");

        if (orden <= 0)
            throw new DomainValidationException("El orden de la parada debe ser mayor a cero.");

        return new Parada
        {
            Id = id,
            HojaDeRutaId = hojaDeRutaId,
            Tipo = TipoRetiro,
            RetiroId = retiroId,
            Orden = orden,
            DireccionId = direccionId,
            FranjaHorariaId = franjaHorariaId,
            Estado = "Pendiente"
        };
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
        if (Estado is "Entregada" or "EntregadaParcialmente" or "Retirada" or "NoRetirada" or "Cancelada")
            throw new DomainValidationException($"No se puede iniciar navegación hacia una parada en estado '{Estado}'.");

        Estado = "EnCamino";
    }

    public void RegistrarArribo()
    {
        if (Estado is "Entregada" or "EntregadaParcialmente" or "Retirada" or "NoRetirada" or "Cancelada")
            throw new DomainValidationException($"No se puede registrar arribo a una parada en estado '{Estado}'.");

        Estado = "EnSitio";
    }

    public void RegistrarEntregaExitosa(bool forzarPorConflicto = false)
    {
        if (Estado == "Cancelada" && !forzarPorConflicto)
            throw new DomainValidationException("No se puede registrar entrega exitosa en una parada cancelada.");

        Estado = "Entregada";
    }

    public void RegistrarEntregaParcial()
    {
        if (Estado == "Cancelada")
            throw new DomainValidationException("No se puede registrar entrega parcial en una parada cancelada.");

        Estado = "EntregadaParcialmente";
    }

    public void RegistrarIntentoFallido()
    {
        if (Estado == "Cancelada")
            throw new DomainValidationException("No se puede registrar un intento fallido en una parada cancelada.");

        Estado = "Fallida";
    }

    /// <summary>El repartidor retiró los bultos en el domicilio (solo paradas de retiro).</summary>
    public void RegistrarRetiroRealizado(bool resolverConflicto = false)
    {
        if (!EsRetiro)
            throw new DomainValidationException("Solo una parada de retiro puede registrar un retiro.");

        // Si el despachador la canceló mientras el dispositivo estaba sin conexión, prevalece lo que ocurrió en el domicilio
        if (Estado == "Cancelada" && !resolverConflicto)
            throw new DomainValidationException("No se puede registrar un retiro en una parada cancelada.");

        Estado = "Retirada";
    }

    /// <summary>El repartidor no pudo retirar los bultos (solo paradas de retiro).</summary>
    public void RegistrarRetiroNoRealizado(bool resolverConflicto = false)
    {
        if (!EsRetiro)
            throw new DomainValidationException("Solo una parada de retiro puede registrar un retiro no realizado.");

        if (Estado == "Cancelada" && !resolverConflicto)
            throw new DomainValidationException("No se puede registrar un retiro en una parada cancelada.");

        Estado = "NoRetirada";
    }

    /// <summary>La parada nunca se visitó: se cierra así al rendir la hoja de ruta.</summary>
    public void MarcarNoVisitada()
    {
        if (Estado is "Entregada" or "EntregadaParcialmente" or "Retirada" or "NoRetirada" or "Fallida" or "Cancelada")
            return;

        Estado = "NoVisitada";
    }

    public void CancelarPorFaltante(string motivo)
    {
        Estado = "Cancelada";
    }
}

/// <summary>Ayudas para trabajar con hojas de ruta mixtas, que mezclan paradas de entrega y de retiro.</summary>
public static class ParadaExtensiones
{
    public static IEnumerable<Guid> IdsDeEnvios(this IEnumerable<Parada> paradas) =>
        paradas.Where(p => p.EnvioId.HasValue).Select(p => p.EnvioId!.Value);

    public static IEnumerable<Guid> IdsDeRetiros(this IEnumerable<Parada> paradas) =>
        paradas.Where(p => p.RetiroId.HasValue).Select(p => p.RetiroId!.Value);
}
