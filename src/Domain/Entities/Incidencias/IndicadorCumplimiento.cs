using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Exceptions;

namespace UltimaMilla.Domain.Entities.Incidencias;

public class IndicadorCumplimiento : Entity, IMustHaveTenant
{
    public Guid OperadorId { get; private set; }
    public Guid? ZonaCoberturaId { get; private set; }
    public Guid? RepartidorId { get; private set; }
    public Guid? ComercioOperadorId { get; private set; }
    public string Periodo { get; private set; } = string.Empty;
    public int TotalEnvios { get; private set; }
    public int EntregadosEnPlazo { get; private set; }
    public int EntregadosFueraDePlazo { get; private set; }
    public int NoEntregados { get; private set; }
    public decimal PorcentajeCumplimiento { get; private set; }

    protected IndicadorCumplimiento() { }

    public IndicadorCumplimiento(
        Guid id,
        Guid operadorId,
        string periodo,
        int totalEnvios,
        int entregadosEnPlazo,
        int entregadosFueraDePlazo,
        int noEntregados,
        Guid? zonaCoberturaId = null,
        Guid? repartidorId = null,
        Guid? comercioOperadorId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new DomainValidationException("El identificador del indicador no puede ser vacío.");

        if (operadorId == Guid.Empty)
            throw new DomainValidationException("El identificador del operador no puede ser vacío.");

        if (string.IsNullOrWhiteSpace(periodo))
            throw new DomainValidationException("El período no puede ser vacío.");

        if (totalEnvios < 0 || entregadosEnPlazo < 0 || entregadosFueraDePlazo < 0 || noEntregados < 0)
            throw new DomainValidationException("Las métricas de envíos no pueden ser negativas.");

        OperadorId = operadorId;
        Periodo = periodo.Trim();
        ZonaCoberturaId = zonaCoberturaId;
        RepartidorId = repartidorId;
        ComercioOperadorId = comercioOperadorId;
        TotalEnvios = totalEnvios;
        EntregadosEnPlazo = entregadosEnPlazo;
        EntregadosFueraDePlazo = entregadosFueraDePlazo;
        NoEntregados = noEntregados;
        PorcentajeCumplimiento = totalEnvios > 0 ? (decimal)entregadosEnPlazo / totalEnvios * 100m : 0m;
    }

    public void ActualizarMetricas(int totalEnvios, int entregadosEnPlazo, int entregadosFueraDePlazo, int noEntregados)
    {
        if (totalEnvios < 0 || entregadosEnPlazo < 0 || entregadosFueraDePlazo < 0 || noEntregados < 0)
            throw new DomainValidationException("Las métricas no pueden ser negativas.");

        TotalEnvios = totalEnvios;
        EntregadosEnPlazo = entregadosEnPlazo;
        EntregadosFueraDePlazo = entregadosFueraDePlazo;
        NoEntregados = noEntregados;
        PorcentajeCumplimiento = totalEnvios > 0 ? (decimal)entregadosEnPlazo / totalEnvios * 100m : 0m;
    }
}
